# First Run Setup Wizard

Vayu opens a First Run Setup Wizard the first time it launches after install. The wizard's job is to make Vayu *useful* before the user is done clicking, without ever doing anything risky behind their back.

## Goals

- Get the user to a working local AI (Ollama + Gemma) within a few minutes.
- Make online AI trivial to enable for users who want it — Vayu supports many providers (Gemini is one of them; see [AI_PROVIDER_REGISTRY.md](AI_PROVIDER_REGISTRY.md)).
- Make every step optional — Vayu must remain usable if the wizard is skipped.
- Never install software, download models, or store keys without explicit consent.

## Future setup-dashboard direction *(planning)*

As Vayu grows toward production (M7) and Version 2, the first-run setup expands from "pick a model" into a **setup dashboard** that captures where Vayu stores things and how it talks to providers. It should eventually ask:

- **Where to store Vayu data** (storage / data root).
- **Logs path.**
- **Cache path.**
- **Local model files path.**
- **Generated assets path.**
- **Project / workspace path.**
- **AI mode** — Offline / Online / Hybrid.
- **Offline provider** — Ollama / Gemma.
- **Online provider / API type** — from the [AI Provider Registry](AI_PROVIDER_REGISTRY.md).
- **Custom provider endpoint** if needed (OpenAI-compatible).
- **API keys stored securely**, preferably **Windows Credential Manager** — **no plaintext keys**.

Rules for the setup dashboard:

- **Every provider can be skipped.** Vayu stays usable in offline / rule-based mode.
- **Every model install/download requires explicit permission.** No silent installs or downloads.
- **Generated outputs default to the Vayu workspace.** Future generated assets, websites, renders, and project files are saved under the user-selected Vayu workspace/storage root unless the user chooses another path.

This is forward-looking direction — the current wizard (M2.5) is the read-only-detection + consented-pull foundation it builds on.

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
       no  → step 10
7.  Pick an online provider from the catalog (Gemini, OpenAI, Anthropic,
    DeepSeek, Mistral, Groq, OpenRouter, Azure OpenAI, AWS Bedrock, …).
    See AI_PROVIDER_REGISTRY.md for the full list.
8.  Show "How to get an API key" page (link comes from the chosen provider's
    SetupHelpUrl)
9.  User pastes their own key → save to Windows Credential Manager under
    Vayu:<ProviderId>:ApiKey → clear textbox
10. Verify:
       - Ollama reachable
       - Selected model present
       - (if applicable) selected online provider's key resolvable
11. Mark setup complete → write FirstRunSetupState to local SQLite
```

A **Skip** button is visible on every screen. Skipping does not break Vayu — it just leaves Vayu in a limited mode (rule-based parser, no cloud AI) until the user comes back to Settings.

## Offline mode setup

### Ollama detection

The wizard checks for Ollama in this order:

1. `winget` reports the `Ollama.Ollama` package as installed.
2. The `ollama` executable is on `PATH`.
3. `GET http://localhost:11434/api/tags` returns a 200.

The detection itself is implemented in M2.2 by `OllamaRuntimeService.GetRuntimeStatusAsync()` — it returns an `OllamaRuntimeStatus` snapshot the wizard renders directly. Detection is read-only: no install, no model pull, no cloud call, no elevation. Each probe is bounded by `OllamaProviderOptions.ProbeTimeoutSeconds` (default 5 s) so the wizard cannot stall on a slow runtime.

In M2.3 the same detection feeds the **Settings → Offline AI · Ollama** card via `SettingsLocalAiViewModel`. Users can already see endpoint, executable, server, and per-model installed/missing state from Settings — well before the First Run Wizard ships in M2.5. Settings exposes a **Refresh** button that re-runs detection only; install and model pull stay deferred to M2.6 with explicit consent.

M2.4 refines that listing: each installed curated row now shows size (`3.6 GB`), parameter size (`4B`), and family parsed from Ollama's `/api/tags` `details` block, and the card carries a small "Installed curated models: X / 3" summary plus an unknown-models counter. The wizard will reuse the same view model fields when M2.5 lands.

### M2.5 — the wizard ships (UI only)

M2.5 graduates the detection card into a five-step page available at **Setup** in the nav rail (and from a Settings card):

