# Vayu — Development Handoff

> Living status document for continuing Vayu development. Last verified by a real
> build on **2026-06-13** (branch `rebuild-v2`). Read this top-to-bottom before
> picking up work. It is deliberately honest: it separates "compiles / unit-tested
> with fakes" from "a human ran it on a real PC and confirmed it."

---

## 0. The one rule that overrides everything

**Nothing counts as "done" until the USER runs it on their own PC and confirms.**
A green CI build and passing unit tests (which use fakes for mic, STT, UI
Automation, network) is **not** verification for desktop/voice features. Every
voice / automation / UI claim below is tagged with its real confidence level.

Hard product constraints (from the user, still in force):
- **No cards. No gradient colours.** macOS "liquid/water glass" look only.
- **No fake "Ready", no fake transcript, no fake wake word, no silent mic, no
  silent click/type, no unsafe shell execution, no plaintext secrets.**
- Every desktop automation action must be **permission-gated, audit-logged,
  cancelable, user-approved**.
- Stay on `rebuild-v2`. **Do not merge to `main` until the user verifies.**
- Commits: one file per commit, **no co-author trailers, no AI attribution.**

---

## 1. Snapshot

| Item | State |
|---|---|
| Active branch | `rebuild-v2` (18 commits ahead of `main`, pushed to origin) |
| Merged to main? | **No** — awaiting user real-PC verification |
| Solution build (`dotnet build Vayu.slnx -c Debug`) | ✅ 0 warnings / 0 errors (verified 2026-06-13) |
| Desktop x64 (`-p:Platform=x64 /warnaserror`) | ✅ 0 warnings / 0 errors (verified 2026-06-13) |
| Tests | 753/753 last full run (15 new wake-word tests). Not re-run this session; build is green. |
| CI on this branch | **Does not run** — `ci.yml`/`security.yml` trigger only on `main` |

### How to run it
```powershell
cd d:\Vayu
git checkout rebuild-v2
dotnet build Vayu.slnx -c Debug                                         # libs + tests (Any CPU)
dotnet build apps/Vayu.Desktop/Vayu.Desktop.csproj -c Debug -p:Platform=x64
```
Launch the built exe:
```
apps\Vayu.Desktop\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\Vayu.Desktop.exe
```
> NOTE: `dotnet build Vayu.slnx -p:Platform=x64` **fails** with MSB4126 — the
> solution has no `x64` solution-config mapping. Build the solution **without**
> `-p:Platform`, and build the Desktop project **with** `-p:Platform=x64`
> separately (this is exactly what `ci.yml` does).

---

## 2. Architecture map (what is real vs stub)

Pattern throughout: **testable logic lives in native-free libraries** (`src/…`,
target `net10.0`) so CI can run them with fakes; **native code** (mic, whisper,
Vosk, UI Automation, WinUI) lives only in `apps/Vayu.Desktop`
(`net10.0-windows…`) injected as delegates. The **M1 safety invariant** is
preserved everywhere: AI/voice only ever produce a `CommandRequest`/`IntentPlan`,
which flows `AgentRuntime → IPermissionService → agent → audit log`. Voice never
bypasses permission/audit.

### Libraries (`src/`) — REAL, unit-tested
| Project | Role | Status |
|---|---|---|
| `Vayu.Core` | Contracts: `CommandRequest`, `IntentPlan`, `RiskLevel` (L0–L6), `IClock`, storage-path contracts, setup state | ✅ real |
| `Vayu.AgentRuntime` | `AgentRuntime`, `AgentRegistry`, `AiRouterIntentPlanner`, `RuleBasedCommandParser` (emits `desktop.open_and_type`) | ✅ real |
| `Vayu.AI.Local` | Ollama detection + provider + model pull + offline planner (off by default) | ✅ real (needs Ollama installed to do anything) |
| `Vayu.AI.Online` / `Vayu.AI.Gemini` | Online provider + Gemini behind cloud-consent | ✅ real (needs API key) |
| `Vayu.Automation.Windows` | App launcher, installed-app catalog, window discovery, safety policy, typing executor contracts, `AppLauncherAgent`, `DesktopAutomationAgent` | ✅ real logic; native bits delegated to Desktop |
| `Vayu.Permissions` | `DefaultPermissionService`, `PermissionPolicy` (risk→confirm rules) | ✅ real |
| `Vayu.Memory` | SQLite audit log + durable user settings | ✅ real |
| `Vayu.Security` | 3 secret stores (Windows Credential Manager / encrypted-JSON / env), `SecureConfigService`, redaction | ✅ real |
| `Vayu.Logging` | Serilog + redactor + in-memory ring buffer | ✅ real |
| `Vayu.Voice` | STT/TTS/wake **contracts + pure state machines** (`VoiceSttState`, `WakeWordStateMachine`, `VoiceCommandService`, `SystemTextToSpeechService`); native-free | ✅ real, fake-injected tests |

