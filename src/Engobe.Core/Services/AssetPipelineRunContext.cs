namespace Engobe.Services;

using System.Collections.ObjectModel;
using Engobe.Abstractions;
using Engobe.Models;

internal sealed record AssetPipelineRunContext(
    string OutputDir,
    AssetUrlResolver Resolver,
    Dictionary<string, string> Manifest,
    BuildOptions Options,
    bool SearchEnabled,
    IAssetMinifier Minifier,
    Collection<string> Warnings,
    Collection<string> Errors);
