# Vayu

**Local-first AI desktop command center for Windows. Offline AI through Ollama/Gemma, with optional online providers such as Gemini, OpenAI, Claude, DeepSeek, Kimi, OpenRouter, and custom OpenAI-compatible endpoints.**

[![CI](https://github.com/sandeepbollavaram/Vayu/actions/workflows/ci.yml/badge.svg)](https://github.com/sandeepbollavaram/Vayu/actions/workflows/ci.yml)
[![Security](https://github.com/sandeepbollavaram/Vayu/actions/workflows/security.yml/badge.svg)](https://github.com/sandeepbollavaram/Vayu/actions/workflows/security.yml)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)](#)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](#)

---

## What is Vayu?

Vayu is an open-source, local-first AI desktop command center for Windows.

It helps users control apps, run workflows, manage files, use local AI through Ollama (Gemma by default), optionally use an online provider of their choice — Gemini, OpenAI, Claude, DeepSeek, Kimi, OpenRouter, or a custom OpenAI-compatible endpoint — and operate developer tools through a safe permission-based agent system. See [docs/AI_PROVIDER_REGISTRY.md](docs/AI_PROVIDER_REGISTRY.md) for the full catalog.

## Core principles

- Local-first by default
- Offline AI through Ollama / llama.cpp
- Gemini only when enabled by the user
- No plain-text API keys in project files
- No hidden automation
- Confirmation before risky actions
- Full local audit logs
- Modular agent architecture

## Features (target)

- Text and voice commands ("Hey Vayu, open VS Code")
- Wake word, clap trigger, global hotkey, tray icon
- App launcher and Windows UI Automation control
- Local AI (Ollama / llama.cpp) and optional Gemini cloud AI
- Gmail drafting (send only after confirmation)
- VS Code / GitHub / Music / Files agents
- Visual workflow builder
- Local SQLite memory and audit log
- WinUI 3 dashboard with Vayu orb

## Modes

| Mode    | Internet | AI                  | Use case                                  |
| ------- | -------- | ------------------- | ----------------------------------------- |
| Offline | No       | Ollama / llama.cpp  | Default. Full privacy.                    |
| Online  | Yes      | Any registry provider | Opt-in. Pick from Gemini, OpenAI, Claude, DeepSeek, Kimi, OpenRouter, custom OpenAI-compatible, etc. |
| Hybrid  | Optional | Ollama first        | Local first, Gemini only when needed.     |

## Offline AI status (Milestone 2)

Vayu's offline brain is functional end to end as of M2:

- **Ollama detection** — Settings shows whether the Ollama executable is on `PATH` and whether its local server is reachable.
- **Curated model catalog** — Gemma 3 4B (recommended), Gemma 3 1B (low-end), and Llama 3.2 3B, each with installed/missing state, size, family, and parameter count read from Ollama's `/api/tags`.
- **Explicit-consent model pull** — the First Run Setup wizard downloads a curated model via `ollama pull` only after a consent dialog that names the exact tag and endpoint; you can cancel mid-download. No installer runs and no cloud is contacted.
- **Offline AI planner (opt-in)** — a local Ollama model turns commands into structured `IntentPlan`s. It is **off by default** and the Settings toggle stays disabled until Ollama is reachable and a curated model is installed.
- **Safety pipeline intact** — the local model only *proposes* a plan. Every action still flows `AgentRuntime → PermissionService → agent → audit log`. If the model is unavailable, unsure, or proposes anything outside the allowlist, Vayu falls back to the deterministic rule-based parser.

Cloud providers (Gemini and the wider registry) are **M3**, voice is **M4**, and typing/clicking inside apps is **M5** — none of these is active in M2.

## Security at a glance

- API keys live in **Windows Credential Manager**, an environment variable, or DPAPI-encrypted local config — **never** in repo files.
- Every log line passes through `SecretRedactor`.
- Risky actions (send email, run shell, delete file, share data with cloud) require explicit confirmation.
- Every action — allowed or denied — is written to a local audit log.

See [SECURITY.md](SECURITY.md) and [docs/SECURITY.md](docs/SECURITY.md).

## Installation

> Vayu is in active development. Milestone 1 is the desktop shell + app launcher. See [docs/ROADMAP.md](docs/ROADMAP.md).

Once releases are published:

- **MSIX**: `Vayu_<version>.msix`
- **Installer**: `Vayu_Setup_<version>.exe`
- Checksums: `SHA256SUMS.txt`

## Development setup

Requirements:

- Windows 10 or 11
- .NET 10 SDK
- Windows App SDK
- Visual Studio 2022 (17.10+) with the **Windows application development** workload, or VS Code + the C# Dev Kit

Clone and scaffold:

```powershell
git clone https://github.com/sandeepbollavaram/Vayu.git
cd Vayu
pwsh ./scripts/scaffold.ps1   # creates solution + projects
dotnet restore
dotnet build
dotnet test
```

Run the desktop app from Visual Studio (set `apps/Vayu.Desktop` as startup) or:

```powershell
dotnet run --project apps/Vayu.Desktop
```

## Ollama setup (offline mode)

1. Install [Ollama](https://ollama.com) and start it (Vayu never installs Ollama for you).
2. Open Vayu → **Settings → Offline AI · Ollama** and click **Refresh** to confirm the server is reachable.
3. Open **Setup** (or Settings → Open First Run Setup) → **Model catalog** → **Download** a curated model (Gemma 3 1B is the smallest). Vayu shows a consent dialog before any download and lets you cancel.
4. Back in **Settings → AI Mode**, flip **Offline AI planner (Ollama)** on. The toggle stays disabled until a curated model is installed and Ollama is reachable; the hint text tells you what's missing.

Vayu never sends data to Ollama beyond what you type. Ollama itself runs locally, and the offline planner makes no cloud calls. See [docs/LOCAL_AI.md](docs/LOCAL_AI.md) and the [M2 demo](docs/M2_DEMO.md) for the full walkthrough.

## Online provider setup (optional)

The First Run Wizard lets you pick any provider from the [AI Provider Registry](docs/AI_PROVIDER_REGISTRY.md) — Gemini, OpenAI, Anthropic Claude, DeepSeek, Kimi, OpenRouter, or a custom OpenAI-compatible endpoint. Gemini is the first one shipped (M3); the others land incrementally. The setup shape below is the same for every direct-API provider — only the help URL changes.

### Gemini (worked example)

**Never paste your API key into any file in this repository.**

Preferred storage:

1. **Windows Credential Manager** — Vayu's Settings page has a "Save Gemini Key" button that writes to the Credential Manager using DPAPI. The key never touches disk in plaintext.
2. **Environment variable** — set `GEMINI_API_KEY` for your user account.
3. **Encrypted local config** — fallback only; encrypted with DPAPI under your Windows account.

See [docs/GEMINI_SETUP.md](docs/GEMINI_SETUP.md) for the full walkthrough.

## Roadmap

See [docs/ROADMAP.md](docs/ROADMAP.md). Milestones: Desktop shell → Local AI → Gemini → Voice → Automation → Agents → Release.

## Contributing

See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md). Security-relevant changes follow [SECURITY.md](SECURITY.md).

## License

Apache-2.0 — see [LICENSE](LICENSE).
