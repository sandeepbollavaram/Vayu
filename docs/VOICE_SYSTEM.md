# Voice System

> **Status: M4.1 — contracts only.** The voice architecture is staked out in `Vayu.Voice`; there is **no real speech recognition, no microphone capture, and no always-listening** yet. Push-to-talk is the only mode that will be usable; wake word and clap trigger are placeholders. Concrete providers and the push-to-talk UI land in M4.2+. The design below describes the full target shape.

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
| M4.2 | Push-to-talk UI foundation.                                   |
| M4.3 | Local STT provider integration (Whisper.cpp first).          |
| M4.4 | TTS provider integration (System TTS first).                 |
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
