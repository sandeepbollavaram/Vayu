# Vayu — Vision & Product Positioning

> **Vayu is a local-first Windows AI desktop assistant and tool-system** that combines offline AI, optional online providers, safe desktop automation, workflows, agent orchestration, coding autopilot, and future creative/3D pipelines — all under **permission gates** and **local audit logs**.

## What Vayu is — and is not

- Vayu is **not** just a chatbot.
- Vayu is **not** just a voice assistant.
- Vayu is **not** just an automation script.
- Vayu **is** a **permission-gated AI desktop operating layer**.

Over its two product versions, Vayu can coordinate local apps, coding agents, cloud providers, creative tools, Blender/Unity, rendering jobs, and visual workflows — while the **user stays in control at all times**.

## Core principles (non-negotiable)

1. **Local-first by default.** Offline AI (Ollama / Gemma) works with no internet. Cloud is opt-in.
2. **Permission-gated.** Every side effect flows through the L0–L6 permission engine; L3+ requires confirmation.
3. **Audit-logged.** Every action — allowed or denied — is written to a local audit log.
4. **Cancelable.** A stop/cancel control is a first-class feature, not an afterthought.
5. **User-approved.** AI produces *plans*; it never executes tools directly. Commits, installs, downloads, cloud calls, and agent prompts all require explicit approval.
6. **Secret-safe.** API keys live in Windows Credential Manager (env var / DPAPI fallback) — never in repo files, never logged, never shown after saving.

These principles hold across **all** milestones, including the advanced Version 2 creative/3D/agent features.

## Two product versions

- **Vayu Version 1 (M1–M11)** — production-ready local-first desktop assistant & tool-system foundation: desktop foundation, offline AI, online providers, voice, desktop automation, workflows + memory, production release, intelligence, mission control, autonomous workbench, and the coding-autopilot / website-app-builder foundation.
- **Vayu Version 2 (M12–M20)** — advanced creative AI, 3D asset generation, Blender/Unity integration, rendering & asset pipelines, NVIDIA/advanced providers, n8n-style visual workflow builder, background agent runtime + live activity dashboard, and the Vayu OS / Desktop Companion ecosystem.

See [ROADMAP.md](ROADMAP.md) for the milestone breakdown and [AI_PROVIDER_REGISTRY.md](AI_PROVIDER_REGISTRY.md) for the multi-provider model (Vayu is not tied to any single vendor).

## Where Vayu runs

Vayu is a **Windows desktop** application first. It is designed to eventually support both local desktop usage and **approved** server/VPS-assisted workflows for agent execution — but every remote/server action must be **explicit, secure, logged, and user-approved**. Server/VPS execution is a future plan, not a current capability.

## Safety is the product

Vayu's differentiator is not "more automation" — it is **trustworthy** automation. Stop/cancel, audit logs, consent dialogs, and the permission engine are the core of the product, not optional add-ons. A feature that cannot be made safe, cancelable, and auditable does not ship.
