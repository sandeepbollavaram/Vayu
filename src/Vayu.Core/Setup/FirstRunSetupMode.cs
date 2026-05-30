namespace Vayu.Core.Setup;

/// <summary>
/// The AI mode the user picks during the First Run Setup Wizard.
/// </summary>
public enum FirstRunSetupMode
{
    /// <summary>Default. Only the local AI provider (Ollama / llama.cpp) is used.</summary>
    OfflineOnly = 0,

    /// <summary>Only the online AI provider (Gemini) is used. Requires a configured key.</summary>
    OnlineOnly = 1,

    /// <summary>Local AI first; falls back to Gemini only when local confidence is below floor and the user consents.</summary>
    Hybrid = 2,
}
