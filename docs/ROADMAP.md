# Roadmap

Each milestone is a *shippable* state: CI green, docs current, demo-able.

## Product strategy: two major versions

Vayu ships as **two major product versions**, not one major version per milestone:

- **Vayu Version 1 = M1 – M11** — a production-ready, local-first Windows AI desktop assistant and tool-system foundation: desktop foundation, offline AI, online providers, voice, desktop automation, workflows + memory, production release, intelligence, mission control, autonomous workbench, and the coding-autopilot / website-app-builder foundation.
- **Vayu Version 2 = M12 – M20** — an advanced creative, 3D, agentic-workflow, rendering, advanced-provider, and OS-like desktop-companion ecosystem: creative AI, 3D asset generation, Blender, Unity, rendering pipelines, NVIDIA/advanced providers, n8n-style visual workflow builder, background agent runtime + live activity dashboard, and the Vayu OS / Desktop Companion layer.

**Development tags** are still per-milestone (one shippable state each):

| Dev tag       | Milestone                          |
| ------------- | ---------------------------------- |
| `v0.1.0-m1`   | M1 Desktop Foundation              |
| `v0.2.0-m2`   | M2 Offline AI Layer                |
| `v0.3.0-m3`   | M3 Online Provider Layer           |
| `v0.4.0-m4`   | M4 Voice Interaction               |
| `v0.5.0-m5`   | M5 Advanced Desktop Automation     |
| `v0.6.0-m6`   | M6 Workflows and Local Memory      |
| …             | one `v0.x.0-mN` tag per milestone  |

**Production targets:**

- `v1.0.0` — the public production release, cut after **M7 / M8** readiness depending on the quality gate (not automatically at M7).
- **Version 1 feature-complete** target — after **M11**.
- **Version 2 feature-complete** target — after **M20**.

> **Aggressive execution target (not a delivery commitment).** The author is currently targeting completion of the full **M1–M20** system in roughly **3 months**, working about **15 hours/day**, using Opus / Claude Code for implementation, ChatGPT for prompting, Kairo for continuity, and manual testing. This is a stretch goal recorded for planning — schedule and scope may change, and quality/safety gates take precedence over the timeline.

## Milestone status

| Milestone                                            | Version | Status         |
| ---------------------------------------------------- | ------- | -------------- |
| **M1** — Desktop Foundation                          | V1      | ✅ Released (`v0.1.0-m1`) |
| **M2** — Offline AI Layer                            | V1      | ✅ Released (`v0.2.0-m2`) |
| **M3** — Online Provider Layer                       | V1      | 🛠️ In progress |
| **M4** — Voice Interaction                           | V1      | ⏳ Planned     |
| **M5** — Advanced Desktop Automation                 | V1      | ⏳ Planned     |
| **M6** — Workflows and Local Memory                  | V1      | ⏳ Planned     |
| **M7** — Production Release                           | V1      | ⏳ Planned     |
| **M8** — Vayu Intelligence Layer                     | V1      | ⏳ Planned     |
| **M9** — Vayu Mission Control                        | V1      | ⏳ Planned     |
| **M10** — Autonomous Workbench                       | V1      | ⏳ Planned     |
| **M11** — Coding Autopilot & Website/App Builder     | V1      | ⏳ Planned     |
| **M12** — Creative AI Generation Layer               | V2      | ⏳ Planned     |
| **M13** — 3D Asset Generation Layer                  | V2      | ⏳ Planned     |
| **M14** — Blender Integration Layer                  | V2      | ⏳ Planned     |
| **M15** — Unity Integration Layer                    | V2      | ⏳ Planned     |
| **M16** — Rendering and Asset Pipeline Layer         | V2      | ⏳ Planned     |
| **M17** — NVIDIA and Advanced Provider Ecosystem     | V2      | ⏳ Planned     |
| **M18** — Visual Workflow Builder (n8n-style)        | V2      | ⏳ Planned     |
| **M19** — Background Agent Runtime + Live Activity    | V2      | ⏳ Planned     |
| **M20** — Vayu OS / Desktop Companion Ecosystem      | V2      | ⏳ Planned     |

## Multi-milestone product features

### First Run Setup Wizard

A core product feature of Vayu: on first launch, a wizard guides the user to a working local AI (Ollama + Gemma) and optionally an online provider key. See [FIRST_RUN_SETUP.md](FIRST_RUN_SETUP.md).

