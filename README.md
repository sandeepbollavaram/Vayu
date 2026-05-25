# Vayu

**Local-first AI desktop assistant for Windows, powered by Ollama offline and Gemini online.**

[![CI](https://github.com/sandeepbollavaram/Vayu/actions/workflows/ci.yml/badge.svg)](https://github.com/sandeepbollavaram/Vayu/actions/workflows/ci.yml)
[![Security](https://github.com/sandeepbollavaram/Vayu/actions/workflows/security.yml/badge.svg)](https://github.com/sandeepbollavaram/Vayu/actions/workflows/security.yml)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)](#)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](#)

---

## What is Vayu?

Vayu is an open-source, local-first AI desktop assistant for Windows.

It helps users control apps, run workflows, manage files, use local AI through Ollama, optionally use Gemini online, and operate developer tools through a safe permission-based agent system.

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
| Online  | Yes      | Gemini API          | Opt-in. Better reasoning, Gmail drafting. |
| Hybrid  | Optional | Ollama first        | Local first, Gemini only when needed.     |

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

1. Install [Ollama](https://ollama.com).
2. Pull a model: `ollama pull llama3.2`.
3. In Vayu **Settings → AI**, set provider = `Ollama`, endpoint = `http://localhost:11434`, model = `llama3.2`.

Vayu never sends data to Ollama beyond what you type or speak. Ollama itself runs locally.

## Gemini setup (online mode)

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
