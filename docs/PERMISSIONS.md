# Permission Model

Every intent Vayu executes carries a `RiskLevel`. The `IPermissionService` decides whether the user must be asked.

## Levels

| Level | Name                   | Examples                                                | Default                |
| ----- | ---------------------- | ------------------------------------------------------- | ---------------------- |
| L0    | Observe                | Read battery, time, mode                                | Allowed                |
| L1    | Open apps & folders    | `open chrome`, `open downloads`                         | Allowed                |
| L2    | Read window metadata   | Focused window title, visible UI tree                   | Allowed                |
| L3    | Type / click in app    | Send text to VS Code editor                             | **Confirm**            |
| L4    | Send message / email   | Send Gmail draft                                        | **Confirm**            |
| L5    | Run shell command      | `git push`, `npm install`                               | **Confirm**            |
| L6    | Admin / system         | Install service, modify registry                        | **Disabled**           |

L6 actions are disabled in code and require a manual edit + rebuild to enable. There is no UI toggle.

## Confirmation UI

When the engine asks the user, the dialog shows:

```
┌──────────────────────────────────────────────────┐
│ Vayu wants to: send an email                     │
│ To:    rahul@example.com                         │
│ Subject: Status update                           │
│ Risk:  L4                                        │
│ Body preview:                                    │
│   Hi Rahul, …                                    │
│                                                  │
│ [ Allow ]  [ Edit ]  [ Cancel ]                  │
│ [ ] Always allow for this workflow               │
└──────────────────────────────────────────────────┘
```

- **Allow** — one-time grant.
- **Edit** — open the plan in a structured editor, then re-enter the engine.
- **Cancel** — write a `Cancelled` row to the audit log.
- **Always allow for this workflow** — off by default. Scoped to the current workflow id. Revocable from Settings → Security.

## Risky actions (always confirm)

- Send Gmail / reply to Gmail
- Delete file / move to protected location
- Run terminal command
- Install package
- Execute script
- Push / commit to GitHub
- Click destructive UI button (detected by element name heuristic: "Delete", "Remove", "Discard", "Sign out", etc.)
- Close unsaved app
- Access "private" documents (paths flagged by the user)
- Send local file content to Gemini
- Share screenshot with cloud AI
- Modify Windows settings

## Audit log

Every decision — including denials and cancellations — is written to the `Actions` table:

```
Id, TimestampUtc, AgentName, CommandText, RiskLevel,
PermissionDecision (Allowed | NeedsConfirmation | Denied | Cancelled),
Status (Success | Failed | PermissionRequired | Cancelled | NeedsClarification),
RedactedDetails (JSON),
ErrorMessage (nullable)
```

The audit log is append-only at the application level. The SQLite file is not signed; a sophisticated local attacker could tamper with it. Sign and ship to a write-once log is a 2.0 item.

## How an agent declares its max risk

```csharp
public sealed class GmailAgent : IAgent
{
    public string Name => "Gmail";
    public RiskLevel MaxRisk => RiskLevel.L4; // sends email; never shell
    public IReadOnlyCollection<string> Intents => new[] { "gmail.draft", "gmail.send" };
    ...
}
```

The runtime refuses to register an agent whose `MaxRisk` is lower than the `RiskLevel` declared by any of its intents (a static check), and refuses at runtime to execute an `IntentPlan` whose risk exceeds the agent's max.
