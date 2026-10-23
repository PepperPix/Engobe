namespace Engobe.Cli.Tests.Fakes;

using Engobe.Models;
using Engobe.Services;

public sealed class FakeDeploymentInitializer : IDeploymentInitializer
{
    public DeploymentTarget? CapturedTarget { get; private set; }

    public Func<DeploymentInitResult>? ResultFactory { get; set; }

    public Exception? ThrowException { get; set; }

    public DeploymentInitResult Initialize(DeploymentTarget target, string projectPath, CancellationToken cancellationToken = default)
    {
        CapturedTarget = target;

        if (ThrowException is not null)
            throw ThrowException;

        return ResultFactory?.Invoke() ?? new DeploymentInitResult(target, []);
    }
}
