namespace Linkfire.MusicLibrary.Application.Catalog;

/// <summary>
/// Raised when an external catalogue fails (network error, timeout, non-success status, unusable payload).
/// The API maps this to a controlled error response instead of leaking provider details.
/// </summary>
public sealed class MusicCatalogUnavailableException : Exception
{
    public MusicCatalogUnavailableException(string providerName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }
}
