# AI Provider Registry

Vayu is **not** a Gemini-only assistant. The First Run Setup Wizard offers a catalog of supported providers so users can pick what they already pay for, already trust, or already run locally.

This document is the source of truth for **which providers Vayu knows about** at the contract level. It does **not** mean Vayu ships a working HTTP connector for each one — see the "Milestone scope" section below for what actually exists in code.

## Why a registry

- Locking the project to a single provider would alienate Claude, OpenAI, and local-only users.
- A flat enum (`Gemini`, `OpenAI`, …) would force every feature to grow a `switch` over providers.
- A registry lets the wizard, the Settings page, and future connectors share one canonical list.

## Provider catalog

### 1. Offline / local providers

Self-hosted; no API key required. Best for privacy, offline use, and zero recurring cost.

| ID            | Display name     | Notes                                                    |
| ------------- | ---------------- | -------------------------------------------------------- |
| `ollama`      | Ollama           | Vayu's default; recommended by the First Run Wizard.     |
| `llama-cpp`   | llama.cpp        | Single-binary embedded model. Vayu does not bundle weights. |
| `lm-studio`   | LM Studio        | Local server with OpenAI-compatible API.                 |
| `localai`     | LocalAI          | Drop-in OpenAI-compatible local server.                  |

### 2. Online direct API providers

Talk to a vendor's first-party API. Each provider has its own API key managed via Vayu's secret stores (Credential Manager → environment → DPAPI file).

| ID            | Display name              | Setup help                                            |
| ------------- | ------------------------- | ----------------------------------------------------- |
| `gemini`      | Google Gemini             | https://aistudio.google.com/app/apikey                |
| `openai`      | OpenAI                    | https://platform.openai.com/api-keys                  |
| `anthropic`   | Anthropic Claude          | https://console.anthropic.com/settings/keys           |
| `deepseek`    | DeepSeek                  | https://platform.deepseek.com                         |
| `kimi`        | Kimi (Moonshot AI)        | https://platform.moonshot.ai                          |
| `mistral`     | Mistral AI                | https://console.mistral.ai                            |
| `groq`        | Groq                      | https://console.groq.com/keys                         |
| `cohere`      | Cohere                    | https://dashboard.cohere.com/api-keys                 |
| `perplexity`  | Perplexity                | https://www.perplexity.ai/settings/api                |
| `xai`         | xAI Grok                  | https://console.x.ai                                  |
| `together`    | Together AI               | https://api.together.xyz/settings/api-keys            |
| `fireworks`   | Fireworks AI              | https://fireworks.ai/account/api-keys                 |
| `cerebras`    | Cerebras                  | https://cloud.cerebras.ai                             |
| `huggingface` | Hugging Face Inference    | https://huggingface.co/settings/tokens                |
| `replicate`   | Replicate                 | https://replicate.com/account/api-tokens              |

### 3. Cloud platform providers

Model hosting layered on a hyperscaler. Authentication is more complex than a single key — often involves resource IDs, regions, or full SDK credentials.

| ID                  | Display name        | Setup help                                                |
| ------------------- | ------------------- | --------------------------------------------------------- |
| `azure-openai`      | Azure OpenAI        | https://learn.microsoft.com/azure/ai-services/openai/quickstart |
| `aws-bedrock`       | AWS Bedrock         | https://aws.amazon.com/bedrock                            |
| `google-vertex-ai`  | Google Vertex AI    | https://cloud.google.com/vertex-ai                        |

### 4. Router / aggregator providers

One endpoint, many models. Useful for users who want to A/B different models behind a single billing relationship.

| ID                          | Display name                | Setup help                          |
| --------------------------- | --------------------------- | ----------------------------------- |
| `openrouter`                | OpenRouter                  | https://openrouter.ai/keys          |
| `litellm`                   | LiteLLM-compatible endpoint | https://docs.litellm.ai             |
| `custom-openai-compatible`  | Custom OpenAI-compatible    | User supplies base URL + key.       |

## Setup Wizard provider selection

After the **Mode** page (Offline-only / Online-only / Hybrid), the wizard branches:

- **Offline-only** — picker shows kind = `OfflineLocal` only. Default: `ollama` (matches `gemma3:4b`).
- **Online-only / Hybrid** — picker shows kinds `OnlineDirect`, `CloudPlatform`, `RouterAggregator`. Default: none selected; user must pick.
- **Hybrid** also requires picking an offline provider for the local step.

