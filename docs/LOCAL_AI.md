# Local AI (Offline Mode)

Vayu's default brain is local. Online AI is a strict opt-in.

## Why local first

- **Privacy** — your prompts, files, and window contents never leave your machine.
- **Latency** — first-token times under 200 ms on modern hardware.
- **No keys, no rate limits, no bills.**
- **Works on a plane.**

## Supported providers

### Ollama (recommended)

[Ollama](https://ollama.com) runs a small REST server at `http://localhost:11434`. Vayu's `OllamaAiProvider` calls `/api/chat`.

Setup:

```powershell
winget install Ollama.Ollama
ollama pull llama3.2:3b      # small + fast
ollama pull llama3.1:8b      # better reasoning
ollama pull qwen2.5-coder:7b # coding-specialized
```

In Vayu Settings → AI:

- Provider: `Ollama`
- Endpoint: `http://localhost:11434`
- Model: `llama3.2:3b` (or whichever you pulled)

### llama.cpp

For users who want a single-binary embedded model. Vayu spawns `llama-server.exe` from a configurable path on startup. We do not bundle weights.

## Choosing a model

| Use case                  | Suggested model        | Why                                 |
| ------------------------- | ---------------------- | ----------------------------------- |
| Fast command parsing      | `llama3.2:3b`          | Snappy on CPU/iGPU                  |
| Better intent planning    | `llama3.1:8b`          | Stronger JSON-following             |
| Coding workflows          | `qwen2.5-coder:7b`     | Tuned for code                      |
| Tiny laptops              | `phi3:mini`            | Runs on 8 GB RAM                    |

## What the local model is asked to do

Vayu uses tiny, JSON-mode prompts. Example:

> System: "You convert user commands into IntentPlans. Reply with **only** valid JSON matching the schema."
> User: "open vscode and start the flexdee project"

Expected JSON:

```json
{
  "intent": "app.launch",
  "args": { "app": "vscode", "openPath": "C:\\Users\\…\\flexdee" },
  "risk": "L1",
  "confidence": 0.92
}
```

If JSON parsing fails twice, the runtime falls back to the **rule-based parser** in `Vayu.AgentRuntime` (regex / keyword matching for the M1 vocabulary). The rule-based parser is also what runs when no local model is configured at all, so Milestone 1 ships usable even without Ollama installed.

## Performance tuning

- Set `OllamaOptions.KeepAlive = "30m"` to avoid cold loads between commands.
- Use `num_ctx = 2048` for command parsing; we don't need long context here.
- For coding workflows, a separate provider instance with `num_ctx = 8192` is loaded on demand.

## Limits

A 3B local model is not GPT-4. It will mis-plan ambiguous commands. Vayu's mitigations:

- The permission engine always confirms L3+ actions, so a mis-plan can't silently misfire.
- `Hybrid` mode falls back to Gemini *only when local confidence is below floor and the user has consented to cloud planning*.
- The rule-based parser handles the 80% of commands ("open X", "play music", "show logs") deterministically.