### Connectors (`src/Vayu.Connectors.*`) — ⚠️ EMPTY STUBS
`Gmail`, `GitHub`, `VSCode`, `Music`, `Files` are **all just `Class1.cs` with an
empty class.** They are referenced in the solution but contain **no real
functionality**. Any "connect my Gmail / GitHub / music" capability does **not
exist** yet.

### Desktop adapters (`apps/Vayu.Desktop/Services`) — native, NOT exercised by CI
| File | Role | Real-PC confidence |
|---|---|---|
| `WindowsAudioCaptureService.cs` | 16 kHz mono PCM mic capture via AudioGraph + COM | ⚠️ builds; **needs human mic test** |
| `WhisperNetSpeechToTextEngine.cs` | Real local STT (whisper.net) | ⚠️ builds; needs whisper model + human test |
| `VoskWakeWordEngine.cs` | Real "Hey Vayu" wake (Vosk, AudioGraph) | ⚠️ builds; needs Vosk model + human test |
| `WakeWordCoordinator.cs` | Wake → capture → transcribe → dispatch (1 cycle, busy-guarded) | ⚠️ builds; needs human test |
| `WindowsTextTypingExecutor.cs` | Types text into a window via UI Automation `ValuePattern` | ⚠️ builds; **Notepad typing unconfirmed on real PC** |
| `WinUiAutomationConfirmationService.cs` | The approval dialog before typing | ⚠️ builds |
| `WinUiSpeechAdapter.cs` | System TTS | ⚠️ builds |
| `SphereOverlayWindow.xaml(.cs)` | Free-floating transparent desktop sphere | ⚠️ builds; **transparency/drag unconfirmed on real GPU** |

### Where the push-to-talk path actually goes (important nuance)
- **Home "Push to talk" → Stop** uses the **REAL** `IAudioCaptureService`
  (`WindowsAudioCaptureService`) + **REAL** `ISpeechToTextProvider`
  (whisper.net). This is the real path. ✅ (code), ⚠️ (real-PC).
- `IVoiceInputService` is still wired to **`StubVoiceInputService`** (`App.xaml.cs:236`).
  It is used only for the **mic-status string** and the older `VoiceCommandService`
  route — **not** for the actual push-to-talk capture. So "mic status" text is
  cosmetic/stub; the capture itself is real.

---

## 3. What WORKS (by confidence level)

### A. Verified by build + unit tests (high confidence in logic, fakes for I/O)
- Command pipeline: typed command → `AiRouterIntentPlanner` (rule-based, with
  optional Ollama / Gemini routing) → `AgentRuntime` → permission gate → agent →
  SQLite audit log.
- Agents registered & dispatchable: `AppLauncherAgent` (open known/installed
  apps), `ShowLogsAgent`, `ShowSettingsAgent`, `DesktopAutomationAgent`
  (`desktop.open_and_type`, L3, confirm-gated).
- Permission engine: risk L0–L6, L3 type/click requires confirm, L6 disabled.
- Secret handling: keys → Windows Credential Manager; status is value-free;
  redaction in logs.
- Audit log: every dispatch recorded with risk/status/agent.
- Wake-word **state machine**: arms only when enabled AND model present;
  missing model → honest `Error` "Wake word runtime not configured"; never
  fake-triggers (15 tests).
- First-run setup state persists in SQLite across restarts.
- Storage-path resolution (logs/cache/models/assets relocate; bootstrap settings
  DB stays at fixed default).

