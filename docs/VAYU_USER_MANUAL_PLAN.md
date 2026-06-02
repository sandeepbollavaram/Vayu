# Vayu User Manual — Documentation Plan

> **Status: planning only.** This file plans the future official end-user manual, *"What is Vayu and how to use Vayu on your desktop."* It is a documentation outline, not the manual itself — the real manual is written as features ship (largely around M7 production release and again at each version's feature-complete point).

## Purpose

Give a non-technical Windows user everything they need to install Vayu, set it up safely, and use it day to day — across both Vayu Version 1 (M1–M11) and Version 2 (M12–M20) capabilities.

## Planned table of contents

### 1. Understanding Vayu
- What Vayu is (a local-first Windows AI desktop assistant & tool-system).
- Why Vayu exists (privacy, control, one place to drive local + cloud AI safely).
- The local-first desktop-assistant concept.
- The safety model: permissions, audit logs, stop/cancel, consent.

### 2. Installing & first-run setup
- Installing Vayu (MSIX / `.exe` installer).
- First launch opens the **Setup Dashboard** automatically (and remembers, across restarts, once you finish or skip it — re-open any time from Settings → Open Setup Dashboard).
- Choosing paths during setup (set a **Vayu storage root**; Vayu derives and creates workspace/logs/cache/models/assets under it, only after you Save):
  - storage / data root
  - Vayu **workspace** path (where generated assets, websites, renders, and projects are saved by default)
  - logs path
  - cache path
  - local model files path
  - generated assets path
- Choosing an **AI mode**: Offline / Online / Hybrid.
- Setting up **Ollama / Gemma** (offline).
- Setting up **online providers** (Gemini first; others from the registry).
- Storing keys in **Windows Credential Manager** — and why you should never keep plaintext API keys.
- Skipping any provider; every model install/download requires explicit permission.

### 3. Everyday use (Version 1)
- Using the command box.
- Using voice (push-to-talk, listening/thinking/speaking states).
- **Setting up local voice (working as of M4.11)**: in Settings → Voice Setup, download a verified Whisper model (base.en recommended) or point at one you have; confirm **Local STT: Ready**; optionally **Test local STT**; then push-to-talk on Home transcribes locally. Turn on **Enable voice commands** to have a valid transcript run through the normal permission/audit pipeline. All on-device — no cloud STT, no always-listening, audio never logged.
- **Voice triggers — wake word & clap (future, opt-in)**:
  - How to **enable/disable the wake word** ("Hey Vayu") — off by default; how
    to turn it on, choose the phrase, and turn it back off.
  - How to **enable/disable the clap trigger** (double-clap) — off by default;
    why it's best in quiet rooms.
  - **Privacy expectations**: detection runs locally; no cloud wake detection by
    default; no audio is ever saved or logged; a visible indicator shows whenever
    the mic is armed/listening; a trigger only *starts listening* — it never runs
    a command by itself, and every command still passes permissions + audit.
  - **How to stop listening**: the always-visible stop/disable control, how to
    cancel an in-progress listen, and how to clear any retained voice metadata.
  - *(Push-to-talk remains the default and needs none of the above.)*
- Opening apps.
- Using safe desktop automation (typing/clicking with confirmation).
- Using workflows.
- Using Mission Control (coordinating coding agents with approval).
- Using the coding autopilot.
- Creating websites / apps with Vayu.

### 4. Advanced use (Version 2)
- Using creative / image generation features.
- Using 3D asset generation.
- Using Blender / Unity / rendering integrations.
- Using the visual (n8n-style) workflow builder.
- Using the background activity dashboard ("what Vayu is doing now").

### 5. Using Vayu safely
- Permissions and risk levels (L0–L6).
- Reading the audit log.
- How to **stop / cancel** any action.
- Consent dialogs for cloud calls and downloads.
- Privacy / enterprise mode.

### 6. Troubleshooting
- Ollama not detected / not running.
- Model download issues.
- Provider key not accepted.
- Permission/consent prompts.
- Logs and where to find them.

### 7. Production / server / VPS usage *(future)*
- When and how Vayu can use **approved** server/VPS-assisted agent workflows.
- The rule: every remote/server action must be **explicit, secure, logged, and user-approved**.
- This is a future capability — not implemented yet.

## Cross-cutting documentation rules

- **Generated outputs default to the Vayu workspace.** Generated assets, websites, renders, and project files are saved under the user-selected Vayu workspace/storage root unless the user picks another path. The manual must make the active workspace path visible and easy to change.
- **Never show secrets.** The manual must teach Credential-Manager storage and explicitly warn against plaintext keys; no screenshot or example may contain a real key.
- **Safety first.** Every "how to use feature X" section pairs with "how to stop X" and "what gets logged."

## When this becomes the real manual

- A first user-facing pass lands around **M7** (production release) covering Version 1 basics.
- It expands at **M11** (Version 1 feature-complete) and again at **M20** (Version 2 feature-complete).
