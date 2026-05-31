namespace Vayu.AI.Online;

/// <summary>
/// Asks the user whether a specific cloud call may proceed. The online AI
/// layer must obtain <see cref="CloudConsentDecision.AllowOnce"/> from this
/// service before sending any private context to a provider (unless the user
/// has turned consent off, which is not the default).
/// </summary>
/// <remarks>
/// M3.1 ships the contract; the real WinUI consent dialog lands in M3.4.
/// Implementations must never log the prompt or any key, and must default to
/// the safe answer (<see cref="CloudConsentDecision.Cancel"/>) if the user
/// dismisses the dialog.
/// </remarks>
public interface ICloudConsentService
{
    /// <summary>Presents <paramref name="request"/> to the user and returns their decision.</summary>
    Task<CloudConsentDecision> RequestConsentAsync(CloudConsentRequest request, CancellationToken cancellationToken = default);
}
