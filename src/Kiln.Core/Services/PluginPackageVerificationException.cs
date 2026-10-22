namespace Kiln.Services;

/// <summary>
/// Reports that a package cannot be verified while its source is unavailable.
/// </summary>
public sealed class PluginPackageVerificationException : InvalidOperationException
{
    /// <summary>
    /// Creates a package verification failure.
    /// </summary>
    public PluginPackageVerificationException()
        : this("Package hash verification failed.")
    {
    }

    /// <summary>
    /// Creates a package verification failure with a warning.
    /// </summary>
    public PluginPackageVerificationException(string message)
        : base(message)
    {
        Warning = message;
    }

    /// <summary>
    /// Creates a verification failure with a warning to display before the error.
    /// </summary>
    public PluginPackageVerificationException(string warning, Exception innerException)
        : base("Offline installation without a matching cached SHA-512 hash is not permitted.", innerException)
    {
        Warning = warning;
    }

    /// <summary>
    /// Gets the warning identifying the source and package that could not be verified.
    /// </summary>
    public string Warning { get; }
}
