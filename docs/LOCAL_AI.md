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
| M2.4 | Installed-model listing refinement — size in GB, family/parameter/quantization parsing, exact-tag matching, curated-vs-unknown summary | ✅ Done         |
| M2.5 | First Run Setup Wizard UI — Welcome → Mode → Ollama → Models → Verification (read-only) | ✅ Done         |
| M2.6 | Safe model pull flow — explicit consent dialog + streaming `/api/pull` with per-row cancel | ✅ Done         |
| M2.7 | Local AI planner — `OllamaIntentPlanner` + `AiRouterIntentPlanner` (offline plan → rule-based fallback); opt-in Settings toggle | ✅ Done         |
| M2.8 | M2 polish + release — readiness-gated toggle, active-model selection, docs sweep, `v0.2.0-m2` tag | 🛠️ In progress |
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

### M2.5 — First Run Setup Wizard UI

The wizard ships as a regular nav page (`Setup` in the rail) and is also reachable from **Settings → Open First Run Setup**. It walks five steps:

1. **Welcome** — context + reminder that every step is skippable.
2. **Mode** — picker for Offline-only (active), Hybrid, and Online-only. The latter two are previewed as "coming in M3" and disabled.
3. **Ollama Status** — endpoint, executable detection, server reachability, and a Refresh button. Reuses the same `SettingsLocalAiViewModel` as the Settings card so the two views never drift.
4. **Model Catalog** — same curated rows as Settings (size / parameter / family / Installed-Missing chip). A "Download" button is rendered **disabled** with the label "Download (M2.6 — requires permission)" so the future flow is discoverable but inert.
5. **Verification** — derived headline (Ready / Partially Ready / Not Ready) plus guidance for the next milestone.

Wizard state lives in `InMemoryFirstRunSetupService` (Vayu.Core) for M2.5; SQLite persistence is M2.8. The wizard never installs Ollama, never pulls a model, and never makes a cloud call — those affordances arrive in M2.6 behind explicit consent.

### M2.6 — explicit-consent model pull

The Model step's Download button is no longer a placeholder. `IOllamaModelPullService` / `OllamaModelPullService` (Vayu.AI.Local) streams `POST /api/pull` with these guarantees enforced *in code, not just in UI*:

- The tag **must** exist in `LocalModelCatalog`. Anything else is rejected before the HTTP request fires.
- The pull is over the **local** Ollama endpoint only. No cloud calls. No subprocess. No installer.
- Streaming JSON-lines progress is surfaced through `IProgress<OllamaModelPullProgress>` and rendered on the row (`downloading · 47%`, `verifying sha256 digest`, etc.). One malformed progress line never kills the stream.
- `CancellationToken` aborts the in-flight stream; the final emitted progress carries `IsCancelled = true`.
- `IsInstalled` is never flipped on the basis of the pull stream alone — the wizard always re-runs `RefreshAsync` afterwards and trusts Ollama's `/api/tags` for the final word.

In the wizard:

1. **Consent dialog** — title `Download <tag> with Ollama?`, body names the exact endpoint (`http://localhost:11434/api/pull`), reminds the user that downloads take time and disk space, names the (optional) estimated size, and offers **Download** vs **Cancel**.
2. **Per-row progress** — every missing curated row has its own Download button; clicking starts a single pull and disables that row.
3. **Cancel** — a top-level **Cancel current download** button appears while the pull is in flight; clicking it aborts the stream via the same `CancellationToken` and the row reverts to a retryable state.
4. **Refresh** — after success/cancel/error, `Detection.RefreshAsync()` re-probes Ollama so the Installed/Missing chips reflect reality.

What M2.6 still does **NOT** do — these stay deferred:

- **No Ollama runtime install.** The wizard's Ollama Status step still tells the user "Vayu will not install Ollama automatically." Runtime install is its own future task with an elevation prompt.
- No cloud calls. No telemetry. No silent retries. No autosave of secrets.

### M2.7 — local AI planning (opt-in)

M2.7 lets a local Ollama model actually *plan* commands. The safety architecture is unchanged: **the model only proposes an `IntentPlan`; it never executes anything.** Every plan still flows `AgentRuntime → IPermissionService → agent → audit log`.

Components (Vayu.AI.Local + Vayu.AgentRuntime):

- **`OllamaIntentPlanner`** (`ILocalIntentPlanner`) — POSTs `/api/generate` with `format: "json"` and a compact system prompt, then hands the model's text to `OllamaPlanJsonParser`. Any failure (HTTP error, malformed JSON, timeout, empty command) returns a `LocalAiPlanningResult` failure rather than throwing.
- **`OllamaPlanJsonParser`** — the trust boundary. It extracts the JSON object (even from prose-wrapped output), enforces an **intent allowlist** (`app.open`, `ui.show_logs`, `ui.show_settings`, `unknown`), validates `confidence ∈ [0,1]`, requires `args.app` for `app.open`, and **assigns the risk level itself** — the model's claimed risk is ignored. A typing request becomes the same `typing_requested` flag the rule-based parser uses, so the agent defers it to M5. The model cannot smuggle in shell/file-delete/email/admin/browser-login intents.
- **`AiRouterIntentPlanner`** (`IIntentPlanner`) — the router `AgentRuntime` actually uses. When offline planning is **enabled** and the model returns a valid plan above the confidence floor, that plan is used (`PlanSource = ollama:<tag>`). Otherwise it falls back to the rule-based parser (`PlanSource = ollama-fallback-rule-based`). When offline planning is **disabled** (the default), it is pure rule-based (`PlanSource = rule-based`) and Ollama is never contacted. The router never throws for a normal model failure.

`LocalAiPlannerOptions` carries the model tag, `MinimumConfidence` (0.70), `MaxPromptChars`, and timeout. `LocalAiPlannerState` is the live, user-flippable toggle the **Settings → AI Mode** switch writes — no restart needed.

Still deferred: cloud / Hybrid routing (Gemini fallback) is **M3**; typing/clicking inside apps is **M5**. M2.7 makes no cloud calls and emits no telemetry.

### M2.8 — readiness gating, active model, release prep

M2.8 makes the opt-in honest and ships M2:

- **Readiness gate** (`LocalAiReadiness.CanEnablePlanner`) — the **Settings → AI Mode** toggle is disabled until Ollama is reachable **and** a curated model is installed. The hint text states the reason: *"Install and start Ollama first"*, *"Download Gemma 3 4B (or another curated model) first"*, or *"Offline planner is ready. Active model: …"*. If the planner is ON and the runtime later disappears, the next Refresh flips it **off** so Vayu never keeps poking a dead model.
- **Active model selection** (`LocalAiReadiness.SelectActiveModel`) — when enabled, the planner runs the recommended installed model (`gemma3:4b`) if present; otherwise the first installed curated model in catalog order (`gemma3:1b`, then `llama3.2:3b`). It **never auto-pulls** — selection is purely over what's already installed. The active tag is surfaced in the provider line and threaded into `LocalAiPlannerState.ActiveModelTag`, which `OllamaIntentPlanner` prefers over its configured default.
- **Persistence** — the planner toggle and active-model choice are **process-memory only** in M2 (`LocalAiPlannerState`). Durable persistence of user AI preferences is deferred to a later milestone alongside the broader settings store; document and move on rather than overbuild before release.
- **Release** — `v0.2.0-m2` is tagged only after a user-verified [M2 demo](M2_DEMO.md) of the full detect → list → pull → plan loop.

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