For each selected non-local provider that `RequiresApiKey == true`, the wizard shows a "paste key" page with the provider's `SetupHelpUrl`. The user can paste the key, **skip** that provider, or **come back later** in Settings.

## Secret naming convention

Each provider's API key lives under a stable name in the secret stores:

- Windows Credential Manager target: `Vayu:<ProviderId>:ApiKey`, e.g. `Vayu:Gemini:ApiKey`, `Vayu:OpenAI:ApiKey`.
- Environment variable: `<UPPER_PROVIDER_ID>_API_KEY`, e.g. `GEMINI_API_KEY`, `OPENAI_API_KEY`.

The encrypted DPAPI fallback uses the same `Vayu:<ProviderId>:ApiKey` name as the Credential Manager.

Cloud platform providers (Azure OpenAI, Bedrock, Vertex) need more than a single key — their setup screen will read multiple values (endpoint, region, resource ID) and store them as additional named secrets under the same `Vayu:<ProviderId>:*` prefix.

## Safety rules

The registry inherits every rule from `docs/SECURITY.md` and the [First Run Setup Wizard](FIRST_RUN_SETUP.md):

- No silent install of any provider's CLI or SDK.
- No silent download of weights, models, or registries.
- No provider's key is ever stored in a tracked file.
- No provider's key is ever logged. `SecretRedactor` patterns cover Gemini `AIza…`, OpenAI `sk-…`, Anthropic-style keys, GitHub tokens, JWTs, and generic `key=value` forms. New providers must ship redaction patterns before any connector merges.
- No provider's key is ever shown after save — Settings shows `<provider name>: key configured` only.
- Cloud AI is off by default. Even with a key present, the user must enable Online or Hybrid mode for that provider to be reachable.
- The user can disable any provider individually from Settings without uninstalling Vayu.

## Milestone scope

The registry exists at M1; connectors arrive one by one over later milestones.

- **M1** — this document + `AiProviderKind` enum + `AiProviderDescriptor` record + `AiProviderRegistry` static catalog of 25 entries. **No HTTP. No SDKs. No provider-specific code in any other project.**
- **M3.1** — the online provider *architecture* lands in `Vayu.AI.Online`: `OnlineAiOptions` (cloud off by default, consent required), `OnlineProviderDescriptor`/`OnlineProviderKind`, `OnlineProviderCatalog` (13 entries: gemini, openai, claude, deepseek, kimi, mistral, groq, cohere, perplexity, xai, openrouter, litellm, custom-openai-compatible), `OnlineProviderKeySource`/`OnlineProviderKeyStatus` (source only — **never the key value**), `CloudConsentRequest`/`CloudConsentDecision`/`ICloudConsentService`, `IOnlineAiProvider`, `OnlineAiPlanningResult`, and `OnlineAiSafetyPolicy` (allowlist + risk ≤ L1 for cloud plans). **Still no HTTP, no key storage, no cloud call.** `RecommendedFirstProvider` is `gemini` because it's implemented first — not because it's the only one.
- **M3.2** — first real connector: Gemini (`Vayu.AI.Gemini` implements `IOnlineAiProvider`). Uses the catalog for ID, setup doc, and capabilities; plans only behind explicit consent.
- **M4+** — additional connectors merge one PR at a time. Each PR adds:
  - implementation of `IOnlineAiProvider` (or a planner contract — TBD with the AI Router work in M2),
  - redaction patterns covering that provider's key format,
  - tests asserting the connector never logs the key,
  - update to this doc's "Milestone scope" listing the connector as shipped.
- **No connector lands without its redaction tests.**

## Adding a new provider

If you want Vayu to support a provider that's not in the catalog:

1. Open a PR adding an entry to `AiProviderRegistry.cs`. Choose a stable kebab-case `Id`.
2. Add the provider's API key shape to `SecretRedactor`'s test fixtures (compile-time-concatenated fakes — see `tests/Vayu.Security.Tests/SecretRedactorTests.cs`).
3. Add the entry to the appropriate section above in this file.
4. Leave the connector implementation for a follow-up PR — registry entries can exist without a working connector and will simply be greyed out in the wizard's provider picker until then.
