# Vayu Security Architecture

This document explains *how* Vayu protects user data and credentials. The user-facing reporting policy lives in [SECURITY.md](../SECURITY.md) at the repo root.

## Threat model

We assume:

- The Windows account running Vayu is trusted.
- Other local users / processes running as the same account are **not** in scope (they can already read your files; DPAPI does not protect against you).
- The repository is public and may be cloned by anyone. **Nothing committed is secret.**
- The network is untrusted; cloud calls require explicit opt-in.

Out of scope: physical access, kernel-level malware, supply-chain attacks against .NET / Ollama / Gemini.

## Secret storage

Order of preference (`Vayu.Security` resolves in this order):

1. **Windows Credential Manager** (`WindowsCredentialSecretStore`). Uses `CredRead`/`CredWrite` via `Microsoft.AspNetCore.DataProtection` or `Meziantou.Framework.Win32.CredentialManager`. Scope: current user.
2. **Environment variable** (`EnvironmentSecretStore`). `GEMINI_API_KEY` etc. Useful for CI and power users.
3. **DPAPI-encrypted JSON** (`EncryptedJsonSecretStore`). Falls back here only if 1 and 2 are absent. Stored at `%LOCALAPPDATA%\Vayu\secrets.dat`, encrypted with `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`.

`appsettings.json` only specifies *which source to read from* (e.g. `"geminiKeySource": "windowsCredentialManager"`). It never contains keys.

## Redaction

`SecretRedactor` is the single source of truth for "what looks like a secret". It is applied:

- by `SecretRedactingEnricher` to every Serilog event
- by the global exception handler before any exception text is shown or stored
- by the audit log before any field is persisted

Default patterns (extensible, with tests):

- `AIza[0-9A-Za-z\-_]{35}` — Gemini / Google API keys
- `sk-[A-Za-z0-9]{20,}` — generic OpenAI-style keys
- `ghp_[A-Za-z0-9]{36}`, `gho_…`, `ghs_…` — GitHub tokens
- `xox[baprs]-[A-Za-z0-9-]{10,}` — Slack tokens
- `Bearer\s+[A-Za-z0-9._\-]+` — Authorization headers
- `eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+` — JWTs
- `(?i)(api[_-]?key|token|secret|password)\s*[=:]\s*\S+`

Matches are replaced by `[REDACTED]`. Tests in `Vayu.Security.Tests` assert each pattern.

## Logging

Serilog with sinks: rolling file at `%LOCALAPPDATA%\Vayu\logs\vayu-.log` (daily, 7-day retention) and an in-memory ring buffer for the Logs page.

- Default level: `Information`
- `Warning` for denied/risky actions
- `Error` for failures
- `Debug` only if the user toggles "Verbose logging" in Settings → Security. Even in Debug, secrets are redacted; only the *structure* of prompts is fuller.

## Permissions

See [PERMISSIONS.md](PERMISSIONS.md). The permission engine is *not* an afterthought — every connector takes `IPermissionService` as a constructor dependency, and the runtime refuses to register an agent that doesn't declare a max risk level.

## Network egress

- Offline mode: the Gemini HTTP client is **not constructed**. Verified by an integration test that asserts no `HttpClient` named `"Gemini"` exists in the DI container when `Mode == Offline`.
- Online mode: every outgoing request is logged (URL + status code + redacted headers). Request bodies are *not* logged.

## CI enforcement

`.github/workflows/security.yml` runs on every PR and `push`:

- Greps the diff for key-shaped strings.
- Fails if any `.env`, `secrets.json`, `*.pfx`, or `*.cer` is committed.
- Runs `Vayu.Security.Tests` to make sure redaction patterns still work.

## What we do NOT do

- We do not capture screenshots automatically.
- We do not read clipboard contents in the background.
- We do not keylog.
- We do not phone home with telemetry by default. (If we ever add telemetry, it will be opt-in, anonymized, and documented here.)
