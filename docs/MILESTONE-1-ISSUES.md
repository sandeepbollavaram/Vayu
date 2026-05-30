# Milestone 1 — GitHub Issues to create

Open these as individual issues with the label `milestone-1`. They are sized to be small, reviewable PRs.

## Foundation

- **M1-01 Scaffold solution and projects**
  - Acceptance: `pwsh ./scripts/scaffold.ps1` creates the solution + 18 projects; `dotnet build` succeeds.
- **M1-02 .editorconfig, Directory.Build.props with nullable + warnings-as-errors**
- **M1-03 CI workflow green on first PR**

## First Run Setup Wizard (docs + interfaces only for M1)

- **M1-34 Add First Run Setup Wizard architecture doc** (`docs/FIRST_RUN_SETUP.md`)
  - Acceptance: doc covers flow, Ollama detection, Gemma model picks (`gemma3:4b` recommended, `gemma3:1b` low-end, `llama3.2:3b` alternative), Gemini key storage rules, troubleshooting, safety rules.
- **M1-35 Add `FirstRunSetupMode` enum** in `Vayu.Core.Setup`
  - Values: `OfflineOnly`, `OnlineOnly`, `Hybrid`.
- **M1-36 Add `FirstRunSetupState` record** in `Vayu.Core.Setup`
  - Fields: `Completed`, `Mode`, `OllamaDetected`, `OllamaEndpoint`, `SelectedLocalModel`, `LocalModelInstalled`, `GeminiKeyConfigured`, `CompletedAtUtc`.
- **M1-37 Add `IFirstRunSetupService` interface** in `Vayu.Core.Setup`
  - Contract: `GetStateAsync`, `StartAsync(mode)`, `CompleteAsync`, `ResetAsync`. No implementation in M1.
- **M1-38 Add `OllamaModelInfo` record** in `Vayu.AI.Local`
  - Fields: `Name`, `Tag`, `SizeBytes`, `IsPresent`.
- **M1-39 Add `IOllamaRuntimeService` interface** in `Vayu.AI.Local`
  - Contract: `IsInstalledAsync`, `IsReachableAsync`, `ListLocalModelsAsync`, `Endpoint` (Uri). No implementation in M1 — real detection lands in M2.
- **M1-40 Add `IGeminiKeySetupService` interface** in `Vayu.AI.Gemini`
  - Contract: `IsKeyConfiguredAsync`, `SaveKeyAsync(string apiKey)`, `RemoveKeyAsync`, `ValidateKeyAsync`. No implementation in M1 — real save/validate lands in M3.

> Real installer/runtime/HTTP work is **not** in M1. See `docs/ROADMAP.md` Milestones 2 and 3.

## Vayu.Core

- **M1-04 Define `RiskLevel` enum (L0–L6)**
- **M1-05 Define `CommandRequest`, `IntentPlan`, `CommandResult` records**
- **M1-06 Define `ICommandHandler`, `IAgent`, `IAgentRuntime`**

## Vayu.Security

- **M1-07 Implement `SecretRedactor` with default pattern set**
  - Tests for each pattern in `Vayu.Security.Tests`.
- **M1-08 Implement `ISecretStore` + `EnvironmentSecretStore`**
- **M1-09 Implement `WindowsCredentialSecretStore`** (uses `Meziantou.Framework.Win32.CredentialManager` or P/Invoke `CredRead`/`CredWrite`)
- **M1-10 Implement `EncryptedJsonSecretStore`** (DPAPI, CurrentUser)
- **M1-11 Implement `SecureConfigService`** — resolves source priority from `appsettings.json`
- **M1-12 Test: no path in `Vayu.AI.Gemini` (stub) logs the key**

## Vayu.Logging

- **M1-13 Serilog configurator with rolling file sink and in-memory ring buffer sink**
- **M1-14 `SecretRedactingEnricher` wired in; test logs a fake key and asserts `[REDACTED]`**

## Vayu.Memory

- **M1-15 SQLite schema migrations (EF Core or Dapper) for `Actions`, `UserSettings`, `AppShortcuts`**
- **M1-16 `IAuditLogService` + implementation; round-trip test**

## Vayu.Permissions

- **M1-17 `IPermissionService` with risk policy (>= L3 → confirm)**
- **M1-18 `IConfirmationPrompt` interface; fake implementation for tests**
- **M1-19 Test: L4 plan with no consent returns `PermissionRequired`; with Allow returns `Allowed`**

## Vayu.AgentRuntime

- **M1-20 Rule-based command parser for M1 vocabulary**
  - `open <app>` → `app.launch`
  - `show logs` → `ui.openLogs`
  - `show settings` → `ui.openSettings`
- **M1-21 `AgentRuntime` dispatcher; registers agents; resolves intent → agent**
- **M1-22 Test: end-to-end `dispatch("open notepad")` → `AppLauncherAgent` invoked with correct args**

## Vayu.Automation.Windows

- **M1-23 `IAppLauncher` implementation for chrome / edge / vscode / notepad / terminal / downloads**
  - Uses `Process.Start` with `ProcessStartInfo.UseShellExecute = true` and a fixed map of well-known apps. User-defined shortcuts come from `AppShortcuts` table.
- **M1-24 `AppLauncherAgent` declaring `MaxRisk = L1`, intent `app.launch`**

## Vayu.Desktop (WinUI 3)

- **M1-25 App shell with NavigationView: Home, Logs, Settings, Security pages**
- **M1-26 Home page: Vayu orb placeholder + command textbox + last result panel**
- **M1-27 Logs page: virtualized list bound to in-memory ring buffer**
- **M1-28 Settings page: AI mode (Offline only for M1), provider read-only, debug-log toggle**
- **M1-29 Security page: shows "Gemini key: not configured" (no secret values displayed ever)**
- **M1-30 Confirmation dialog component**

## Docs / Examples

- **M1-31 `examples/commands/M1-demo.md` — step-by-step demo script**
- **M1-32 First screenshots → README**
- **M1-33 Tag `v0.1.0-m1` on milestone completion**
