# First Run Setup Wizard

Vayu opens a First Run Setup Wizard the first time it launches after install. The wizard's job is to make Vayu *useful* before the user is done clicking, without ever doing anything risky behind their back.

## Goals

- Get the user to a working local AI (Ollama + Gemma) within a few minutes.
- Make online AI (Gemini) trivial to enable for users who want it.
- Make every step optional — Vayu must remain usable if the wizard is skipped.
- Never install software, download models, or store keys without explicit consent.

## Flow

```
1.  Welcome
2.  Pick a mode:  Offline-only  ·  Online-only  ·  Hybrid (recommended)
3.  Check Ollama:
       installed?   yes → continue
                    no  → ask permission → guided install → manual link fallback
4.  Check Ollama is reachable at http://localhost:11434
5.  Pick a local model:
       Recommended:        gemma3:4b
       Low-end fallback:   gemma3:1b
       Alternative:        llama3.2:3b
       → ask permission → pull via Ollama → show progress
6.  Online AI? (only if user picked Online-only or Hybrid)
       yes → step 7
       no  → step 9
7.  Show "How to get a Gemini API key" (link to https://aistudio.google.com/app/apikey)
8.  User pastes their own key → save to Windows Credential Manager → clear textbox
9.  Verify:
       - Ollama reachable
       - Selected model present
       - (if applicable) Gemini key resolvable
10. Mark setup complete → write FirstRunSetupState to local SQLite
```

A **Skip** button is visible on every screen. Skipping does not break Vayu — it just leaves Vayu in a limited mode (rule-based parser, no cloud AI) until the user comes back to Settings.

## Offline mode setup

### Ollama detection

The wizard checks for Ollama in this order:

1. `winget` reports the `Ollama.Ollama` package as installed.
2. The `ollama` executable is on `PATH`.
3. `GET http://localhost:11434/api/tags` returns a 200.

If none of the above, the wizard shows a screen explaining what Ollama is, what it does, and what installing it means. The user must click **Install Ollama** before anything happens. The manual install link (`https://ollama.com/download/windows`) is always visible as a fallback for users who'd rather install it themselves.

### Recommended models

| Model         | Size   | When to pick                                                |
| ------------- | ------ | ----------------------------------------------------------- |
| `gemma3:4b`   | ~3 GB  | **Recommended.** Modern laptops, snappy command parsing.    |
| `gemma3:1b`   | ~1 GB  | Low-end hardware, integrated GPU, 8 GB RAM.                 |
| `llama3.2:3b` | ~2 GB  | Alternative; better for users who already use Llama.       |

The wizard recommends `gemma3:4b` by default and only changes it if the system reports < 8 GB free RAM, in which case it suggests `gemma3:1b`.

### Model download

Before pulling, the wizard shows:

- Model name and Ollama tag
- Estimated size on disk
- Estimated download time at the user's current bandwidth
- A clear **Cancel** button

Pulling is performed by Ollama itself (`ollama pull <model>`). Vayu does not bundle weights, does not host its own mirror, and does not retry silently. Failed pulls show the Ollama error verbatim plus a "try again" / "skip" choice.

### Offline after first download

Once a model is pulled, it lives on disk under Ollama's storage. Vayu never re-downloads it. Subsequent launches go straight to a working offline state.

## Online mode setup

The wizard only shows the Gemini steps if the user picked **Online-only** or **Hybrid**.

### How the user gets a Gemini key

The wizard links to `https://aistudio.google.com/app/apikey` and explains:

- The key is *yours*; you create it in Google AI Studio.
- Vayu does **not** create the key for you and never sees your Google account.
- Free-tier limits and billing apply to *your* Google account, not Vayu.

### How the key is stored

Order of preference (`IGeminiKeySetupService` resolves):

1. **Windows Credential Manager** — target name `Vayu:Gemini:ApiKey`. DPAPI-protected. **Default.**
2. **Environment variable** `GEMINI_API_KEY` — if set, used as-is; the wizard does not overwrite it.
3. **Encrypted local file** under `%LOCALAPPDATA%\Vayu\secrets.dat` (DPAPI, CurrentUser) — fallback only.

The wizard *never*:

- saves the key to `appsettings.json`, `config.json`, `.env`, or any committed file;
- logs the key;
- includes the key in crash reports or telemetry;
- shows the key after save (a "key configured" indicator only — not the value).

### Reset / revoke

From **Settings → Security → Gemini API key**:

- **Revoke** — removes the Windows Credential Manager entry and clears in-memory cache.
- **Re-enter** — opens the same paste dialog as the wizard.

From PowerShell:

```powershell
cmdkey /delete:Vayu:Gemini:ApiKey
```

## Setup state

`FirstRunSetupState` (in `Vayu.Core.Setup`) is the wizard's source of truth:

```
Completed          bool
Mode               OfflineOnly | OnlineOnly | Hybrid
OllamaDetected     bool
OllamaEndpoint     string?            (http://localhost:11434)
SelectedLocalModel string?            (e.g. "gemma3:4b")
LocalModelInstalled bool
GeminiKeyConfigured bool
CompletedAtUtc     DateTimeOffset?
```

Persisted in SQLite (`UserSettings` table). The wizard reads this on app startup; if `Completed == false`, the wizard re-opens.

## Strict safety rules (enforced in code and CI)

- **No silent Ollama install** — the install button is the *user's* click.
- **No silent model download** — every `ollama pull` is preceded by a confirmation.
- **No forced Gemini** — cloud AI stays disabled by default, even after the wizard.
- **No plaintext keys** — `appsettings.json` / `config.json` / `.env` are *never* written to by the wizard.
- **No key in logs** — `SecretRedactor` runs on every log line; Gemini's `AIza…` pattern is in the default match set.
- **No key shown after save** — the textbox is cleared, the value is never re-displayed.
- **No real key committed** — `.github/workflows/security.yml` scans for `AIza`-style strings on every PR.
- **No cloud by default** — even with a key present, the user must enable Online or Hybrid mode.
- **Consent before private context** — the first time Vayu would send a file, window text, or screenshot to Gemini, a consent dialog appears.
- **Skip is always available** — Vayu must remain usable if the user skips every step.

## Troubleshooting

| Symptom                                  | What to try                                                  |
| ---------------------------------------- | ------------------------------------------------------------ |
| Ollama install fails                     | Install manually from https://ollama.com/download/windows    |
| `localhost:11434` not reachable          | Start Ollama (Start menu → Ollama), then click **Re-check**  |
| Model pull stuck at 0 %                  | Cancel, check internet, retry; try `gemma3:1b` if slow link |
| "Gemini key invalid" after save          | Re-issue the key in AI Studio; old keys may be revoked      |
| "Setup keeps reopening"                  | `FirstRunSetupState.Completed` is still false — finish or skip the wizard once explicitly |

## Milestone status

The wizard ships in stages:

- **M1** — architecture doc (this file) + minimal interfaces (`IFirstRunSetupService`, `IOllamaRuntimeService`, `IGeminiKeySetupService`, `FirstRunSetupMode`, `FirstRunSetupState`, `OllamaModelInfo`). UI not built yet.
- **M2** — Ollama detection + local model presence check + pull-with-consent flow. Wizard UI lands.
- **M3** — Gemini key save/validate via Windows Credential Manager + first-call consent dialog.
- **M7** — bundled installer can pre-fill defaults if the user accepts during install.
