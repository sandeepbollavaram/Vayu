namespace Vayu.AI.Gemini;

/// <summary>
/// Builds the compact, allowlist-anchored planning prompt sent to Gemini.
/// Contains no secrets and applies a hard length cap.
/// </summary>
internal static class GeminiPromptBuilder
{
    /// <summary>
    /// The system instruction. Mirrors the offline planner's contract so the
    /// model is held to the same narrow, safe vocabulary.
    /// </summary>
    public const string SystemInstruction =
        "You convert a user's desktop command into a single JSON object and nothing else. " +
        "Allowed intents: \"app.open\" (open an installed app), \"ui.show_logs\", \"ui.show_settings\", \"unknown\". " +
        "Schema: {\"intent\":string,\"confidence\":number 0..1,\"args\":{\"app\":string optional,\"typingRequested\":boolean optional},\"reason\":string}. " +
        "Rules: For \"open X\" use intent \"app.open\" with args.app set to X. " +
        "If the user asks to type, write, or enter text into an app, set args.typingRequested to true. " +
        "Never invent shell commands, file deletions, emails, clicks, logins, admin actions, or URLs; use \"unknown\" if unsure. " +
        "Reply with only the JSON object.";

    /// <summary>Composes the full user-turn text, trimmed to <paramref name="maxPromptChars"/>.</summary>
    public static string BuildUserPrompt(string commandText, int maxPromptChars)
    {
        var text = (commandText ?? string.Empty).Trim();
        if (maxPromptChars > 0 && text.Length > maxPromptChars)
        {
            text = text[..maxPromptChars];
        }
        return text;
    }
}
