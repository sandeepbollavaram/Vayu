# Gemini Setup (Online Mode)

> **Gemini is optional. Vayu supports many providers** — Gemini is one entry in the [AI Provider Registry](AI_PROVIDER_REGISTRY.md) alongside OpenAI, Anthropic, DeepSeek, Mistral, Groq, OpenRouter, Azure OpenAI, AWS Bedrock, and others. This document is the worked example for **how to set up an online provider**; the same shape applies to the other entries in the registry.

**Never paste your API key into any file inside this repository.**

> **Status:** as of **M3.2** the Gemini connector exists (`Vayu.AI.Gemini.GeminiProvider`) and can plan a command into an `IntentPlan` — but only behind explicit per-call consent and only when a key is configured. It reads the key from the secret stores below; the **key-setup UI** (paste/save) is **M3.3** and the **consent dialog** is **M3.4**, so until those land Gemini is not yet reachable from the app and cloud AI stays disabled by default.

The Gemini connector lives in `Vayu.AI.Gemini`. It is only constructed when:

1. `appsettings.json` has `"defaultProvider": "online"` or `"hybrid"`, *and*
2. A Gemini key is resolvable from one of the secret stores.

> **The easy path is the [First Run Setup Wizard](FIRST_RUN_SETUP.md).** If you pick **Online-only** or **Hybrid** mode in the wizard, you can choose Gemini from the provider catalog and get a guided "paste your Gemini key" screen that writes the key directly to Windows Credential Manager and clears the textbox. The steps below are the manual equivalent for users who skipped the wizard or want to script the setup.

## Step 1 — Get a key

1. Visit `https://aistudio.google.com/app/apikey`
2. Create an API key.
3. Note the key. It will look like `AIzaSy…` (35+ characters).

## Step 2 — Store the key (pick ONE)

### Option A — Windows Credential Manager (recommended)

Open Vayu → **Settings → Security → Gemini API key → Save**. The dialog accepts the key, writes it via DPAPI to the Windows Credential Manager under the target name `Vayu:Gemini:ApiKey`, and immediately clears the textbox. The key never touches disk in plaintext.

Verify from PowerShell:

```powershell
cmdkey /list:Vayu:Gemini:ApiKey
```

You should see the entry; the value is **not** displayed.

### Option B — Environment variable

```powershell
[Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "AIza...your-key...", "User")
```

Restart Vayu. The `EnvironmentSecretStore` will pick it up.

Useful for CI runners (set as a GitHub Actions repository secret, not in code).

### Option C — Encrypted local config (fallback)

If neither of the above is available, `EncryptedJsonSecretStore` writes to
`%LOCALAPPDATA%\Vayu\secrets.dat`, encrypted with DPAPI (current user only).

You should not need this option on a developer workstation. It exists for users without admin rights or in locked-down environments.

## Step 3 — Enable online or hybrid mode

In Vayu Settings → AI:

- Mode: `Hybrid` (recommended) or `Online`
- Provider check: the page shows `Gemini key: present` (it never shows the key itself).

## Step 4 — Consent the first time

The first time a command would send your local context (e.g., file contents, window text) to Gemini, Vayu shows a consent dialog:

> "Vayu wants to send the following data to Google Gemini to plan this command. [preview]"
> [ Send ] [ Don't send ] [ Don't send, and use local AI instead ]

Your choice is logged (not the data).

## Revoking the key

- Settings → Security → Gemini API key → **Revoke**. Deletes the credential entry and clears the in-memory copy.
- Or PowerShell: `cmdkey /delete:Vayu:Gemini:ApiKey`
- Or remove the environment variable.

## What we send to Gemini

Only what's needed to plan a command:

- The user's natural-language command
- The structured `CommandRequest`
- The current available intents (so the model picks one)
- *Optional*, with explicit consent: the focused window's title, a file's contents, a screenshot

We do **not** send: Vayu's audit log, your other secrets, the contents of any other window, or your file system tree.

## Costs

Gemini API usage is billed by Google. Vayu shows token estimates before each cloud call when `Settings → AI → Show cost preview` is on (off by default to reduce friction; recommended on for early users).
