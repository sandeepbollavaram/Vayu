# Milestone 2 — Offline AI demo

This is the manual walkthrough for the M2 offline-AI loop: **detect → list → pull → plan**, with the permission pipeline intact throughout. Everything here is local; no cloud is contacted at any step.

## Prerequisites

- Windows 10/11, the Vayu desktop app built in Debug.
- [Ollama](https://ollama.com) installed and running. Vayu does **not** install Ollama for you.
- Internet access for the one-time model download in step 4 (the model then runs fully offline).

## Walkthrough

### 1. Start Ollama

Launch Ollama (or run `ollama serve`). Confirm the server answers:

```powershell
curl http://localhost:11434/api/tags
```

### 2. Open Vayu and verify detection

1. Launch `Vayu.Desktop`.
2. Go to **Settings → Offline AI · Ollama** and click **Refresh**.
3. Expect: **Executable on PATH: Detected**, **Server reachable: Reachable**, endpoint `http://localhost:11434`.
4. The curated catalog lists Gemma 3 4B / Gemma 3 1B / Llama 3.2 3B with **Missing** chips if none are installed yet.

### 3. Confirm the planner toggle is gated

Still on **Settings → AI Mode**:

- With no curated model installed, **Offline AI planner (Ollama)** is **disabled**, and the hint reads *"Download Gemma 3 4B (or another curated model) first…"*.

### Model row UI states (M2.9)

Each catalog row is in exactly one state — the "Missing" chip, a live download, and an "Installed" model can never appear at once:

| State | Status chip | Download button | Progress + Cancel | Metadata chips |
| --- | --- | --- | --- | --- |
| **MissingIdle** | Missing | shown, enabled (if Ollama reachable) | hidden | hidden |
| **Downloading** | Downloading | hidden | progress bar + per-row Cancel + status line | hidden |
| **Cancelled** | Cancelled | shown, enabled (retry) | hidden | hidden |
| **Failed** | Failed | shown, enabled (retry) | hidden | hidden |
| **Installed** | Installed | hidden | hidden | size · params · family |

During a download the status line walks Ollama's phases: `Starting…` → `pulling manifest` → `downloading · 14%` (with a determinate progress bar) → `verifying sha256 digest` → `success`. The bar is indeterminate during manifest/verify phases (no byte totals) and determinate while downloading.

### 4. Download a model with explicit consent

1. Open **Setup** (nav rail) or **Settings → Open First Run Setup**.
2. Step through to **Model catalog** (step 4).
3. Click **Download** on **Gemma 3 · 1B** (smallest, fastest to fetch).
4. A consent dialog appears: *"Download gemma3:1b with Ollama?"* naming `http://localhost:11434/api/pull`.
5. Click **Cancel** once to confirm nothing happens. Then click **Download** again and **Download** in the dialog.
6. Watch the row stream progress: the chip turns **Downloading**, a cyan progress bar appears, the status line shows `downloading · NN%`, and a per-row **Cancel** button is available. The Download button is hidden — no duplicate downloads, no stale Missing chip.
7. When it finishes, the row flips to **Installed**: the chip reads Installed, the Download button is gone, and size · parameter · family chips appear.

To see the cancel path: start a download, click the row's **Cancel** — the chip shows **Cancelled**, the status line reads *"Cancelled. You can retry."*, and the Download button comes back enabled.

### 5. Enable the offline planner

1. Return to **Settings → AI Mode** and click **Refresh** on the Offline AI card if needed.
2. The toggle is now **enabled**, hint reads *"Offline planner is ready. Active model: gemma3:1b."*
3. Flip **Offline AI planner (Ollama)** **on**. Provider line shows *"Ollama local AI (gemma3:1b, rule-based fallback)"*.

### 6. Run commands (planned by the local model)

From the command surface:

- `open notepad` → Notepad opens (gated by the permission engine, written to the audit log).
- `open visual studio code` → VS Code opens.
- `show logs` → navigates to Logs.
- `show settings` → navigates to Settings.

### 7. Confirm typing is still blocked (M5)

- `open notepad and write hello` → Vayu opens Notepad but **refuses to type**, returning a NeedsClarification that points to M5. No keystrokes are sent.

### 8. Confirm safe fallback

- Stop Ollama (quit the app or `taskkill /im ollama.exe`).
- Back in **Settings → AI Mode**, click **Refresh**. The planner toggle **disables itself** and, if it was on, flips **off**; the hint reads *"Install and start Ollama first…"*.
- Run `open notepad` again → it still works via the **rule-based parser** fallback. No crash, no hang.

## What this proves

- Detection, listing, consented pull, and local planning all work offline.
- The local model only **proposes** plans; `AgentRuntime → PermissionService → audit log` still gates every action.
- The planner is opt-in and self-disables when its runtime disappears.
- No cloud calls, no telemetry, no silent installs or downloads.

## Out of scope for M2

- Cloud providers (Gemini and the wider registry) — **M3**.
- Voice — **M4**.
- Typing/clicking inside apps — **M5**.