1. **Welcome** — context + reminder that the wizard is skippable.
2. **Mode** — the wizard sets up the Offline path; **Online Gemini** and **Hybrid** are now live as of **M3.5** but are chosen in **Settings → AI Mode** (not the wizard) after you save a Gemini key in Settings → Online AI · Gemini. The default everyday mode stays **Rule-based** (cloud off). In Online/Hybrid, every cloud planning call asks for consent (Allow once / Use local instead / Cancel), at most once per command, and every plan still passes through the permission engine.
3. **Ollama Status** — endpoint, executable, server, Refresh — shares the M2.3/M2.4 view model with Settings so the two never drift.
4. **Model Catalog** — the curated catalog with M2.4 metadata (size/family/parameter). A Download button is rendered **disabled** ("Download (M2.6 — requires permission)") so the future affordance is visible but inert.
5. **Verification** — Ready / Partially Ready / Not Ready verdict plus follow-up guidance.

Persistence is in-memory via `InMemoryFirstRunSetupService` (Vayu.Core); SQLite persistence is M2.8. Install and model pull remain deferred to M2.6 with explicit consent.

### M2.6 — the Model step gets a real Download button

Each missing curated row in the Model step now has its own **Download** button. Clicking it shows a `ContentDialog` titled `Download <tag> with Ollama?` with the exact endpoint (`http://localhost:11434/api/pull`), a reminder about download time and disk space, the estimated size when known, and two buttons — **Download** and **Cancel**.

On accept, `OllamaModelPullService` streams Ollama's `POST /api/pull` and reports progress lines onto the row (`downloading · 47%`, `verifying sha256 digest`, `success`). A top-level **Cancel current download** button appears while the pull is in flight; clicking it aborts the stream via `CancellationToken`. On completion (success, cancel, or error) the wizard re-runs detection so the Installed/Missing chips reflect Ollama's `/api/tags` rather than the pull stream.

The Ollama Status step still reads "Vayu will not install Ollama automatically." Ollama runtime install needs its own elevation prompt and is intentionally out of M2.6 scope.

### M2.7 — turning the model on

Once a curated model is installed, the user can enable **Offline AI planner (Ollama)** from **Settings → AI Mode**. The toggle is opt-in and defaults to OFF. When ON, the AI Router asks the local model to plan each command and falls back to the rule-based parser whenever the model is unavailable, unsure, or proposes anything outside the allowlist. The model only proposes a plan — the permission engine still gates every action, and typing/clicking is still deferred to M5.

As of M2.8 the toggle is **readiness-gated**: it stays disabled until Ollama is reachable and a curated model is installed, with hint text explaining what's missing. The planner runs the recommended installed model when present, otherwise the first installed curated model (`gemma3:1b`, then `llama3.2:3b`) — it never auto-pulls. If the runtime disappears while the planner is on, the next Refresh turns it off so Vayu falls back cleanly to the rule-based parser. See the [M2 demo](M2_DEMO.md) for the full walkthrough.

If none of the above, the wizard shows a screen explaining what Ollama is, what it does, and what installing it means. The user must click **Install Ollama** before anything happens — the actual install/pull flow is M2.6 and requires explicit consent. The manual install link (`https://ollama.com/download/windows`) is always visible as a fallback for users who'd rather install it themselves.

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

The wizard only shows the online-provider steps if the user picked **Online-only** or **Hybrid**. The provider catalog comes from [AI_PROVIDER_REGISTRY.md](AI_PROVIDER_REGISTRY.md); Gemini below is shown as the worked example, but the same flow applies to OpenAI, Anthropic, DeepSeek, Mistral, Groq, OpenRouter, and the rest.

### Provider selection

The picker lists providers grouped by kind:

- **OfflineLocal** — Ollama (default), llama.cpp, LM Studio, LocalAI.
- **OnlineDirect** — Gemini, OpenAI, Anthropic Claude, DeepSeek, Kimi, Mistral, Groq, Cohere, Perplexity, xAI Grok, Together, Fireworks, Cerebras, Hugging Face, Replicate.
- **CloudPlatform** — Azure OpenAI, AWS Bedrock, Google Vertex AI.
- **RouterAggregator** — OpenRouter, LiteLLM-compatible endpoint, custom OpenAI-compatible endpoint.

The user can:

- pick one online provider now and add more later from Settings,
- pick **none** and rely entirely on local AI,
- pick a provider but defer the key (the wizard records "selected, key pending" and Settings nudges the user later).

### How the user gets a provider API key

For each `RequiresApiKey == true` provider in the registry, the wizard shows a "How to get an API key" page using the provider's `SetupHelpUrl`. Below is the Gemini example; the OpenAI, Anthropic, Groq, etc. pages follow the same shape with their respective help URLs.

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
