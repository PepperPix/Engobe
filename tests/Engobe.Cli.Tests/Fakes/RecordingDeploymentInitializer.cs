namespace Engobe.Cli.Tests.Fakes;

using Engobe.Models;
using Engobe.Services;

public sealed class RecordingDeploymentInitializer : IDeploymentInitializer
{
    public DeploymentInitOptions? CapturedOptions { get; private set; }

    public DeploymentInitResult? Result { get; set; }

    public DeploymentInitResult Initialize(DeploymentTarget target, string projectPath, CancellationToken cancellationToken = default)
        => Initialize(target, projectPath, new DeploymentInitOptions(), cancellationToken);

    public DeploymentInitResult Initialize(
        DeploymentTarget target,
        string projectPath,
        DeploymentInitOptions options,
        CancellationToken cancellationToken = default)
    {
        CapturedOptions = options;
        return Result ?? new DeploymentInitResult(target, []);
    }
}
