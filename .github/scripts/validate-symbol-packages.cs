#:property StaticAnalysisCodeType=ProdExe
#:property RestorePackagesWithLockFile=false

using System.IO.Compression;

if (args.Length != 1)
{
    await Console.Error.WriteLineAsync("Usage: dotnet run --file validate-symbol-packages.cs -- <directory>").ConfigureAwait(false);
    return 1;
}

try
{
    var packages = Directory.GetFiles(args[0], "*.snupkg")
        .Order(StringComparer.Ordinal)
        .ToArray();
    if (packages.Length == 0)
        throw new InvalidDataException($"No symbol packages found in {args[0]}");

    foreach (var package in packages)
    {
        using var archive = await ZipFile.OpenReadAsync(package).ConfigureAwait(false);
        var symbols = archive.Entries
            .Where(entry => entry.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (symbols.Length == 0)
            throw new InvalidDataException($"{Path.GetFileName(package)}: no PDB files found");

        foreach (var symbol in symbols)
        {
            using var stream = await symbol.OpenAsync().ConfigureAwait(false);
            var signature = new byte[4];
            if (await stream.ReadAtLeastAsync(signature, signature.Length, throwOnEndOfStream: false).ConfigureAwait(false) != signature.Length
                || !signature.AsSpan().SequenceEqual("BSJB"u8))
            {
                throw new InvalidDataException(
                    $"{Path.GetFileName(package)}: non-portable PDB: {symbol.FullName}");
            }
        }

        await Console.Out.WriteLineAsync($"{Path.GetFileName(package)}: {symbols.Length} portable PDBs").ConfigureAwait(false);
    }

    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
{
    await Console.Error.WriteLineAsync($"Symbol package validation failed: {exception.Message}").ConfigureAwait(false);
    return 1;
}
