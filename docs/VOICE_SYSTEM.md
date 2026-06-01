# Voice System

> **Status: M4.4 — system text-to-speech.** On top of M4.1 contracts, M4.2 push-to-talk UI, and M4.3 local STT, M4.4 adds **spoken responses** via System TTS — `TextToSpeechOptions`, `TextToSpeechProviderStatus`, `VoiceAssistantPhrases`, and `SystemTextToSpeechService` (validation, length cap, and a secret-guard) backed by a desktop `WinUiSpeechAdapter` (`Windows.Media.SpeechSynthesis`). TTS is **off by default** and opt-in (Settings → Voice toggle); the Home Voice card has **Speak status** + **Stop speaking**. Vayu only ever speaks short neutral phrases ("Done.", "Cancelled.", "I need confirmation to do that.") — **never user content, never a secret** (the guard refuses key/token/PEM-looking text). No cloud TTS, no audio files. Local STT (M4.3) remains a shell; the voice→command pipeline is still **M4.5**. The design below describes the full target shape.

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
| M4.5 | Voice command pipeline into `AgentRuntime`.                   |
| M4.6 | Vayu Sphere voice-state animation.                            |
| M4.7 | Wake word / clap trigger — planning docs only.               |
| M4.8 | M4 polish + demo.                                             |

---

## Design overview (target)

This doc describes the design so the M1–M3 abstractions land in the right shape.

## Inputs

Vayu listens in four ways. All four route through `IVoiceInputService` → `ICommandHandler`.

### 1. Push-to-talk (default, ships first)

Hold a global hotkey (default `Ctrl+Win+Space`). Audio is captured while held, transcribed when released. Zero always-on listening.

### 2. Wake word — "Hey Vayu"

A small always-on KWS (keyword spotting) model. Candidates:

- [openWakeWord](https://github.com/dscripka/openWakeWord) — Python, but exportable ONNX
- [Porcupine](https://picovoice.ai/platform/porcupine/) — proprietary, has a free tier
- [Vosk small KWS](https://alphacephei.com/vosk/) — fully offline

We default to openWakeWord (ONNX via `Microsoft.ML.OnnxRuntime`). Wake-word audio buffers are **discarded** after KWS triggers — only post-wake audio is sent to STT.

### 3. Clap trigger

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
