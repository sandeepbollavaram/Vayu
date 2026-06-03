# Voice System

> **Production voice setup (M4.11).** Voice now works end-to-end with no fake states. Settings → **Voice Setup** can **download a verified Whisper model** (tiny.en / **base.en (recommended)** / small.en from the canonical `ggerganov/whisper.cpp` Hugging Face files) with an explicit consent dialog (source, size, destination `%LOCALAPPDATA%\Vayu\models`), a progress bar, **Cancel**, and partial-file cleanup — or point at a model you already have. **Local STT shows "Ready" only when whisper.net can actually load the model** (a readiness probe, not just a file-exists check); otherwise it shows Not configured / Model invalid / Runtime unavailable / Failed. A **Test local STT** button captures a short sample, transcribes it locally, and shows the transcript — it **never** dispatches a command. From there, Home push-to-talk produces a real transcript and (with Enable voice commands ON) dispatches it through the permission/audit pipeline. Downloads require an explicit click; nothing downloads automatically.
>
> **"Hey Vayu" wake word (rebuild v2).** Vayu now has a **real local wake word** using **Vosk** (offline, on-device). It is **opt-in and off by default** — enabling it is the only thing that opens the mic for monitoring, and a visible indicator (the desktop sphere's listening dot) shows while it is armed. The pure `WakeWordStateMachine` only reaches `Armed` when wake word is enabled *and* a local model is present; a missing model yields an honest `Error` ("Wake word runtime not configured"), **never a fake "ready" and never a fabricated trigger**. On a real "hey vayu" / "vayu" match it raises a trigger, and the desktop `WakeWordCoordinator` runs one capture → whisper.net transcribe → `CommandRequest` → permission → agent → audit cycle (the same pipeline as a typed command). After each trigger there is a cooldown; there is a stop/disable control; no raw audio is logged; Vayu is never always-listening without the toggle. The free-floating **desktop sphere** is a control surface for this (start/stop listening, open Vayu) — it never captures audio or automates on its own.

> **whisper.net engine wired (M4.10).** The local STT delegate is now a **real on-device whisper.net engine** (`WhisperNetSpeechToTextEngine`, Desktop-only). Captured 16 kHz mono PCM is converted to float samples (`PcmAudioConverter`) and transcribed by whisper.net using the model file you configure in Settings → Voice Setup — **fully local, no cloud**. The native packages (`Whisper.net` + `Whisper.net.Runtime`) live **only** in the desktop app, so `Vayu.Voice` stays native-free and CI stays green. The engine maps every failure to a safe message (model missing / runtime unavailable / empty audio / no speech recognised / cancelled) and **never fabricates a transcript**. You supply the model file; Vayu does **not** download one (a guided model download is a future milestone). Recommended model: a GGML/GGUF Whisper model compatible with the whisper.net runtime (e.g. `ggml-base.en.bin`).

> **Real local STT vertical slice (M4.9).** Push-to-talk now performs **real microphone capture** and **real local transcription** when a model is configured. Flow: hold Push to talk → `IAudioCaptureService` captures 16 kHz mono PCM **in memory** (Windows `AudioGraph` adapter, capture only while active, capped by `MaxCaptureSeconds`) → Stop → the audio is transcribed by a local `ISpeechToTextProvider` → the transcript appears on the Home Voice card. If **Enable voice commands** is OFF the transcript is shown but nothing dispatches ("Transcript ready. Voice commands are off."); if ON, the transcript is dispatched as a `CommandRequest { Source = "voice/<provider>" }` through the **same** `IAgentRuntime` → permission → audit path as a typed command. **No cloud STT, no wake word, no clap, no always-listening.** The native engine plugs in via an injected transcription delegate, so the `Vayu.Voice` library stays native-free and CI stays green; until a real engine + model are configured the provider reports **not configured / runtime unavailable** and **no transcript is fabricated**. Configure the model path in Settings → Voice Setup.
>
> **Configuring the local STT model.** Settings → **Voice Setup** has a model-path field: enter the path to a local model file (e.g. a Whisper GGUF), press **Save** — Vayu validates the file exists and enables local STT — or **Disable** to turn it back off. The path is stored locally (no secret); audio never leaves the device and is never logged.
>
> **Local STT troubleshooting.**
> - *"No local STT model is configured"* — set a model path in Settings → Voice Setup and Save.
> - *"model file was not found"* — the path is wrong or the file moved; re-Save the correct path.
> - *"runtime could not transcribe / runtime unavailable"* — the file isn't a whisper.net-compatible GGML/GGUF model, or it's corrupt; try a known-good model such as `ggml-base.en.bin`.
> - *"No speech was recognised"* — capture succeeded but no words were detected; speak closer to the mic and try again.

> **Voice Setup surface (M4.R2).** The Home voice section is presented as **Voice Setup** — microphone status, Local STT provider status, TTS status, the voice-command toggle, and an explicit *planned, opt-in* line for wake word ("Hey Vayu") and clap trigger. It states the honest state plainly and never implies always-listening.

> **Honest state (M4.R).** Push-to-talk and text-to-speech are wired, and the voice→command pipeline exists, but a **local speech-to-text runtime is not configured yet** — so push-to-talk currently produces **no transcript** and voice commands stay **inert** until a real STT provider is set up (the Whisper provider is an honest shell, not a fake transcript). Wake word and clap trigger are **planned and opt-in** (design in this doc); Vayu is **never always-listening**; audio stays on-device and is never logged; Stop always stops. The Home Voice card states exactly this. The sections below describe the wired pipeline and the target design.

> **Status: M4.6 — Vayu Sphere voice-state polish.** On top of the wired voice pipeline (M4.5), M4.6 makes the **Vayu Sphere read the voice state at a glance**. `VoiceStateVisualMapper` (pure, testable) maps each `VoiceInteractionState` to a `VoiceVisualToken`, and `VayuSphere.SetVoiceState` turns that into a distinct lightweight cue: a calm idle pulse, a brighter/faster **Listening** pulse (cyan), a blue **Transcribing** scan, a violet **Thinking** accent, a teal **Executing** ring, a soft **Speaking** glow, a red **Error** flash, and an amber **Cancelled** fade. The Home Voice card shows a colour-matched state chip. **Visual-only** — no audio amplitude, no microphone/capture changes, no command-pipeline changes; the existing app-command Processing/Success/Error animations are untouched and repeated push-to-talk/stop never leaves a stuck animation. The design below describes the full target shape.

### M4.5 voice command pipeline (unchanged)

`VoiceCommandService` turns a successful, above-floor transcript into a `CommandRequest { Source = "voice/<provider>" }` dispatched through the **same** `IAgentRuntime` as a typed command. **No new execution path.** Off by default; failed/empty/low-confidence/cancelled dispatch nothing; inert until a real STT runtime is configured; short secret-safe TTS phrase after dispatch.

### M4.5 voice command pipeline

```
push-to-talk → VoiceRecognitionResult (local STT)
  └─ dispatch ONLY if Success && transcript non-empty && confidence ≥ floor && not cancelled
       → CommandRequest { Text = transcript, Source = "voice/<provider>", Metadata = { confidence, provider } }
         → IAgentRuntime.DispatchAsync  (AI Router → IPermissionService → agent → audit log)
           → CommandResult  → optional short TTS phrase (secret-safe)
```

| Type | Role |
| --- | --- |
| `VoiceCommandOptions` | `EnableVoiceCommands` (off by default), `MinimumConfidence` (0.70), `SourcePrefix` (`voice`), `SpeakResultWhenTtsEnabled`, `MaxTranscriptChars` (500). |
| `VoiceCommandResult` | Success / `WasDispatched` / transcript / confidence / `CommandResult` / reason / correlation id / provider. No audio. |
| `VoiceCommandService` | `IVoiceCommandService` impl. Gates dispatch, builds the `CommandRequest`, calls `IAgentRuntime`, and speaks a short result phrase. Never bypasses permissions; never speaks secrets or command text. |

### M4.4 TTS contracts

| Type | Role |
| --- | --- |
| `TextToSpeechOptions` | `EnableTextToSpeech` (off by default), `ProviderName` (`system`), `VoiceName`, `Rate`, `Volume`, `MaxCharsPerUtterance` (300). No secrets. |
| `TextToSpeechProviderStatus` | Provider availability + enabled flag + voice + message. No audio. |
| `VoiceAssistantPhrases` | The short, neutral phrases Vayu may speak. No long narration, no user content. |
| `SystemTextToSpeechService` | `ITextToSpeechService` impl: rejects blank/over-long/secret-looking text, then delegates playback to an injected engine. The default engine is a no-op (CI-safe); the desktop app injects `WinUiSpeechAdapter`. |
| `WinUiSpeechAdapter` (desktop) | The only place WinRT speech is touched; synthesises + plays in-process, discards the stream, `StopAsync` halts playback. |
| `VoiceTtsState` | Process-level opt-in the Settings toggle flips at runtime. Default OFF. |

### M4.3 local STT contracts

| Type | Role |
| --- | --- |
| `LocalSpeechToTextOptions` | `EnableLocalStt` (off by default), `PreferredProvider` (`whispercpp`), `ModelPath`, `MaxCaptureSeconds`. No secrets. |
| `SpeechToTextProviderStatus` | Provider availability + configured flag + model path + message. No audio. |
| `ISpeechToTextProvider` | `GetStatusAsync` / `TranscribeAsync(ReadOnlyMemory<byte>)`. Audio in memory only; never logged or persisted; never a cloud call. |
| `WhisperCppSpeechToTextProvider` | First local provider **shell**. Reports not-configured when the model/runtime is absent; never requires native DLLs; never fabricates a transcript. |

A clearly-labelled `MockSpeechToTextProvider` exists in the **test project only** — it returns a fixed transcript so the push-to-talk → transcribe flow can be exercised without a real engine. It is never registered in production.

## M4.1 contracts

| Type | Role |
| --- | --- |
| `VoiceInteractionState` | Idle / Listening / Transcribing / Thinking / Executing / Speaking / Error / Cancelled — drives the Sphere + activity feed. |
| `VoiceInputMode` | PushToTalk (active) · WakeWordPlanned · ClapTriggerPlanned (placeholders; `VoiceInputModes.IsActive` gates them). |
| `VoiceSession` | One interaction: id, state, mode, transcript (no audio), correlation id. |
| `VoiceRecognitionResult` | STT outcome: success/transcript/confidence/error/duration/provider. |
| `SpeechSynthesisRequest` / `SpeechSynthesisResult` | TTS request (text + voice/rate) and outcome. No secrets, no audio. |
| `MicrophoneStatus` | Availability + permission + device name; `CanCapture` gate. Reports only — never opens the device. |
| `IVoiceInputService` | `GetMicrophoneStatusAsync` / `StartPushToTalkAsync` / `StopAsync`. |
| `ITextToSpeechService` | `SpeakAsync` / `StopAsync`. |
| `IVoiceCommandService` | `StartPushToTalkCommandAsync` / `CancelAsync` — runs the full voice→command pipeline (wired in M4.5). |
| `VoiceEvent` + `IVoiceActivitySink` | Display-only events for the Sphere voice-state animation and activity cards. |

All contracts are **provider-neutral** — no Whisper / Vosk / Azure type is referenced. Public voice records carry no `byte[]` audio, no secrets.

### Command path (the safety invariant)

```
voice (push-to-talk)
  → transcript (local STT)
    → CommandRequest { Source = "voice/..." }
      → AI Router (rule-based / offline / online-hybrid)
        → IPermissionService (L0–L6 gate)
          → agent execution → audit log
```

The transcript is the *only* thing that crosses from the voice layer into the runtime — from there a spoken command is indistinguishable from a typed one (same planner, risk levels, confirmations, audit rows). Voice produces no plan of its own and executes nothing directly. Every capture is interruptible (`StopAsync` / `CancelAsync`), and a `Cancelled` session dispatches nothing.

### Milestone staging

| Sub  | Scope                                                          |
| ---- | -------------------------------------------------------------- |
| M4.1 | **Voice architecture / contracts** (this section). No capture.|
| M4.2 | ✅ Push-to-talk UI foundation — Home Voice card + Settings section + stub service. No capture, no STT, no execution. |
| M4.3 | ✅ Local STT provider foundation — `ISpeechToTextProvider` + `WhisperCppSpeechToTextProvider` shell (no native binary, honest not-configured), transcript area in the Home Voice card. Audio local/in-memory; no command execution. |
| M4.4 | ✅ TTS provider integration — `SystemTextToSpeechService` (validate + cap + secret-guard) + `WinUiSpeechAdapter` (System TTS). Off by default, opt-in toggle, Speak/Stop UI, short neutral phrases only. No cloud, no secrets spoken, no command execution. |
| M4.5 | ✅ Voice command pipeline — `VoiceCommandService` dispatches a successful, above-floor transcript through the existing `IAgentRuntime` (Source `voice/<provider>`). Off by default; failed/empty/low-confidence/cancelled never dispatch; no bypass; short secret-safe result phrase. Inert until a real STT runtime is configured. |
| M4.6 | ✅ Vayu Sphere voice-state animation — `VoiceStateVisualMapper` + distinct sphere cues per state (listening/transcribing/thinking/executing/speaking/error/cancelled) + Home state chip. Visual-only; no audio amplitude; app-command animations untouched. |
| M4.7 | ✅ Wake word / clap trigger — **planning docs only** (this doc's "Future wake word and clap trigger design" section). No engine, no clap detection, no always-listening service, no background capture. |
| M4.9 | ✅ **Real local STT vertical slice** — real push-to-talk mic capture (`IAudioCaptureService` + Windows `AudioGraph` adapter, in-memory PCM, capped, Stop-cancellable) + `DelegatingLocalSttProvider` (real transcription via injected delegate; honest not-configured, never a fake transcript) + Settings → Voice Setup model-path config. Transcript dispatch still requires Enable voice commands and flows through the permission/audit pipeline. No cloud STT, no wake/clap, no always-listening. |
| M4.10 | ✅ **whisper.net engine** — `WhisperNetSpeechToTextEngine` (Desktop-only, `Whisper.net` + `Whisper.net.Runtime`) plugged into the M4.9 delegate seam + `PcmAudioConverter` (16-bit PCM → float). Real on-device transcription from the configured model; safe failure mapping; no fake transcript; no cloud; `Vayu.Voice` stays native-free. User supplies the model file (no auto-download yet). |
| M4.11 | ✅ **Production voice setup** — guided model download (`WhisperModelCatalog` verified HF sources + `WhisperModelDownloadService` consent/progress/cancel/partial-cleanup), real readiness probe so "Ready" means whisper.net can load the model (`DelegatingLocalSttProvider` readiness-probe seam wired to `WhisperNetSpeechToTextEngine.TryProbeRuntime`), a **Test local STT** flow (transcribe-only, never dispatches), and full Voice Setup readiness states. End-to-end: model → Ready → push-to-talk → real transcript → opt-in dispatch. No fake transcript, no cloud, no auto-download. |
| M4.8 | M4 polish + demo.                                             |

---

## Future wake word and clap trigger design (M4.7 — planning only)

> **This section is design, not behaviour.** M4.7 plans wake word and clap
> trigger; it ships **no** wake-word engine, **no** clap detector, **no**
> always-listening service, and **no** background microphone capture. Today
> the only way Vayu captures audio is push-to-talk — the user holds a key.
> Everything below describes a *future, opt-in* capability gated on the safety
> and consent model written here. Until that future milestone, none of it runs.

### Where these fit

`VoiceInputMode` already reserves the two future triggers as **planned**
placeholders — `WakeWordPlanned` and `ClapTriggerPlanned` — and
`VoiceInputModes.IsActive` returns `true` only for `PushToTalk`. That gate is
the contract: a planned mode cannot become active by accident. Push-to-talk
(`Ctrl+Win+Space`, hold-to-talk) stays the shipping default and the only mode a
user needs.

### Non-negotiable safety rules (all future trigger work inherits these)

- **Push-to-talk is the safe default.** Wake word and clap trigger are extra
  conveniences, never the baseline.
- **Wake word is opt-in.** Off until the user explicitly enables it.
- **Clap trigger is opt-in.** Off until the user explicitly enables it.
- **Always-listening is OFF by default.** Enabling a wake word or clap trigger
  is the *only* way any continuous-listen path can exist, and even then the user
  must turn it on knowingly.
- **Local detection is preferred; no cloud wake-word detection by default.**
  Keyword spotting and clap detection run on-device. Audio for detection never
  leaves the machine. (Cloud STT for the *post-trigger* command remains a
  separate, already-consent-gated choice — the trigger itself stays local.)
- **No raw audio logging.** Detection buffers live in memory and are discarded;
  no audio bytes are written to disk or logs, ever — same rule as push-to-talk.
- **No background mic capture without a visible indicator.** Whenever a trigger
  is armed and the mic is open, a clear, always-visible indicator (sphere state
  + tray/status cue) must show it. No silent listening.
- **Clear stop/disable control.** The user can disable wake word / clap, and
  stop an in-progress listen, from an obvious control at any time.
- **A trigger only enters the `Listening` state.** Wake word and clap **start
  listening** — they never execute an action and never produce a plan of their
  own.
- **The command pipeline is unchanged.** Anything heard after a trigger still
  flows transcript → `CommandRequest { Source = "voice/..." }` → AI Router →
  `IPermissionService` (L0–L6) → agent → audit log. No new execution path, no
  permission bypass, identical to a typed or push-to-talk command.
- **No command executes** until its transcript passes the confidence floor *and*
  the permission/audit pipeline — a trigger firing is not consent to act.
- **Clearable history.** If any voice metadata is ever retained (e.g. an opt-in
  voice-debug transcript log), the user must be able to clear it. Audio is never
  retained.

### Wake word planning

Candidate wake phrases (final choice deferred to implementation):

- **"Hey Vayu"** — the primary candidate; two syllables of context reduce false
  positives.
- **"Vayu"** — shorter, higher false-positive risk; offered as a secondary
  option, not the default.
- **Custom wake phrase** — a later enhancement once the local KWS path is proven.

Design constraints for the future wake-word implementation:

- **Local-first detection.** A small on-device keyword-spotting (KWS) model
  (e.g. an ONNX export via `Microsoft.ML.OnnxRuntime`); no cloud wake detection
  by default. Pre-wake audio is discarded — only post-wake audio reaches STT.
- **False-positive protection.** A confidence threshold on the KWS score, plus
  optional double-trigger / short-phrase confirmation, so background speech and
  TV audio don't arm the mic.
- **Cooldown after activation.** After a trigger fires, a brief refractory window
  prevents immediate re-triggering and repeated accidental listens.
- **Manual confirmation for risky actions.** A wake-word-initiated command that
  maps to a higher risk level still hits the normal permission confirmation —
  the wake word never lowers a risk level or skips a prompt.
- **Visible armed state.** When the wake word is armed, the sphere/tray shows it;
  when it's actively listening post-trigger, that's the existing `Listening`
  visual.

### Clap trigger planning

- **Opt-in only.** Off by default; the user must enable it explicitly.
- **Pattern.** A simple **double-clap** to start, with a configurable pattern as
  a later option (e.g. number of claps / timing window).
- **False-positive protection.** Energy/transient thresholds over an estimated
  noise floor, with timing constraints, so single environmental bangs and
  applause don't trigger it.
- **Not for noisy environments by default.** The feature is conservative: in
  noisy conditions it should err toward *not* triggering, and the user is warned
  it's best suited to quiet rooms.
- **Only starts listening.** A detected clap pattern does exactly one thing —
  begins a short push-to-talk-style `Listening` window. It never executes a
  command directly and never bypasses the pipeline.
- **Same stop/disable + indicator rules** as wake word: visible when armed, a
  clear disable control, no raw audio logged.

### What M4.7 explicitly does *not* do

No wake-word engine, no clap detector, no always-listening service, no
background microphone capture, no new STT/TTS runtime, no command-execution
changes, and no UI beyond these doc references. Implementation is a **future
milestone**, gated on the model above.

---

## Design overview (target)

This doc describes the design so the M1–M3 abstractions land in the right shape.

## Inputs

Vayu listens in four ways. All four route through `IVoiceInputService` → `ICommandHandler`.

### 1. Push-to-talk (default, ships first)

Hold a global hotkey (default `Ctrl+Win+Space`). Audio is captured while held, transcribed when released. Zero always-on listening.

### 2. Wake word — "Hey Vayu"

> Opt-in and off by default — see "Future wake word and clap trigger design"
> above for the M4.7 safety/consent model that governs this.

A small always-on KWS (keyword spotting) model. Candidates:

- [openWakeWord](https://github.com/dscripka/openWakeWord) — Python, but exportable ONNX
- [Porcupine](https://picovoice.ai/platform/porcupine/) — proprietary, has a free tier
- [Vosk small KWS](https://alphacephei.com/vosk/) — fully offline

We default to openWakeWord (ONNX via `Microsoft.ML.OnnxRuntime`). Wake-word audio buffers are **discarded** after KWS triggers — only post-wake audio is sent to STT.

### 3. Clap trigger

> Opt-in and off by default — see "Future wake word and clap trigger design"
> above for the M4.7 safety/consent model that governs this.

Audio energy detector with a double-clap pattern (two transients within 600 ms, each > 0.6 RMS over noise floor). Triggers push-to-talk for 3 seconds. Off by default; on by user toggle.

### 4. Tray icon click / hotkey

Same as push-to-talk but without hold — toggles a 5-second listen window.

## Speech-to-text

Two providers, picked in Settings → Voice:

- **Whisper.cpp** (`whisper-small.en` by default) — runs locally, ~150 ms per command on CPU.
- **Vosk** — even smaller, lower accuracy, useful on weak hardware.

Cloud STT (Azure Speech) is supported but disabled by default. If enabled, the same consent rules as Gemini apply: each command's audio leaves only after consent.

## Text-to-speech

- **Windows.Media.SpeechSynthesis** (System TTS) — default, zero install.
- **Piper** (neural TTS) — optional, ships separately, much better quality.
- **Azure Speech** — optional, online.

Vayu's TTS persona is short and neutral: "Done.", "Cancelled.", "I need confirmation to do that." No verbose narration.

## Mic permissions

Vayu does not access the microphone until the user grants it explicitly. The first time push-to-talk is triggered, the OS-level prompt appears (Windows Privacy → Microphone). If denied, voice features are disabled with a clear message; text still works.

## Privacy

- Audio buffers are kept in memory only.
- Whisper.cpp runs locally; no audio leaves the machine.
- A "voice debug" toggle (off by default) saves transcripts for 24 hours into `%LOCALAPPDATA%\Vayu\voice-debug\`. Audio is **never** saved.
- All voice-triggered commands enter the same permission engine — voice is not a permission bypass.
