namespace Vayu.AI.Online;

/// <summary>
/// The user's answer to a <see cref="CloudConsentRequest"/>. A cloud call may
/// proceed only on <see cref="AllowOnce"/>.
/// </summary>
public enum CloudConsentDecision
{
    /// <summary>Send this one request to the cloud provider. No standing permission is granted.</summary>
    AllowOnce = 0,

    /// <summary>Decline the cloud call and plan with the local model instead.</summary>
    UseLocalInstead = 1,

    /// <summary>Decline entirely; do nothing.</summary>
    Cancel = 2,
}
