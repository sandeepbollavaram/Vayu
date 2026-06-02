# Desktop Automation

> **Status: M5.2 — first real safe-typing slice (Notepad).** The safety
> foundation from M5.1 is now wired end-to-end: `open notepad and write hello`
> opens Notepad and, **only after you approve**, types the exact approved text
> via Windows UI Automation (`ValuePattern.SetValue` — never global SendKeys).
> Secret-looking and shell-looking text is **blocked before any prompt**; a
> hidden/unknown window is never typed into; Cancel types nothing; every run is
> audit-logged. Clicking, reading visible text, and screenshots remain planned
> (not yet executable).

### M5.2 — how "open notepad and write hello" works

1. The parser produces the **`desktop.open_and_type`** intent (risk **L3**) with
   `app=notepad` and `text=hello` (original casing preserved).
2. It flows through `AgentRuntime → IPermissionService` (L3 → a real **Allow /
   Cancel** permission dialog) → `DesktopAutomationAgent`.
3. The agent evaluates the typing plan with `AutomationSafetyPolicy` **first** —
   secret/shell text is rejected here, before opening the app or prompting.
4. It opens Notepad (existing safe launcher) and polls `IWindowDiscoveryService`
   for the **visible** Notepad window.
5. It shows the **automation confirmation dialog** (`WinUiAutomationConfirmationService`):
   action = Type text, target app/window, **exact text preview**, risk, warning,
   **Approve once / Cancel** (dismiss = Cancel).
6. On **Cancel** → nothing is typed. On **Approve once** →
   `WindowsTextTypingExecutor` finds the editable control via UI Automation and
   sets its value to the approved text.
7. The runtime writes a redacted audit row; the Home result panel shows the
   outcome.

Two independent safety layers apply — the permission engine **and** the
automation confirmation — and neither is bypassed.

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

## Shipped in M5.2

- ✅ `WinUiAutomationConfirmationService` — the WinUI confirmation dialog
  (target / action / **exact text preview** / risk / warning; **Approve once** /
  **Cancel**, dismiss = Cancel).
- ✅ A real **Allow / Cancel** permission dialog (`WinUiConfirmationPrompt`) for
  L3+ at the permission layer.
- ✅ `WindowsTextTypingExecutor` — safe typing via UI Automation
  `ValuePattern.SetValue` (official `Interop.UIAutomationClient`), fail-safe when
  no editable control is found; **never** global `SendKeys`.
- ✅ `DesktopAutomationAgent` (`desktop.open_and_type`, L3) wired into the runtime.

## Still to come

- Click executor (`ClickElement`).
- Read-visible-text executor (`ReadVisibleText`).
- Screenshot capture, permission-gated.
- These remain **planned** — no clicking, reading, or screenshots execute yet.

## What M5.2 does NOT do

No browser login automation, no password/credential entry, no payment/checkout
automation, no hidden-window automation, no screenshot capture, no arbitrary app
scripting, and no arbitrary shell execution. Typing is the only executable
action, and only into a visible, approved window after explicit approval.
