# Local AI (Offline Mode)

Vayu's default brain is local. Online AI is a strict opt-in.

## Why local first

- **Privacy** — your prompts, files, and window contents never leave your machine.
- **Latency** — first-token times under 200 ms on modern hardware.
- **No keys, no rate limits, no bills.**
- **Works on a plane.**

## Milestone staging (M2)

The offline AI layer ships in safe increments. Each stage is reachable only
after the previous one is green — Vayu never silently installs, downloads,
or runs anything.

| Sub  | Scope                                                                 | Status         |
| ---- | --------------------------------------------------------------------- | -------------- |
| M2.1 | Offline AI architecture / contracts (`LocalAiOptions`, `LocalModelCatalog`, `ILocalAiProvider`, …) | ✅ Done         |
| M2.2 | Ollama runtime detection (`OllamaRuntimeService`, `OllamaLocalAiProvider`, PATH + winget + `/api/tags` probes) | ✅ Done         |
| M2.3 | Local model catalog wiring — Settings → "Offline AI · Ollama" card with detection + curated catalog | ✅ Done         |
| M2.4 | Installed-model listing refinement — size in GB, family/parameter/quantization parsing, exact-tag matching, curated-vs-unknown summary | 🛠️ In progress |
| M2.5 | First Run Setup Wizard UI                                             | ⏳ Planned      |
| M2.6 | Safe model pull flow (`/api/pull` with explicit consent + progress)   | ⏳ Planned      |
| M2.7 | Local AI planner (`OllamaAiProvider` + AI Router with confidence floor) | ⏳ Planned      |
| M2.8 | M2 polish + `v0.2.0-m2` tag                                           | ⏳ Planned      |

Hard rules carried from M1 and reinforced for M2:

- **Every AI-produced plan still flows through `AgentRuntime` and the permission engine.** The local AI never invokes a tool directly.
- Local AI is **off by default** until the user explicitly enables it in the wizard (`LocalAiOptions.EnableLocalAi` defaults to `false`).
- Model downloads require a separate explicit consent step.

### M2.2 — what detection does and what it does NOT do

`OllamaRuntimeService` is a **read-only** detection layer. It answers four questions and nothing more:

1. Is `ollama` (or `ollama.exe`) on `PATH`? — synchronous file probe.
2. Does `winget list --id Ollama.Ollama` say the package is installed? — bounded sub-process probe; missing winget is non-fatal.
3. Is `GET http://localhost:11434/api/tags` returning a success status? — bounded HTTP probe (default 5 s).
4. Which models did `/api/tags` report, and is the recommended one (`gemma3:4b`) among them?

The result is an `OllamaRuntimeStatus` snapshot the First Run Wizard renders.

What M2.2 does **NOT** do — these belong to later sub-milestones with explicit consent:

- No `ollama pull` / model download. (M2.6.)
- No `winget install Ollama.Ollama`. (M2.6.)
- No cloud calls of any kind.
- No telemetry, no usage reporting, no "phone home".
- No process is ever started elevated; the winget probe is a plain `list`, never `install`.

### M2.3 — Settings UI for Offline AI

Vayu Desktop's Settings page now hosts an **Offline AI · Ollama** card backed by `SettingsLocalAiViewModel`. The card is read-only and shows:

- The configured endpoint (`http://localhost:11434` by default).
- Whether the Ollama executable was found on `PATH`.
- Whether the local server is reachable (`GET /api/tags`).
- A short, redaction-safe status message.
- The curated `LocalModelCatalog` rendered as a list, each row with `DisplayName`, `Description`, `HardwareNote`, `ModelTag`, and an **Installed / Missing** chip reconciled against Ollama's `/api/tags` response.

A **Refresh** button re-runs the same detection probes — it never installs Ollama, never pulls a model, never makes a cloud call. If detection itself throws, the UI shows "Could not probe Ollama runtime. Detection will retry on next refresh." rather than an exception dump.

When Ollama is unreachable the message points the user at the planned First Run Wizard (M2.5). When Ollama is reachable but no models are installed, the message names M2.6 as the milestone that will bring guided model pull *with explicit user permission*. Until those land, Settings remains the read-only window into Vayu's offline brain.

### M2.4 — installed-model listing refinement

The Settings card now extracts richer metadata from each `/api/tags` entry without changing the safety envelope:

