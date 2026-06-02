# Desktop Automation

> **Status: M5.1 — safety foundation (no real typing/clicking yet).** M5 gives
> Vayu the ability to do more than launch apps — focus windows, read visible
> text, type, click, and (with permission) screenshot. M5.1 lays the **safety
> contracts** for that capability. Nothing in M5.1 types, clicks, or captures a
> screen; it defines *what Vayu may plan*, *how risky each action is*, and *what
> the user must approve* before anything runs. The executor + confirmation UI
> arrive in **M5.2**.

## The safety model

Every automation action is **permission-gated, audit-logged, cancelable, and
user-approved**. Vayu never:

- types or clicks **silently** — a higher-risk action shows exactly what it will
  do and waits for approval;
- types **secrets** — passwords, API keys, tokens, and credentials are rejected
  before any typing is even planned;
- types **shell commands** — command-runner text (`cmd`, `powershell`, `bash`,
  `|`, `&&`, `-Command`, …) is rejected; Vayu never runs arbitrary shell;
- acts on a **hidden or unknown window** — only **visible, identified** windows
  are valid targets;
- captures a **screenshot** without explicit permission;
- **bypasses** `AgentRuntime` / the permission engine / the audit log.

## Action model

| Type | Risk | Confirmation | Notes |
| --- | --- | --- | --- |
| `OpenApp` | L1 | no | The existing M1 capability. |
| `FocusWindow` | L2 | no | Bring a visible window to the foreground. |
| `ReadVisibleText` | L2 | no | Read visible text only — never hidden content. |
| `TypeText` | L3 | **yes** | Approve the exact text first. Secrets/shell rejected. |
| `ClickElement` | L3 | **yes** | Click a described element in a visible app. |
| `ScreenshotVisibleWindow` | L3 | **yes** | Explicit permission required. |
| `Wait` | L0 | no | Bounded pause between steps. |
| `Unknown` | L6 | — | Never executable. |

The risk levels map onto the existing `Vayu.Core.RiskLevel` engine (L3+ requires
confirmation; L6 is disabled).

## Contracts (M5.1)

- **`AutomationActionType`** — the action kinds above.
- **`AutomationTarget`** — `AppName` / `WindowTitle` / `ProcessId?` /
  `ElementDescription?`; `IsResolved` is the minimum bar for a real target.
- **`AutomationActionPlan`** — a *planned* step: action + target + risk +
  `RequiresConfirmation` + reason + a safe `TextPreview` + correlation id.
- **`AutomationActionResult`** / `AutomationStatus` — `Success` /
  `NeedsConfirmation` / `Rejected` / `Cancelled` / `Failed`.
- **`AutomationSafetyPolicy`** — `Classify` / `RequiresConfirmation` /
  `Evaluate` / `RejectIfUnsafe`. Rejects secret text, shell text, unknown
  actions, and hidden/unknown targets. Pure; no execution.
- **`IWindowDiscoveryService`** / `WindowsWindowDiscoveryService` — enumerates
  **visible top-level windows only** (title, process, bounds). No hidden
  windows, no content reading, no screenshots.
- **`IAutomationConfirmationService`** / `AutomationConfirmationRequest` /
  `AutomationConfirmationDecision` (`ApproveOnce` / `Cancel`). The default
  implementation **denies** every request (fail-closed) until the M5.2 dialog
  ships — so nothing can run without an explicit approval path.
- **`CommandToAutomationPlanner`** — turns "open notepad and write hello" into an
  `OpenApp` step plus a **planned** `TypeText` step (confirmation-gated, secret/
  shell-rejected). It plans only; M5.1 never types.

## "open notepad and write hello"

This compound command is the canonical M5 example. Today:

- the **open** part runs through the existing app launcher (L1);
- the **type** part is *planned* (`TypeText`, L3, confirmation required) but
  **never executed** in M5.1 — exactly as the rule-based parser already refuses;
- it will be **completed only after** the M5.2 confirmation dialog + a safe
  typing executor, and only on explicit approval, with an audit row.

## Coming in M5.2

- A WinUI confirmation dialog implementing `IAutomationConfirmationService`
  (shows target / action / text preview / risk / warning; **Approve once** /
  **Cancel**).
- A safe typing executor that focuses the approved window and types the approved
  text via the UI Automation pattern (never global `SendKeys`), audited and
  cancelable.
- Click + read-visible-text executors.
- Screenshot capture, permission-gated.
