# Vayu Architecture

## Goals

- **Local-first**: every feature must work offline; cloud is additive, opt-in, and per-feature.
- **Modular**: each capability is a separate project with a small interface; the desktop app composes them through DI.
- **Safe**: no side-effecting action runs without the permission engine clearing it.
- **Testable**: business logic lives in `src/` libraries (not in the WinUI app). Anything UI-free is unit-testable.

## Layers

```
Presentation         apps/Vayu.Desktop (WinUI 3), Vayu.Tray, Vayu.Cli
Application          Vayu.AgentRuntime, Vayu.Permissions, workflow engine
Domain               Vayu.Core (interfaces, intents, results, risk types)
Platform / I/O       AI providers, connectors, automation, voice, memory, security, logging
Persistence          SQLite (audit, settings, memory), DPAPI-encrypted JSON (fallback secrets)
```

A higher layer may depend on a lower layer; **never** the other way. `Vayu.Core` has no dependencies.

## Request lifecycle

```
1. Input              text box, voice, hotkey, tray, clap
2. CommandParser      raw string → structured CommandRequest
3. AI Router          chooses provider (local first; cloud only if enabled + consented)
4. IntentPlanner      LLM (or rule-based fallback) returns IntentPlan with declared RiskLevel
5. PermissionEngine   resolves: Allowed / NeedsConfirmation / Denied; shows UI if needed
6. AgentRuntime       routes IntentPlan to the agent that registered for it
7. Connector / Tool   actually does the thing (open app, draft email, run shell)
8. Result             CommandResult { Success | Failed | PermissionRequired | Cancelled | NeedsClarification }
9. AuditLog           always written, with redaction
10. Response          UI status + optional TTS
```

The model **never** invokes a tool directly. It produces an `IntentPlan`; the runtime, gated by permissions, calls the tool.

## Key interfaces (Vayu.Core)

- `ICommandHandler` — receives a `CommandRequest`, returns a `CommandResult`.
- `IAgent` — declares supported intents, max risk level it can request, and the `Execute(IntentPlan)` method.
- `IAgentRuntime` — registry + dispatcher; resolves an `IntentPlan` to an `IAgent`.
- `IPermissionService` — evaluates an `IntentPlan` against policy and (when needed) the user.
- `IAuditLogService` — append-only redacted log.
- `IAiProvider` (with `ILocalAiProvider`, `IOnlineAiProvider`) — `Plan(prompt, context) → IntentPlan`.
- `ISecretStore` — `Get / Set / Delete / Exists` for named secrets.
- `IWindowsAutomationService`, `IAppLauncher`, `IVoiceInputService`, `IWakeTriggerService` — platform services.
- Connector interfaces: `IGmailConnector`, `IVSCodeConnector`, `IWorkflowService`, `IMemoryStore`.

## AI Router

Pseudocode:

```csharp
public async Task<IntentPlan> Plan(CommandRequest req, CancellationToken ct)
{
    if (_settings.Mode == AiMode.Offline)
        return await _local.PlanAsync(req, ct);

    if (_settings.Mode == AiMode.Online)
        return await _online.PlanAsync(req, ct);

    // Hybrid
    var plan = await _local.PlanAsync(req, ct);
    if (plan.Confidence >= _settings.HybridLocalFloor) return plan;

    if (!await _consent.AllowsCloudPlanning(req)) return plan; // user said no — keep local
    return await _online.PlanAsync(req, ct);
}
```

## Data

SQLite tables — see [docs/SECURITY.md](SECURITY.md) for the no-secrets rule.

- `Actions` — audit log (every command, every decision)
- `Permissions` — workflow-scoped "always allow" grants
- `Workflows`, `WorkflowSteps`
- `UserSettings`
- `Agents` — registered agents and their declared max risk level
- `Memories` — long-running user-facing memory (notes, shortcuts)
- `AppShortcuts` — user's custom "open X" mappings
- `AiRequests` — redacted prompt/response metadata (no full bodies unless debug)
- `ErrorReports`

## Threading / hosting

The desktop app uses `Microsoft.Extensions.Hosting` for DI. UI thread (dispatcher) calls into services through `IAgentRuntime`. Long-running work is `async` end-to-end. Voice and wake-word listeners run on background hosted services.

## Why this shape

- Putting permissions *between* planning and execution means even a compromised or hallucinating model cannot trigger a side effect — the user is always the last switch.
- Keeping every connector behind an interface makes Gmail, GitHub, Spotify, etc. trivially mockable in tests and replaceable per platform.
- A single `CommandResult` type lets the UI render any agent's outcome uniformly.
