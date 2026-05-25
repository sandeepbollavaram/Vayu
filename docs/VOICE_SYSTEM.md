# Voice System

Voice arrives in **Milestone 4**. This doc describes the design so the M1–M3 abstractions land in the right shape.

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
