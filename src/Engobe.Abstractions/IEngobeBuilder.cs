namespace Engobe.Abstractions;

/// <summary>
/// Customizes the Engobe services registered with the dependency injection container.
/// </summary>
public interface IEngobeBuilder
{
    /// <summary>
    /// Replaces all registered <see cref="IAssetMinifier"/> services with a single minifier.
    /// </summary>
    /// <typeparam name="T">The minifier implementation, registered as a singleton.</typeparam>
    /// <returns>This builder, for chaining.</returns>
    IEngobeBuilder UseMinifier<T>() where T : class, IAssetMinifier;

    /// <summary>
    /// Registers an additional <see cref="IAssetProcessor"/> service.
    /// </summary>
    /// <typeparam name="T">The processor implementation, registered as a singleton.</typeparam>
    /// <returns>This builder, for chaining.</returns>
    IEngobeBuilder AddAssetProcessor<T>() where T : class, IAssetProcessor;
}