### B. Built, NOT yet confirmed on a real PC (the open verification list)
- Frosted-glass Command Center *look* (Mica BaseAlt, no cards/gradients).
- Free-floating desktop sphere: transparency, drag, right-click menu, click-to-focus.
- Real mic capture (AudioGraph) producing usable PCM.
- whisper.net transcribing real speech.
- Vosk "Hey Vayu" actually triggering on a real mic.
- **One-click Vosk wake-model download** (Settings → Voice Setup → "Hey Vayu
  wake word" → Download): downloads + extracts the alphacephei.com model into
  `…\models\vosk-wake`, then the Enable toggle arms it. Built + unit-tested
  (extract/flatten/zip-slip/cleanup) but not yet run end-to-end on a real PC.
- "open notepad and write hello" actually typing into real Notepad.
- System TTS speaking on the user's machine.

---

## 4. GAPS (promised / intended but NOT built)

1. **Real installer `.exe`** (Vibrance-style Inno Setup): install path → offline/
   online/both → Next installs Ollama model + API keys + link accounts → finish →
   launch. **Direction documented in `docs/FIRST_RUN_SETUP.md`; no installer
   exists.** Setup is currently an in-app first-run flow, not installer-first.
2. **Model downloads — mostly built now.** Whisper STT model: one-click download
   in Settings → Voice Setup (since M4.11). Vosk wake model: one-click download +
   extract in Settings → Voice Setup → "Hey Vayu wake word" (added on rebuild-v2,
   `VoskModelCatalog` + `VoskModelDownloadService`). **Remaining gaps:** no
   checksum/hash verification of the downloaded model bytes (only an HTTPS
   allowlist), and no install-time bundling so a fresh machine still downloads on
   first use. Both downloads still need real-PC end-to-end confirmation.
3. **Connectors** (Gmail, GitHub, VSCode, Music, Files) — empty stubs, no logic.
4. **"Drive Claude Code / other CLIs"** (Hermes/OpenClaw-style) — not started.
5. **VPS / remote control** — not started.
6. **Account OAuth linking** — not started (referenced in setup vision only).
7. **Startup animation** — referenced in product direction; not implemented.
8. **Always-on wake** refinement (slice 1B) — current wake is one-shot opt-in;
   robustness/continuous arming not hardened.
9. **`StubVoiceInputService`** still wired for `IVoiceInputService` — should be
   replaced or removed so mic-status reflects the real capture device.

---

## 5. BUGS / RISKS / SHARP EDGES

- **`dotnet build Vayu.slnx -p:Platform=x64` fails (MSB4126).** Not a code bug —
  a build-invocation footgun. Always build solution without platform; Desktop
  with platform. Documented in §1.
- **Sphere transparency via DWM negative margins** (`{-1,-1,-1,-1}`) is
  GPU/driver-sensitive; may render with a visible frame or black box on some
  machines. Unconfirmed.
- **whisper.net / Vosk model paths**: if the model folder is absent the features
  honestly report "not configured" (good), but there is **no guided fix** in the
  UI yet, so it looks broken to a user who hasn't read docs.
- **Mic capture COM interop is `unsafe`** (`IMemoryBufferByteAccess`). Builds and
  is bounded, but is the highest-risk native surface — review buffer handling if
  touched.
- **CA1031 catch-all** is pragma-suppressed at every UI/voice boundary by design
  (surface failure, never crash). Intentional, but means exceptions are swallowed
  — check logs, not crashes, when diagnosing.
- **Test suite not re-run this session** — 753/753 is last-known; only the build
  was re-verified green today.
- **`VayuAccent` enum + colored result indicators** still exist in
  `HomePage.xaml.cs` (Cyan/Teal/Violet/Amber/Red dots). These are small accent
  dots, not gradients/cards, but verify they don't read as "neon" against the new
  glass — the user is sensitive to this.

---

## 6. SECURITY / VULNERABILITIES

### Posture (good)
- **No plaintext secrets**: API keys go to Windows Credential Manager via
  `WindowsCredentialSecretStore`; status surfaces are value-free.
- **Secret scanner** (`.github/workflows/security.yml`): forbids committed
  `.env`/`secrets.json`/`.pfx`/`.cer`, and greps tracked files for key-shaped
  strings (AIza…, sk-…, ghp_/gho_/ghs_…, xox…, Bearer…, JWT, PEM). **Test secret
  data must be built at runtime**, never committed as literals, or this trips.
- **Permission/audit gating** on all automation; voice cannot bypass it.
- **Mic is opt-in**; wake word off by default; no always-listening without
  explicit enable + visible indicator.
- **Cloud calls gated** behind an explicit consent dialog (`ICloudConsentService`).
- Logging redacts secrets.

### Caveats / things to watch
- **Security workflow runs only on `main`** → the `rebuild-v2` branch is
  unscanned by CI. Run the secret patterns locally before merge.
- **`EncryptedJsonSecretStore`** is a writable fallback store — confirm it is not
  used to persist real keys in plaintext-adjacent form; Credential Manager should
  be primary.
- **whisper/Vosk models are downloaded over HTTP(S)** via `HttpClient` — when the
  one-click download is built, **verify TLS + checksum** the model files (supply-
  chain risk if a model URL is swapped).
- **No code signing** on the (not-yet-existing) installer — required before any
  real distribution or SmartScreen will block it.
- `unsafe` audio COM block — see §5.

No known exploitable vulnerability in committed code today; the above are
hardening items for when distribution/auto-download land.

---

## 7. Voice / wake-word specifics (the headline feature)

Goal: **"Hey Vayu, open notepad and write [what I say]" actually works.**

Current end-to-end chain (all code exists, real-PC unconfirmed):
```
Vosk wake ("hey vayu") ─trigger→ WakeWordCoordinator
  → IAudioCaptureService (AudioGraph, 6s cap)   [real mic]
  → ISpeechToTextProvider (whisper.net)         [real local STT]
  → CommandRequest{Source="voice/…"}
  → AgentRuntime → Permission confirm dialog → DesktopAutomationAgent
  → WindowsTextTypingExecutor (UI Automation ValuePattern) → Notepad
  → SQLite audit row
```
Fallback when wake model absent: **Home "Push to talk"** (same real capture +
whisper.net path, gated by the "Run my voice commands" toggle for dispatch).

To make the voice demo work on a real PC **right now**, the user must:
1. Download the Vosk wake model (Settings → Voice Setup → "Hey Vayu wake word" →
   Download) — or use push-to-talk instead of the wake word.
2. Download a whisper model (Settings → Voice Setup → "Download a model").
3. Enable the wake word toggle (off by default).

The one-click downloads now cover both models; what remains unproven is whether
they work end-to-end on the user's real machine (download → arm → trigger →
transcribe → type).

