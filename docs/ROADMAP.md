# Roadmap

Each milestone is a *shippable* state: CI green, docs current, demo-able.

## First Run Setup Wizard — product feature (multi-milestone)

A core product feature of Vayu: on first launch, a wizard guides the user to a working local AI (Ollama + Gemma) and optionally an online provider key (any entry from the AI Provider Registry). See [FIRST_RUN_SETUP.md](FIRST_RUN_SETUP.md). The work is intentionally split:

- **M1**: architecture doc + minimal interfaces (`IFirstRunSetupService`, `IOllamaRuntimeService`, `IGeminiKeySetupService`, `FirstRunSetupMode`, `FirstRunSetupState`, `OllamaModelInfo`). **No UI, no installer automation, no real Ollama/Gemini calls.**
- **M2**: real Ollama runtime detection (`winget` query, PATH probe, `/api/tags` ping) + local model presence check + pull-with-consent flow. WinUI wizard pages land here.
- **M3**: Gemini key save/validate via Windows Credential Manager + first-call consent dialog (first concrete provider connector wired through the registry).
- **Later (M7 or earlier)**: optional bundled installer integration.

## AI Provider Registry — product feature (multi-milestone)

Vayu is not Gemini-only. The wizard offers a catalog of providers spanning offline (Ollama, llama.cpp, LM Studio, LocalAI), online-direct (Gemini, OpenAI, Anthropic, DeepSeek, Kimi/Moonshot, Mistral, Groq, Cohere, Perplexity, xAI, Together, Fireworks, Cerebras, Hugging Face, Replicate), cloud platforms (Azure OpenAI, AWS Bedrock, Vertex AI), and routers (OpenRouter, LiteLLM, custom OpenAI-compatible). See [AI_PROVIDER_REGISTRY.md](AI_PROVIDER_REGISTRY.md).

- **M1**: registry doc + `AiProviderKind` enum + `AiProviderDescriptor` record + `AiProviderRegistry` static catalog of 25 entries. **No HTTP, no SDKs.**
- **M3**: first concrete connector (Gemini) wired through the registry.
- **M4+**: incremental connectors — one PR per provider, each carrying redaction patterns and "must not log key" tests. Connectors without an implementation appear greyed out in the wizard.

## Milestone 1 — Desktop shell (active)

**Goal**: type `open vscode`, see a permission dialog, confirm, app opens, audit row appears.

- WinUI 3 app: Home, Settings, Logs pages
- `Vayu.Core` — interfaces, results, intents
- `Vayu.Core/Setup/` — First Run Wizard contracts (`FirstRunSetupMode`, `FirstRunSetupState`, `IFirstRunSetupService`)
- `Vayu.Core/Ai/` — Provider Registry contracts (`AiProviderKind`, `AiProviderDescriptor`, `AiProviderRegistry` — 25 entries)
- `Vayu.AI.Local` — `IOllamaRuntimeService`, `OllamaModelInfo` (contracts only; no real runtime call)
- `Vayu.AI.Gemini` — `IGeminiKeySetupService` (contract only; no real HTTP)
- `Vayu.Security` — secret stores + redactor (full)
- `Vayu.Logging` — Serilog + redacting enricher
- `Vayu.Memory` — SQLite schema + `IAuditLogService`
- `Vayu.Permissions` — engine + confirmation UI hook
- `Vayu.AgentRuntime` — dispatcher + rule-based parser
- `Vayu.Automation.Windows` — `IAppLauncher` for chrome/edge/vscode/notepad/terminal/downloads
- `xUnit` tests for Security, Permissions, Memory, AgentRuntime
- `.github/workflows/ci.yml` and `security.yml`

**Exit criteria**: M1 demo script in `examples/commands/M1-demo.md` runs end to end.

## Milestone 2 — Local AI

- `OllamaAiProvider` (`/api/chat` JSON-mode)
- `OllamaRuntimeService` — real implementation of M1's contract (winget query, PATH probe, `/api/tags` ping, `/api/pull` with progress events)
- First Run Wizard UI: Mode picker → Ollama check → model picker (`gemma3:4b` default, `gemma3:1b` fallback, `llama3.2:3b` alternative) → pull-with-consent → verify
- AI Router with Offline/Online/Hybrid
- Confidence threshold + fallback to rule-based parser
- Settings UI: choose endpoint + model
- Tests: provider mocked; router decision matrix; wizard state machine

## Milestone 3 — First online provider (Gemini)

> Gemini is shipped first because the contract from M1 already exists. Subsequent online providers (OpenAI, Anthropic Claude, DeepSeek, Kimi, OpenRouter, Mistral, Groq, custom OpenAI-compatible, …) follow the same shape and are added incrementally per [AI_PROVIDER_REGISTRY.md](AI_PROVIDER_REGISTRY.md) in M4+.

- `GeminiAiProvider`
- `GeminiKeySetupService` — real implementation of M1's contract (save to Windows Credential Manager, validate via redacted test call, revoke)
- First Run Wizard "Gemini" page (only shown for Online/Hybrid modes) — paste-key dialog → save → clear textbox → key never re-displayed
- Consent dialog for cloud planning (first-call gate)
- `SecretValidationService` — does a *redacted* test call to verify the key
- Tests: connector must not log the key under any code path; wizard cannot persist key to any tracked file

## Milestone 4 — Voice

- Push-to-talk + Whisper.cpp
- TTS (System)
- Wake word ("Hey Vayu" via openWakeWord)
- Clap trigger
- Mic permission flow

## Milestone 5 — Windows automation

- Focused window detection
- UI element tree read
- Controlled click / type (L3 — always confirms)
- Screenshot-with-consent
- Refuses to automate unapproved process names

## Milestone 6 — Agents

- Gmail (draft + send-on-confirm)
- VS Code (open project, run task, terminal)
- GitHub (commit message, PR body — no auto push)
- Music (Spotify / local)
- Files (search, organize, delete-on-confirm)
- Workflow builder UI

## Milestone 7 — Release

- MSIX packaging
- Inno Setup `.exe` installer
- Code signing
- Auto-update channel (sparkle / Velopack)
- Release workflow + SHA256SUMS
- Demo video script + screenshots
- README polish for launch
