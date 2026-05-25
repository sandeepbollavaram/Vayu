# Security Policy

## Supported versions

Vayu is pre-1.0. Only the `main` branch receives security updates. Once 1.0 ships, the latest minor release will be supported.

## Reporting a vulnerability

**Do not open a public issue for security problems.**

Email: `security@vayu.invalid` (replace with the real address before first release) — or open a private GitHub Security Advisory at:

`https://github.com/sandeepbollavaram/Vayu/security/advisories/new`

Please include:

- A description of the issue and its impact
- Steps to reproduce
- Vayu version / commit SHA
- Whether the issue is being publicly discussed anywhere

We aim to acknowledge reports within 72 hours and provide a fix or mitigation within 30 days for high/critical issues.

## Scope

In scope:

- Secret leakage (keys in logs, files, telemetry, exceptions, crash dumps)
- Permission bypass (an action runs without the documented consent step)
- Local privilege escalation through Vayu
- Audit log tampering or omission
- Network egress in offline mode

Out of scope:

- Issues that require the attacker to already be a local admin on the target machine
- Vulnerabilities in third-party software (Ollama, Gemini, Windows) — please report upstream
- Self-XSS or social-engineering scenarios

## Security principles (enforced by code and CI)

1. No API key, OAuth token, or credential is ever stored in a tracked file in this repository.
2. CI's `security.yml` workflow scans every PR for key-shaped strings and fails the build if any are found.
3. Vayu's runtime stores secrets only in: Windows Credential Manager, an environment variable, or a DPAPI-encrypted file under the current user's profile.
4. Every log statement passes through `SecretRedactor`. Tests verify redaction for the patterns we know about; new patterns are added with tests.
5. Risky actions (L3+) require explicit confirmation; consent decisions are written to the audit log.
6. Offline mode does not instantiate any network-bound AI client.
7. Cloud features are off by default and require the user to enter a Gemini key *and* enable online mode in Settings.

## Coordinated disclosure

We will credit reporters (with permission) in release notes. If you would prefer to remain anonymous, say so in your report.