- **M1** ✅ Architecture doc + minimal contracts (`IFirstRunSetupService`, `FirstRunSetupMode`, `FirstRunSetupState`).
- **M2.5** UI pages (Mode → Ollama check → Gemma model picker → pull-with-consent → verify).
- **M3** Online provider key onboarding (Gemini first; the registry's other providers follow).
- **M7** Optional bundled installer integration.

### AI Provider Registry

Vayu is not Gemini-only. The wizard offers a catalog of providers spanning offline (Ollama, llama.cpp, LM Studio, LocalAI), online-direct (Gemini, OpenAI, Anthropic, DeepSeek, Kimi/Moonshot, Mistral, Groq, Cohere, Perplexity, xAI, Together, Fireworks, Cerebras, Hugging Face, Replicate), cloud platforms (Azure OpenAI, AWS Bedrock, Vertex AI), and routers (OpenRouter, LiteLLM, custom OpenAI-compatible). See [AI_PROVIDER_REGISTRY.md](AI_PROVIDER_REGISTRY.md).

- **M1** ✅ Registry doc + contracts.
- **M3** First concrete connector (Gemini).
- **M4+** Incremental connectors — one PR per provider, each carrying redaction patterns and "must not log key" tests.

---

## ✅ M1 — Desktop Foundation (v0.1.0, released)

**Goal**: type `open vscode`, see the planner pick it up, watch the app open, and see two audit rows.

Shipped:

- WinUI 3 dashboard with bounded Vayu Sphere (orb + wind-V + dotted halo).
- `Vayu.Core` — `RiskLevel`, `PermissionDecision`, `CommandRequest`, `IntentPlan`, `CommandResult`, `ICommandHandler`, `IAgent`, `IAgentRuntime`, `IClock`.
- `Vayu.Security` — `ISecretStore` + three implementations + `SecretRedactor` (11 patterns) + `SecureConfigService`.
- `Vayu.Logging` — Serilog + redacting enricher + bounded in-memory ring buffer for the Logs page.
- `Vayu.Memory` — SQLite audit log (`Actions` table).
- `Vayu.Permissions` — L0–L6 policy + `IConfirmationPrompt` + `DefaultPermissionService`.
- `Vayu.AgentRuntime` — token-aware rule-based parser + permission/audit-aware dispatcher.
- `Vayu.Automation.Windows` — `KnownAppCatalog` + `InstalledAppCatalog` (Desktop + Start Menu `.lnk`/`.url` discovery with subsequence matching) + `AppLauncherAgent`.
- "open notepad and write hello" returns a NeedsClarification pointing to M5 — Vayu never silently types in M1.

**Exit criteria** — met: M1 demo runs end to end, CI + Security + Release green, repo tagged `v0.1.0-m1`.

---

## ✅ M2 — Offline AI Layer (v0.2.0-m2, released)

**Goal**: make Vayu plan and respond intelligently without internet using a local model (Ollama + Gemma by default).

Subdivisions:

| Sub  | Scope                                                                                              |
| ---- | -------------------------------------------------------------------------------------------------- |
| M2.1 | **Offline AI architecture** — contracts: `LocalAiOptions`, `LocalModelCatalog`, `LocalModelDescriptor`, `LocalAiProviderStatus`, `ILocalAiProvider`, `ILocalIntentPlanner`, `LocalAiPlanningResult`. No HTTP, no Ollama install, no model downloads. |
| M2.2 | Ollama runtime detection — `OllamaRuntimeService` real impl (winget probe, PATH probe, `GET /api/tags` ping). |
| M2.3 | Local model catalog wiring — Settings → "Offline AI · Ollama" card shows endpoint, executable/server detection, recommended model, and the curated catalog with Installed/Missing badges; Refresh re-runs detection (no install, no pull). |
| M2.4 | Installed model listing refinement — `OllamaModelInfo` parses size + `details.family` / `parameter_size` / `quantization_level`; Settings rows show GB/parameter/family; curated-vs-unknown summary; matching stays exact-tag. |
| M2.5 | First Run Setup Wizard UI — five-step page (Welcome → Mode → Ollama → Models → Verification) reachable from the nav rail and Settings. Read-only; reuses the M2.3/M2.4 detection view model. Wizard state lives in `InMemoryFirstRunSetupService`; install/pull stay deferred to M2.6. |
| M2.6 | Safe model pull flow — `IOllamaModelPullService` streams `POST /api/pull` for curated tags only; consent `ContentDialog` names the exact endpoint and lets the user cancel; per-row progress; `IsInstalled` only flips after a fresh `/api/tags` confirmation. No Ollama runtime install — that's a future task. |
| M2.7 | Local AI planner — `OllamaIntentPlanner` (strict-JSON, allowlisted intents, Vayu-assigned risk) + `AiRouterIntentPlanner` (offline plan above confidence floor → rule-based fallback). Opt-in Settings toggle, default OFF. Plans still flow through AgentRuntime → PermissionService → audit. Online/Hybrid routing stays M3; typing/clicking stays M5. |
| M2.8 | M2 polish — readiness-gated planner toggle (`LocalAiReadiness`), active-model selection over installed curated models (no auto-pull), README + LOCAL_AI/FIRST_RUN_SETUP sweep, `docs/M2_DEMO.md`. |
| M2.9 | M2 UI release-polish — single-state model-row state machine (`ModelRowState`), per-row progress bar + cancel, clean installed state, full glitch sweep. Tag `v0.2.0-m2` after user-verified demo. |

Hard rules carried from M1:

- No silent Ollama install. No silent model download. Every step is skippable.
- All AI-produced plans still flow through `AgentRuntime` and `IPermissionService`. The local AI never invokes a tool directly.
- No cloud dependency at any stage of M2.

---

## M3 — Online Provider Layer (v0.3.0)

**Goal**: add optional online AI providers safely, with Gemini as the first concrete connector.

Providers (in scope; ship incrementally over M3 and M4+):

- Gemini · OpenAI · Anthropic Claude · DeepSeek · Kimi / Moonshot · Mistral · Groq · Cohere · Perplexity · xAI Grok · OpenRouter · LiteLLM-compatible endpoint · Custom OpenAI-compatible endpoint.

Features:

- Provider Registry UI (Settings → "Add an online provider" → catalog picker).
- Secure API key setup via Windows Credential Manager (`Vayu:<ProviderId>:ApiKey`); environment-variable and DPAPI-encrypted fallbacks per `docs/SECURITY.md`.
- "Test this key" button — redacted round-trip, no body logged.
- First-call consent dialog before any private context leaves the device.
- Keys are never displayed after save; never logged; never sent to telemetry.
- Gemini is the first concrete connector; the rest follow the same shape.

| Sub  | Scope                                                                                              |
| ---- | -------------------------------------------------------------------------------------------------- |
| M3.1 | ✅ **Online provider architecture** — contracts only in `Vayu.AI.Online`: `OnlineAiOptions`, `OnlineProviderDescriptor`/`Kind`, `OnlineProviderCatalog` (13 providers), `OnlineProviderKeySource`/`KeyStatus` (no key value), `CloudConsentRequest`/`Decision`/`ICloudConsentService`, `IOnlineAiProvider`, `OnlineAiPlanningResult`, `OnlineAiSafetyPolicy`. No cloud HTTP, no key storage. Cloud disabled by default; consent required. |
| M3.2 | ✅ Gemini provider connector — `GeminiProvider` implements `IOnlineAiProvider`: key resolved via `SecureConfigService` (value internal-only, never surfaced), `PlanAsync` makes no HTTP call unless consent is `AllowOnce` and a key is configured, JSON-mode planning, output allowlist/risk-validated through `OnlineAiSafetyPolicy`. Tested with a fake `HttpMessageHandler` — no real key, no real network. Still disabled in the UI until key-setup (M3.3) + consent dialog (M3.4). |
| M3.3 | ✅ Secure provider key setup UI — Settings "Online AI · Gemini" card with `PasswordBox` + Save/Remove. `GeminiKeySetupService` saves to Windows Credential Manager (DPAPI-encrypted-config fallback), reports value-free status (Configured/Not configured + source), clears the textbox on save, and never shows/logs the key. Env-var keys show as configured-via-environment but can't be removed from Vayu. Test button is disabled ("Test key (M3.4)") — validation is a cloud call needing the consent dialog. |
| M3.4 | ✅ Cloud consent dialog — `DesktopCloudConsentService` (WinUI `ContentDialog`) implements `ICloudConsentService`: shows provider / purpose / data summary / estimated prompt size / sensitive-context flag + the "no secrets, logged without prompt body" note; **Allow once / Use local instead / Cancel**, dismiss = Cancel. Enables the Settings **Test key** button (only when configured) → a minimal redacted `GeminiProvider.TestKeyAsync` round-trip that fires **only** on Allow once. No HTTP on Cancel/Use-local/no-key; 401/403/429/5xx/malformed all fail safely; key and prompt body never logged. Consent is not persisted. |
| M3.5 | ✅ Online AI planner + router integration — `AiRouterIntentPlanner` gains `PlanningMode` (RuleBased / Offline / Online / Hybrid). **Online**: ask `ICloudConsentService`; `AllowOnce` → `GeminiProvider.PlanAsync`, `UseLocalInstead` → local/rule, `Cancel`/dismiss → rule fallback. **Hybrid**: local first; on failure/low-confidence ask consent once → Gemini → rule fallback. Consent asked at most once per command; cloud plans allowlist/risk-validated; the router never throws → always a rule-based fallback. Settings AI Mode is a 4-option selector gated by readiness (Offline needs Ollama+model, Online needs a Gemini key, Hybrid needs both). Every plan still flows through AgentRuntime → PermissionService → audit. |
| M3.6 | ✅ Provider Registry UI polish — Settings "Online Provider Registry" card renders all 13 `OnlineProviderCatalog` entries via the pure `ProviderRegistryViewModel`/`ProviderCardViewModel`: display name, id, kind (Direct API / Router / Cloud Platform / Custom OpenAI-compatible), capability chips (Chat / JSON mode / Streaming), and status. **Gemini** = "Available now" + configured/not-configured (managed in the dedicated key card); the other 12 = "Planned · Coming in M3.7+" with disabled actions. No non-Gemini cloud calls, no key fields on any card. |
| M3.7 | ✅ Provider connector shells — `PlannedOnlineProvider` (one reusable `IOnlineAiProvider` shell) + `PlannedOnlineProviders` factory build safe no-op connectors for OpenAI / Claude / DeepSeek / Kimi / OpenRouter / Custom OpenAI-compatible (and every other non-Gemini catalog entry). Each shell `PlanAsync` returns a redaction-safe "planned, not implemented yet" failure — **no HTTP, no key read, no consent dependency**. The registry marks shell providers "Planned · connector shell present". Gemini stays the only working connector for the M3 release. |
| M3.8 | 🛠️ M3 release polish — final docs/README sweep, `docs/M3_DEMO.md`, provider-registry wording. Tag `v0.3.0-m3` after a user-verified demo of the key-setup → consent → Online/Hybrid planning → registry flow. |

Hard rules carried from M1/M2:

- **Cloud AI is off by default.** Online providers are opt-in; the user must explicitly configure one.
- **Consent before private context leaves the device** (`OnlineAiOptions.RequireConsentBeforeCloudCall` defaults true).
- API keys are never committed, never logged, never shown after saving — `OnlineProviderKeyStatus` carries only the source, never the value.
- **Online AI produces `IntentPlan`s only.** Cloud planners never execute tools; every plan still flows through `AgentRuntime → IPermissionService → agent → audit log`, and `OnlineAiSafetyPolicy` caps cloud plans at the allowlist + risk ≤ L1 in M3.
- Gemini is **one** optional provider (implemented first), not the only one.

---

## M4 — Voice Interaction Layer (v0.4.0)

**Goal**: make Vayu usable by voice.

Features:

- Push-to-talk (global hotkey, default `Ctrl+Win+Space`).
- Speech-to-text interface (Whisper.cpp first; Azure Speech as optional cloud STT under consent).
- Text-to-speech interface (System TTS first; Piper optional).
- Mic permission UI — Windows privacy prompt only fires on the user's first PTT.
- Voice states surfaced on the Vayu Sphere (idle / listening / thinking / speaking).
- Wake word ("Hey Vayu") planning + clap trigger planning behind toggles.

| Sub  | Scope                                                                                              |
| ---- | -------------------------------------------------------------------------------------------------- |
| M4.1 | ✅ **Voice architecture / contracts** — `Vayu.Voice`: `VoiceInteractionState`, `VoiceInputMode`, `VoiceSession`, `VoiceRecognitionResult`, `SpeechSynthesisRequest`/`Result`, `MicrophoneStatus`, `IVoiceInputService`, `ITextToSpeechService`, `IVoiceCommandService`, `VoiceEvent`, `IVoiceActivitySink`. Provider-neutral. No capture, no STT/TTS, no always-listening. See [VOICE_SYSTEM.md](VOICE_SYSTEM.md). |
| M4.2 | ✅ Push-to-talk UI foundation — Home **Voice** card (mic-status line, Push-to-talk + Stop/Cancel, current state) + Settings voice section; `StubVoiceInputService` drives the session state machine with **no microphone capture and no faked transcript** (returns "recognition arrives in M4.3"); `InMemoryVoiceActivitySink` buffers `VoiceEvent`s; `VayuSphere.SetVoiceState` maps voice state to the existing sphere animation. No STT, no always-listening, no command execution. |
| M4.3 | ✅ Local STT provider foundation — `LocalSpeechToTextOptions`, `SpeechToTextProviderStatus`, `ISpeechToTextProvider`, `WhisperCppSpeechToTextProvider` shell (detects model path; no native binary so CI stays clean; honest "shell ready / not configured", never a fake transcript). Home Voice card gains an STT-status line + transcript area; Settings shows local-STT status. Audio stays local/in-memory — no disk, no cloud, no audio logs. No command execution (that's M4.5). |
| M4.4 | ✅ TTS provider integration — `TextToSpeechOptions`/`Status`, `VoiceAssistantPhrases`, `SystemTextToSpeechService` (blank-reject + length cap + secret-guard; injectable engine so CI stays WinRT-free) + desktop `WinUiSpeechAdapter` (`Windows.Media.SpeechSynthesis`). Off by default, opt-in Settings toggle (`VoiceTtsState`), Speak/Stop on the Home Voice card. Short neutral phrases only — no user content, no secrets, no cloud TTS, no audio files. No command execution (that's M4.5). |
| M4.5 | ✅ Voice command pipeline into `AgentRuntime` — `VoiceCommandService` (`VoiceCommandOptions`/`VoiceCommandResult`) turns a successful, above-floor transcript into a `CommandRequest { Source = "voice/<provider>" }` and dispatches through the existing `IAgentRuntime` (no new execution path). Off by default (Home toggle); failed/empty/low-confidence/cancelled never dispatch; short secret-safe TTS phrase after dispatch. Inert until a real STT runtime is configured. Voice never bypasses the permission engine or the audit log. |
| M4.6 | ✅ Vayu Sphere voice-state animation — `VoiceStateVisualMapper` (pure `VoiceInteractionState` → `VoiceVisualToken`) + new Listening/Speaking storyboards and per-state accent colours in `VayuSphere.SetVoiceState`; colour-matched Home state chip. Visual-only — no audio amplitude, no capture/command changes; app-command Processing/Success/Error animations untouched; clean return-to-idle. |
| M4.7 | ✅ Wake word / clap trigger — **planning docs only** (design + safety/consent model in [VOICE_SYSTEM.md](VOICE_SYSTEM.md); user-facing notes in [VAYU_USER_MANUAL_PLAN.md](VAYU_USER_MANUAL_PLAN.md)). No engine, no clap detection, no always-listening, no background capture. |
| M4.R | 🛠️ **Product recovery + UX stabilization** (inserted before M4.8 after manual testing). Corrects product direction: first launch now routes to setup before the main shell (`FirstRunExperience`), Home becomes the **Vayu Command Center** (VAYU ACTIVITY / live-feed placeholder, Trust & Safety, AI Provider, Setup cards), app launch resolves **Store/MSIX apps via vetted URI schemes** (`open spotify` now works) through `KnownUriCatalog` with no arbitrary shell execution, and the Voice card states its honest state (STT not configured; wake/clap planned, never always-listening). No wake/clap impl, no installer, no VPS execution. |
| M4.9 | ✅ **Real local STT vertical slice** — push-to-talk now does real microphone capture (`IAudioCaptureService` + Windows `AudioGraph` adapter, in-memory PCM, capped, Stop-cancellable) and real local transcription via `DelegatingLocalSttProvider` (native engine injected as a delegate; honest not-configured, no fake transcript). Model path configured in Settings → Voice Setup. Transcript dispatch still requires Enable voice commands and flows transcript → `CommandRequest{Source=voice/<provider>}` → AI Router → Permission Guard → agent → audit. No cloud STT, no wake/clap, no always-listening. |
| M4.10 | ✅ **whisper.net engine** — `WhisperNetSpeechToTextEngine` (Desktop-only, `Whisper.net` + `Whisper.net.Runtime`) wired into the M4.9 delegate seam, with `PcmAudioConverter` (16-bit PCM → float). Vayu now transcribes speech **on-device** from a user-supplied model; native packages stay in the desktop app so `Vayu.Voice` and CI stay native-free. Safe failure mapping, no fake transcript, no cloud, no auto-download. |
| M4.12 | ✅ **Production first-run setup** — durable SQLite-backed setup state (`IUserSettingsStore`/`SqliteUserSettingsStore` + `PersistentFirstRunSetupService`) that survives restarts; first launch routes to the Setup Dashboard, completing/skipping routes to the Command Center thereafter, re-openable from Settings. Real storage step: editable Vayu root (default `%LOCALAPPDATA%\Vayu`), validated, folders created only on Save (non-destructive), persisted. **No API keys persisted** (only a configured-flag; keys stay in the secure store). Offline/Online/Voice steps connect to the real Ollama/Gemini/Whisper features. |
| M4.11 | ✅ **Production voice setup** — guided Whisper model download (`WhisperModelCatalog` verified HF sources + consent/progress/cancel/cleanup `WhisperModelDownloadService`), a genuine readiness probe (Local STT "Ready" only when whisper.net loads the model), a transcribe-only **Test local STT** flow, and full Voice Setup readiness states. Closes the voice story: model → Ready → push-to-talk → real transcript → opt-in dispatch through the permission/audit pipeline. No fake state, no cloud, no auto-download. |
| M4.R2 | 🛠️ **Product realignment** (continues M4.R). Strips remaining milestone/prototype wording from production UI (Settings/Setup/Home) and replaces it with product vocabulary (Setup Dashboard, AI Mode, Local/Online/Hybrid AI, Permission Guard, Provider Registry, Audit Log, Agent Queue). Setup reads as a **Setup Dashboard** with a 7-stage stepper (Welcome/Storage/AI Mode/Local AI/Online AI/Voice/Trust & Finish) and storage-path placeholders (default `%LOCALAPPDATA%\Vayu`). Home becomes a fuller **Control Center** (status strip + AGENT QUEUE / AI RUNTIME / TRUST GUARD / SETUP HEALTH cards). App discovery adds the **Windows App Paths registry** resolver (validated `.exe` only — never a command line) plus calculator/explorer/settings, so normal installs without shortcuts resolve. Voice section becomes **Voice Setup** (honest STT-not-configured; wake/clap planned, opt-in). No installer, no wake/clap, no VPS execution, no unsafe shell. |
| M4.8 | M4 polish + demo. |

Hard rules: voice is opt-in; no mic capture without user action; no always-listening; no audio uploaded to cloud without explicit consent; no raw audio in logs; a spoken command is a `CommandRequest` that still flows through `AgentRuntime → IPermissionService → audit` — voice never bypasses the permission engine.

> **Product-direction note (recorded at M4.R).** Vayu is a **full local-first AI desktop tool system / command center** — not a chatbot or a voice demo. The Home surface is the **Vayu Command Center**; its right-side cards are a **Live / Vayu Activity** feed (what Vayu and its agents are doing now). Setup is a **first-launch experience**, not just a nav tab. App launching must reliably open **installed apps** (e.g. Spotify) via the catalog, discovered shortcuts, and vetted URI schemes — never by executing arbitrary user text as a shell command. Future **approved VPS / server workflows** (Hermes-style remote agent control) are allowed **only** with explicit approval, secure execution, and audit logs — the same permission-gated model as every other action.

---

## M5 — Advanced Desktop Automation (v0.5.0)

**Goal**: safely control apps beyond opening them.

Features:

- Focused window detection.
- App switching.
- Visible UI element reading via Windows UI Automation.
- Safe clicking (L3 — confirmation required).
- Safe typing (L3 — confirmation required). This is where `"open notepad and write hello"` finally lands.
- Screenshot with explicit consent.
- Refuses to automate unapproved process names.
- Never elevates; never bypasses Windows permissions.

---

## M6 — Workflows and Local Memory (v0.6.0)

**Goal**: make Vayu remember useful local context and run repeatable workflows.

Features:

- Local long-term memory (separate SQLite tables from the audit log).
- Command history, workflow history, project paths, app preferences, setup choices, error/fix memory.
- "Forget this" / reset memory commands.
- Disable long-term memory toggle in Settings → Security.
- Visual workflow builder.

Carries the Kairo memory-design rules: no auto-train, no auto-upload, redact-before-store, audit-and-memory are physically separate tables.

---

## M7 — Production Release Layer

**Goal**: ship Vayu publicly as a real Windows product.

Features:

- MSIX package + normal `.exe` installer.
- GitHub release workflow + checksums + release notes.
- Manual test checklist + security checklist + crash-safe logging.
- Installer docs + demo video.
- **v1.0.0 production release** (cut after M7/M8 readiness — see the quality gate).
- Code signing + auto-update land in v1.x.

---

## M8 — Vayu Intelligence Layer

**Goal**: make Vayu locally intelligent.

Features:

- Local personal context graph.
- Error memory + fix recall.
- Project memory + workflow memory.
- Local semantic search + desktop timeline.
- Smart suggestions.
- Local embeddings (runs locally).
- **No automatic training on user data. No cloud upload without consent.**

---

## M9 — Vayu Mission Control

**Goal**: let Vayu coordinate other AI agents and sub-agents.

Pilots: ClaudePilot · CodexPilot · CursorPilot · GeminiPilot · HermesPilot · CodePilot · BuildPilot · MemoryPilot · SecurityPilot.

Features:

- Agent task queue with per-agent locks.
- Prompt preview + explicit approval before any prompt leaves Vayu.
- Multi-agent audit timeline + Kairo continuation-brief support.
- Vayu can open VS Code / Claude Code / Codex / Hermes-style agents and prepare prompts **only with user permission**.
- No secret prompt sending. No hidden control.

---

## M10 — Autonomous Workbench

**Goal**: let Vayu safely plan and execute multi-step work.

Features:

- Task planning + step-by-step execution.
- Build/test/fix loop with human approval checkpoints.
- Rollback plan + stop button + risk display + before/after summary.
- Commit only after approval — Vayu never commits autonomously.
- Project work runs through permission gates; can run locally or on **approved** VPS/server environments later.
- This is the foundation required before coding autopilot and the creative/3D features.

---

## M11 — Coding Autopilot and Website/App Builder *(closes Version 1)*

**Goal**: let the user prompt or talk to Vayu to create code projects, websites, and apps safely.

Features:

- "Create a website for my startup" / "Create a landing page" / "Create a dashboard".
- Create project folder/files, choose a framework template, write code.
- Run dev server, open preview, inspect build errors, fix errors **with approval**.
- Show the result, review whether the site/app looks good or bad, suggest improvements, apply approved changes.
- Generate README / deployment notes.
- All file creation/editing audited; commits only after approval; no secret files created accidentally.
- Can coordinate local coding agents and **approved** VPS/server agents later.

Example — *"Vayu, create a landing page for Flexdee."* → Vayu (1) asks for the project location, (2) creates files, (3) writes code, (4) runs the app, (5) opens the preview, (6) reviews the page, (7) suggests design improvements, (8) applies approved changes, (9) logs every step.

> **Version 1 target — M1–M11 together form Vayu Version 1:** a production-ready, local-first Windows AI desktop assistant and tool-system foundation with offline AI, online-provider safety, voice, desktop automation, workflows, memory, mission control, autonomous workbench, and the coding/website autopilot foundation.

---

# Vayu Version 2 — M12 – M20

> Advanced creative, 3D, agentic-workflow, rendering, advanced-provider, and desktop-companion ecosystem. **Everything below stays permission-gated, audit-logged, cancelable, and user-approved** — the M1–M10 safety model carries forward unchanged. Cloud/provider calls require consent; provider keys live in Windows Credential Manager (no plaintext).

## M12 — Creative AI Generation Layer

**Goal**: let Vayu generate and review images, icons, UI references, and creative assets.

Features:

- Image-generation provider registry + prompt-to-image workflows.
- App-icon, UI-reference, and social-graphic generation.
- Generated-asset library; compare outputs; review quality; say what's good/bad; suggest prompt improvements; regenerate after approval.
- Generated files saved under the selected Vayu workspace.
- Cloud calls require consent; provider keys stored securely.

Potential providers later (where available): OpenAI image models · Gemini image models · Stability AI · Replicate · Hugging Face · fal.ai · Leonardo · RunPod · custom providers.

---

## M13 — 3D Asset Generation Layer

**Goal**: let Vayu generate and manage 3D assets.

Features:

- Text-to-3D and image-to-3D workflows; 3D-model provider registry.
- Asset preview; file-format handling (GLB / FBX / OBJ / USDZ planning).
- Generated-asset library + metadata index.
- Quality review (polycount / material / texture notes); regenerate/improve workflow.
- Save to the selected Vayu workspace; all provider calls consent-gated.

Potential future providers (where available): NVIDIA visual/3D models · Meshy · Tripo AI · Luma AI · Rodin · CSM · Replicate 3D models · custom provider endpoints.

---

## M14 — Blender Integration Layer

**Goal**: let Vayu work with Blender safely. **Pilot: BlenderPilot.**

Features:

- Detect Blender install; open Blender; create/open project.
- Import generated 3D models; run Blender Python scripts **with approval**.
- Cleanup mesh; assign materials; basic lighting/camera; render preview; export assets.
- Save output to the Vayu workspace.
- Every script/action is shown before execution. No hidden Blender scripting.

---

## M15 — Unity Integration Layer

**Goal**: let Vayu work with Unity projects safely. **Pilot: UnityPilot.**

Features:

- Detect Unity Hub / Unity Editor; open Unity project.
- Import generated assets; create scene; assign materials; organize folders.
- Run editor scripts **with approval**; generate basic C# scripts; build/run a preview scene; export a package.
- Every action audited. No hidden project modification.

---

## M16 — Rendering and Asset Pipeline Layer

**Goal**: let Vayu coordinate rendering and asset pipelines.

Pilots: RenderPilot · AssetPilot · ScenePilot · TexturePilot.

Features:

- Render job queue + preview renders.
- Image/3D/Blender/Unity asset handoff; local asset library.
- File-conversion planning; compression/optimization; export profiles.
- Progress dashboard + cancel/stop control.
- All generated outputs stored under the selected Vayu workspace.

---

## M17 — NVIDIA and Advanced Provider Ecosystem

**Goal**: add advanced provider integrations after the creative/coding foundation is ready.

Features:

- NVIDIA provider support **where available through current NVIDIA provider catalogs/APIs**; NVIDIA Build / NIM catalog integration where practical.
- Image/vision/model APIs; 3D/visual provider discovery where available.
- Provider capability registry; cost/usage warnings; provider-specific consent.
- API keys in Windows Credential Manager; no plaintext keys; no cloud call without user approval.
- Provider plugins.

> No specific NVIDIA 3D API is promised to be free or always available — availability depends on current NVIDIA provider catalogs/APIs.

---

## M18 — Visual Workflow Builder and n8n-style Automation

**Goal**: let users build workflows visually — a local-first desktop automation system.

Features:

- Workflow graph builder; triggers; manual approval steps.
- App actions; AI-provider steps; file steps; image/3D generation steps; Blender/Unity steps.
- Condition branches; retry/error handling; audit trail.
- Reusable templates; workflow import/export.
- Every risky step is permission-gated.

Positioning: Vayu becomes **n8n-style automation for the Windows desktop + AI agents** — but local-first, permission-gated, and personal.

---

## M19 — Background Agent Runtime and Live Activity Dashboard

**Goal**: show what Vayu and its agents are doing in real time.

Features:

- Right-side live activity cards; background task queue.
- Running-agent cards; model-call cards; workflow-progress cards.
- Downloads / renders / builds / tests status; stop/cancel buttons.
- Agent logs; failure/retry cards; notification center.
- "What Vayu is doing now" feed; activity timeline connected to the audit log.

This is where the right-side dashboard cards become a real mission-control surface.

---

## M20 — Vayu OS / Desktop Companion Ecosystem *(closes Version 2)*

**Goal**: make Vayu feel like an AI operating layer over Windows.

Features:

- Always-available command bar; global hotkey overlay; floating mini Vayu Sphere.
- Context-aware desktop assistant; project-aware workspace launcher; personal automation dashboard.
- Cross-app workflow control; local knowledge hub; agent console.
- Trust Center pro; Developer Control Center; enterprise/privacy mode.
- Plugin-marketplace preparation; complete desktop-companion experience.

> **Version 2 target — M12–M20 together form Vayu Version 2:** a full creative, 3D, agentic-workflow, rendering, advanced-provider, and desktop-companion ecosystem.

---

## Post-M10 safety rule

Every advanced-automation milestone (M11–M20) inherits the same non-negotiable safety model: **permission-gated, audit-logged, cancelable, and user-approved.** No hidden automation, no silent clicking/typing, no secret reading, no cloud call or model download without explicit consent. Future server/VPS-assisted agent workflows are allowed **only** with explicit approval, secure execution, and audit logs.

---

## Release mapping

| Tag         | Milestone | Version | Theme                                       |
| ----------- | --------- | ------- | ------------------------------------------- |
| `v0.1.0-m1` | M1        | V1      | Desktop Foundation (released)               |
| `v0.2.0-m2` | M2        | V1      | Offline AI Layer (released)                 |
| `v0.3.0-m3` | M3        | V1      | Online Provider Layer                       |
| `v0.4.0-m4` | M4        | V1      | Voice Interaction                           |
| `v0.5.0-m5` | M5        | V1      | Advanced Desktop Automation                 |
| `v0.6.0-m6` | M6        | V1      | Workflows and Local Memory                  |
| `v0.7.0-m7` | M7        | V1      | Production Release infra → **v1.0.0** gate  |
| `v0.8.0-m8` | M8        | V1      | Intelligence Layer                          |
| `v0.9.0-m9` | M9        | V1      | Mission Control                             |
| `v0.10.0-m10` | M10     | V1      | Autonomous Workbench                        |
| `v0.11.0-m11` | M11     | V1      | Coding Autopilot & Website/App Builder — **Version 1 feature-complete** |
| `v0.12.0-m12` … `v0.20.0-m20` | M12–M20 | V2 | Creative → 3D → Blender → Unity → Rendering → NVIDIA/Advanced Providers → Visual Workflows → Background Agents → Vayu OS — **Version 2 feature-complete after M20** |

> `v1.0.0` is the **public production release**, cut after M7/M8 readiness depending on the quality gate — it is not tied automatically to a single milestone tag.
