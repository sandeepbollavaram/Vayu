# Roadmap

Each milestone is a *shippable* state: CI green, docs current, demo-able.

## Milestone status

| Version | Milestone                                          | Status         |
| ------- | -------------------------------------------------- | -------------- |
| v0.1.0  | **M1** — Desktop Foundation                        | ✅ Released     |
| v0.2.0  | M2 — Offline AI Layer                              | 🛠️ In progress |
| v0.3.0  | M3 — Online Provider Layer                         | ⏳ Planned     |
| v0.4.0  | M4 — Voice Interaction                             | ⏳ Planned     |
| v0.5.0  | M5 — Advanced Desktop Automation                   | ⏳ Planned     |
| v0.6.0  | M6 — Workflows and Local Memory                    | ⏳ Planned     |
| v1.0.0  | M7 — Production Release                            | ⏳ Planned     |
| v2.0.0  | M8 + M9 — Intelligence Layer + Mission Control     | ⏳ Planned     |
| v3.0.0  | M10 – M13 — Autonomous Workbench + Companion OS    | ⏳ Planned     |

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

## 🛠️ M2 — Offline AI Layer (v0.2.0, active)

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
| M2.8 | M2 UI/docs/release — polish, screenshots, tag `v0.2.0-m2`. |

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

## M7 — Production Release Layer (v1.0.0)

**Goal**: ship Vayu publicly as a production Windows app.

Features:

- MSIX packaging (signed in a follow-up).
- Normal `.exe` installer (Inno Setup).
- Release workflow producing both artifacts + `SHA256SUMS.txt`.
- Release-notes generation from milestones.
- Manual test checklist, security checklist, crash-safe logging.
- Installer docs + first demo video.
- **v1.0.0**. Code signing + auto-update channel land in v1.x.

---

## M8 — Vayu Intelligence Layer (v2.0.0, part 1)

**Goal**: make Vayu locally intelligent.

Features:

- Local personal context graph.
- Error memory + fix recall.
- Project memory + workflow memory.
- Local semantic search.
- Desktop timeline.
- Smart suggestions on the Home page.
- Local embeddings (Ollama embedding model or equivalent — runs locally).
- **No automatic training on user data.** **No cloud upload without consent.**

---

## M9 — Vayu Mission Control (v2.0.0, part 2)

**Goal**: let Vayu coordinate other AI agents (Claude Code, Codex, Cursor, Gemini CLI, local/API agents).

Pilots:

- ClaudePilot · CodexPilot · CursorPilot · GeminiPilot · CodePilot · BuildPilot · MemoryPilot · SecurityPilot.

Features:

- Agent task queue with per-agent locks.
- Prompt preview + explicit approval before any prompt leaves Vayu.
- Multi-agent audit timeline (every cross-agent call lands in the existing audit log).
- Reads Kairo continuation briefs when they're present on disk.
- Never secretly controls another AI agent.

---

## M10 — Autonomous Workbench (v3.0.0, part 1)

**Goal**: let Vayu safely plan and execute multi-step project work.

Features:

- Task planning with explicit steps.
- Step-by-step execution with human approval checkpoints at every L3+ action.
- Build/test/fix loop.
- Rollback plan + stop button + risk display.
- Before/after summary before any commit.
- "Commit only after approval" gate — Vayu never commits autonomously.

---

## M11 — True 3D Interactive Vayu Sphere (v3.0.0, part 2)

**Goal**: replace the M1 lightweight XAML sphere with a true interactive AI-core visualisation.

Features:

- Real 3D / particle sphere.
- Wind-shaped V inside the orb (matches the Vayu icon identity).
- Rotating particle ring.
- Voice-reactive amplitude.
- Typing-reactive pulse.
- Processing animation + success/error states.
- GPU-safe rendering.

Tech evaluated first: Win2D · Composition API · SwapChainPanel · DirectX · WebView2 canvas (only if necessary). Whatever ships, it must keep the M1 storyboards' "no heavy dependencies" promise.

---

## M12 — Plugin and Skill Ecosystem (v3.0.0, part 3)

**Goal**: let others extend Vayu safely.

Features:

- Plugin system with manifest declaration.
- Local skill files.
- Community agents.
- Workflow templates.
- Provider plugins (third parties can add registry entries).
- Desktop automation plugins.
- Permission declaration per skill — plugins inherit the L0–L6 model and the permission engine still gates every side effect.
- Skill marketplace as a follow-up.

---

## M13 — Vayu OS Layer / Desktop Companion Ecosystem (v3.0.0, part 4)

**Goal**: make Vayu feel like an AI layer over Windows.

Features:

- Always-available desktop command bar.
- Global hotkey overlay.
- Floating mini sphere.
- Context-aware desktop assistant.
- Project-aware workspace launcher.
- Personal automation dashboard.
- Cross-app workflow control.
- Local knowledge hub.
- Agent console (Mission Control surface).
- Trust Center pro.
- Developer Control Center.
- Enterprise / privacy mode.

---

## Release mapping

| Tag      | Milestone(s)                                                | Theme                                     |
| -------- | ----------------------------------------------------------- | ----------------------------------------- |
| `v0.1.0` | M1                                                          | Desktop Foundation (released)             |
| `v0.2.0` | M2                                                          | Offline AI Layer                          |
| `v0.3.0` | M3                                                          | Online Provider Layer                     |
| `v0.4.0` | M4                                                          | Voice Interaction                         |
| `v0.5.0` | M5                                                          | Advanced Desktop Automation               |
| `v0.6.0` | M6                                                          | Memory and Workflows                      |
| `v1.0.0` | M7                                                          | Production Release                        |
| `v2.0.0` | M8 + M9                                                     | Intelligence Layer + Mission Control      |
| `v3.0.0` | M10 + M11 + M12 + M13                                       | Autonomous Workbench + Companion OS       |
