namespace Kiln.Services;

using System.Collections.ObjectModel;
using Kiln.Abstractions;
using Kiln.Models;

internal sealed record AssetPipelineRunContext(
    string OutputDir,
    AssetUrlResolver Resolver,
    Dictionary<string, string> Manifest,
    BuildOptions Options,
    bool SearchEnabled,
    IAssetMinifier Minifier,
    Collection<string> Warnings,
    Collection<string> Errors);