- **Size** is shown per installed row as a friendly string (`3.6 GB`, `812 MB`, `45 KB`). The pure formatter `OllamaModelInfo.FormatSize(long?)` is reused anywhere a byte count needs rendering.
- **Family**, **parameter size**, and **quantization level** are parsed from the `details` block when Ollama supplies it. Missing or malformed `details` falls back to a name-derived family — never throws.
- **Modified-at** is parsed when present; an unparseable timestamp degrades to `null`.
- **Matching is exact, case-insensitive, tag-only.** Installed/Missing chips never flip true on a family-only or fuzzy match — that would mislead the user about what `ollama run gemma3:4b` will actually execute.
- A small **curated-vs-unknown summary** sits above the row list: *"Installed curated models: 2 / 3 · 1 other model present (not in curated catalog)"*. The unknown count is informational only — unknown tags never appear as curated rows.

Even with richer metadata, M2.4 still does **NOT** install anything, pull anything, or talk to the cloud. Refresh is detection-only.

## Supported providers

### Ollama (recommended)

[Ollama](https://ollama.com) runs a small REST server at `http://localhost:11434`. Vayu's `OllamaAiProvider` calls `/api/chat`.

> **The easy path is the [First Run Setup Wizard](FIRST_RUN_SETUP.md).** On first launch it detects whether Ollama is installed, asks permission to install it if not, asks permission to pull the recommended model, and verifies the result. Every step is skippable.

If you'd rather set it up by hand:

```powershell
winget install Ollama.Ollama
ollama pull gemma3:4b        # recommended default (~3 GB)
ollama pull gemma3:1b        # low-end fallback (~1 GB)
ollama pull llama3.2:3b      # alternative (~2 GB)
ollama pull qwen2.5-coder:7b # coding-specialized
```

> **Heads-up — model downloads are large.** A first `ollama pull` of a 4 B model is multiple gigabytes over your internet connection. After it finishes once, it's on disk and Vayu uses it offline forever — no re-download.

In Vayu Settings → AI:

- Provider: `Ollama`
- Endpoint: `http://localhost:11434`
- Model: `gemma3:4b` (or whichever you pulled)

### llama.cpp

For users who want a single-binary embedded model. Vayu spawns `llama-server.exe` from a configurable path on startup. We do not bundle weights.

## Choosing a model

| Use case                          | Suggested model        | Why                                            |
| --------------------------------- | ---------------------- | ---------------------------------------------- |
| **Default (modern laptops)**      | `gemma3:4b`            | Recommended by the First Run Wizard. Balanced. |
| Low-end (8 GB RAM, integrated GPU)| `gemma3:1b`            | Fits everywhere, still useful for parsing.    |
| Alternative                       | `llama3.2:3b`          | Solid 3 B model; pick if you already use Llama.|
| Better intent planning            | `llama3.1:8b`          | Stronger JSON-following on capable machines.  |
| Coding workflows                  | `qwen2.5-coder:7b`     | Tuned for code.                                |

## What the local model is asked to do

Vayu uses tiny, JSON-mode prompts. Example:

> System: "You convert user commands into IntentPlans. Reply with **only** valid JSON matching the schema."
> User: "open vscode and start the flexdee project"

Expected JSON:

```json
{
  "intent": "app.launch",
  "args": { "app": "vscode", "openPath": "C:\\Users\\…\\flexdee" },
  "risk": "L1",
  "confidence": 0.92
}
```

If JSON parsing fails twice, the runtime falls back to the **rule-based parser** in `Vayu.AgentRuntime` (regex / keyword matching for the M1 vocabulary). The rule-based parser is also what runs when no local model is configured at all, so Milestone 1 ships usable even without Ollama installed.

## Performance tuning

- Set `OllamaOptions.KeepAlive = "30m"` to avoid cold loads between commands.
- Use `num_ctx = 2048` for command parsing; we don't need long context here.
- For coding workflows, a separate provider instance with `num_ctx = 8192` is loaded on demand.

## Limits

A 3B local model is not GPT-4. It will mis-plan ambiguous commands. Vayu's mitigations:

- The permission engine always confirms L3+ actions, so a mis-plan can't silently misfire.
- `Hybrid` mode falls back to Gemini *only when local confidence is below floor and the user has consented to cloud planning*.
- The rule-based parser handles the 80% of commands ("open X", "play music", "show logs") deterministically.
