# Contributing to Vayu

Thanks for considering a contribution. Vayu is meant to be a serious, safety-conscious project — so the bar is a little higher than "drive-by PR".

## Before you start

- For features: open an issue first. Match it to a milestone in [ROADMAP.md](ROADMAP.md). If it doesn't fit any milestone, propose where it should slot in.
- For bugs: include OS version, Vayu commit SHA, and a redacted log snippet.
- For security issues: **do not** open a public issue. See [../SECURITY.md](../SECURITY.md).

## Development environment

- Windows 10 or 11
- .NET 10 SDK
- Windows App SDK 1.5+
- Visual Studio 2022 17.10+ (or Rider / VS Code + C# Dev Kit)
- PowerShell 7

```powershell
git clone https://github.com/sandeepbollavaram/Vayu.git
cd Vayu
pwsh ./scripts/scaffold.ps1   # only first time
dotnet restore
dotnet build
dotnet test
```

## Code style

- C# 14 / .NET 10 idioms. `nullable enable` everywhere.
- One class per file. File-scoped namespaces.
- `async` end-to-end; no `.Result` / `.Wait()`.
- `internal` by default; only public what crosses a project boundary.
- Folder layout mirrors namespace.

## Tests

- Every new public type in `src/` gets a test in `tests/`.
- Tests must not require network or Ollama running. Mock providers.
- **Never** put a real API key in a test, even temporarily. Use `fake-gemini-key-for-tests`.

## Pull requests

- One concern per PR. Small and reviewable beats heroic.
- Link the issue.
- Update docs in the same PR if behavior changes.
- CI must be green. `security.yml` must be green (no key-shaped strings, no `.env`).

## Security-relevant changes

If your PR touches `Vayu.Security`, `Vayu.Permissions`, the audit log, or the AI Router, add:

- a description of the threat you considered, and
- tests covering the new path under denial and confirmation.

## Code of conduct

Be kind. Assume good faith. No discriminatory language. Maintainers reserve the right to lock threads that go sideways.