---

## 8. Recommended next steps (priority order)

1. ~~One-click Vosk + whisper model download~~ — **done** (whisper M4.11; Vosk
   wake model on rebuild-v2). Follow-up hardening: add **checksum/hash
   verification** of downloaded model bytes, and **install-time bundling** so a
   fresh machine doesn't download on first use.
2. **User runs slice 1A and reports** which of the §3B items work/break; fix the
   specific failing one (likely sphere transparency or Notepad typing). The wake
   model download → enable → "Hey Vayu" trigger path is now the key thing to test.
3. **Replace `StubVoiceInputService`** so mic-status reflects the real device.
4. **Real installer** (Inno Setup, Vibrance-style) per `docs/FIRST_RUN_SETUP.md` —
   only after the in-app flow is proven, and with code signing planned.
5. Then: connectors (real Gmail/GitHub/etc.), drive Claude Code/CLIs, VPS control,
   startup animation, slice-1B always-on wake hardening.

Deferred / user-gated (do NOT do autonomously): tag `v0.3.0-m3`; merge
`rebuild-v2` → `main`.

---

## 9. Key files index (fast navigation)

- DI / app bootstrap: `apps/Vayu.Desktop/App.xaml.cs`
- Home command center: `apps/Vayu.Desktop/Pages/HomePage.xaml(.cs)`
- Desktop sphere: `apps/Vayu.Desktop/SphereOverlayWindow.xaml(.cs)`,
  `apps/Vayu.Desktop/Controls/VayuSphere.xaml(.cs)`
- Wake word: `src/Vayu.Voice/WakeWordStateMachine.cs` (logic) +
  `apps/Vayu.Desktop/Services/VoskWakeWordEngine.cs` (native) +
  `…/Services/WakeWordCoordinator.cs`
- STT: `…/Services/WhisperNetSpeechToTextEngine.cs`, `…/WindowsAudioCaptureService.cs`,
  `src/Vayu.Voice` (`VoiceSttState`, `DelegatingLocalSttProvider`)
- Typing automation: `…/Services/WindowsTextTypingExecutor.cs`,
  `src/Vayu.Automation.Windows/DesktopAutomationAgent.cs` + `AutomationSafetyPolicy.cs`
- Planner / runtime: `src/Vayu.AgentRuntime/AiRouterIntentPlanner.cs`,
  `RuleBasedCommandParser.cs`, `AgentRuntime.cs`
- Theme (glass): `apps/Vayu.Desktop/Styles/VayuTheme.xaml`
- Setup/voice docs: `docs/FIRST_RUN_SETUP.md`, `docs/VOICE_SYSTEM.md`

---

*Keep this file current. When a §3B item is confirmed on a real PC, move it to
§3A and note the date. When a §4 gap is built, delete it from gaps and add it to
§3. This is the first thing the next session should read.*
