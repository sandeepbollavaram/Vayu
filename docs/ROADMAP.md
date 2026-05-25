# Roadmap

Each milestone is a *shippable* state: CI green, docs current, demo-able.

## Milestone 1 — Desktop shell (active)

**Goal**: type `open vscode`, see a permission dialog, confirm, app opens, audit row appears.

- WinUI 3 app: Home, Settings, Logs pages
- `Vayu.Core` — interfaces, results, intents
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
- AI Router with Offline/Online/Hybrid
- Confidence threshold + fallback to rule-based parser
- Settings UI: choose endpoint + model
- Tests: provider mocked; router decision matrix

## Milestone 3 — Gemini online

- `GeminiAiProvider`
- Gemini key flow: Settings → save → Credential Manager
- Consent dialog for cloud planning
- `SecretValidationService` — does a *redacted* test call to verify the key
- Tests: connector must not log the key under any code path

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
