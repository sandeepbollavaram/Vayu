# Milestone 3 — Online Provider demo

Manual walkthrough for the M3 online-provider loop: **secure key setup → cloud consent → Online/Hybrid planning → provider registry**, with the permission/audit pipeline intact throughout. Gemini is the only active online connector; every other provider is a planned shell.

## Prerequisites

- Windows 10/11, the Vayu desktop app built in Debug.
- A Google **Gemini API key** (from [Google AI Studio](https://aistudio.google.com/)).
- Optional: [Ollama](https://ollama.com) running with a curated model, if you want to try **Hybrid** mode.

> **Never paste a real API key into any file, screenshot, log, or commit.** Vayu stores it in Windows Credential Manager and never shows it again.

## 1 — Save the Gemini key (M3.3)

1. Launch `Vayu.Desktop` → **Settings → Online AI · Gemini** (status shows **NOT CONFIGURED**).
2. Paste your key into the masked box → **Save key**.
3. The textbox **clears immediately**; the message reads "Saved securely. Your key is never shown after saving."
4. Status flips to **CONFIGURED · WINDOWS CREDENTIAL MANAGER**; **Remove key** enables.

## 2 — Consented Test key (M3.4)

With a key saved, **Test key** is enabled. Click it:

- **Cancel / dismiss** the consent dialog → "Test cancelled." — **no cloud call**.
- **Use local instead** → "Skipped cloud test; local AI remains available." — **no cloud call**.
- **Allow once** → "Testing…" then "Test succeeded." (valid key) or a safe failure (invalid → "rejected", rate-limited → "rate limited", server → "server error"). This is a single minimal health-check; no prompt body or key is logged.

## 3 — Pick an AI mode (M3.5)

**Settings → AI Mode** is a 4-option selector, each gated by readiness:

- **Rule-based** — always available (the safe default).
- **Offline AI** — enabled when Ollama is reachable + a curated model is installed.
- **Online Gemini** — enabled when a Gemini key is configured.
- **Hybrid** — enabled when both are ready.

Pick **Online Gemini** (or **Hybrid** if Ollama is set up). The provider line reflects the choice.

## 4 — Plan commands through Gemini (consent-gated)

From the command box, run:

- `open notepad`
- `show logs`
- `show settings`

For each, before any Gemini call, the **consent dialog** appears (provider, purpose "Plan your command", estimated prompt size, sensitive context: No):

- **Cancel / dismiss** → no cloud call; the **rule-based parser** handles the command (`PlanSource = cloud-consent-cancelled-rule-based`).
- **Use local instead** → no cloud call; local model (if available) or rule-based handles it.
- **Allow once** → Gemini produces an `IntentPlan`; it then flows through `AgentRuntime → PermissionService → agent → audit log` exactly like any other plan.

In **Hybrid** mode, a confident local plan wins with **no consent prompt at all**; consent is asked only when the local model fails or is unsure, and **at most once per command**.

## 5 — Typing is still blocked (M5)

- `open notepad and write hello` → Notepad opens but Vayu **refuses to type**, returning a NeedsClarification that points to M5. This holds whether the plan came from Gemini, the local model, or the rule-based parser — the `typing_requested` flag is preserved and the AppLauncherAgent defers it.

## 6 — Provider registry (M3.6 / M3.7)

**Settings → Online Provider Registry** lists all 13 catalog providers:

- **Gemini** — **Available now** (+ configured / not configured).
- **OpenAI · Claude · DeepSeek · Kimi · OpenRouter · Custom OpenAI-compatible** — **Planned · connector shell present**.
- The remaining providers — **Planned**.

None of the planned cards allow setup or any API call.

## 7 — Confirm the safety guarantees

- **Logs page** → no API key, no prompt body anywhere.
- **Security page** → Gemini key status only (Configured / source), never the value.
- Stop using cloud at any time: switch AI Mode back to **Rule-based**, or **Remove key** — Online/Hybrid disable and Vayu falls back cleanly.

## What this proves

- Secure key handling (Credential Manager, cleared on save, never shown/logged).
- Per-call cloud consent with safe Cancel / Use-local paths.
- Online and Hybrid planning that still passes every plan through the permission/audit pipeline.
- A multi-provider registry that's honest about what works now (Gemini) versus what's planned.
- The M1 invariant holds end to end: **AI only proposes plans; the runtime gates execution.**

## Out of scope for M3

- Real OpenAI / Claude / DeepSeek / Kimi cloud calls — connector **shells** only (future milestones).
- Voice — **M4**.
- Typing / clicking inside apps — **M5**.
