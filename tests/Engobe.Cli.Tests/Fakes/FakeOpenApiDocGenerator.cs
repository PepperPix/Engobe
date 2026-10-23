namespace Engobe.Cli.Tests.Fakes;

using Engobe.Models;
using Engobe.Services;

public sealed class FakeOpenApiDocGenerator : IOpenApiDocGenerator
{
    public Func<DocGenReport>? ResultFactory { get; set; }

    public DocGenReport Generate(string specPath, string outputDir) =>
        ResultFactory?.Invoke() ?? new DocGenReport([], [], [], []);
}
