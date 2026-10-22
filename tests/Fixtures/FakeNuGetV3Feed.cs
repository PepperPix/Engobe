namespace Kiln.Tests;

using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kiln.Services;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

internal sealed class FakeNuGetV3Feed : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<string, byte[]> _responses = new(StringComparer.Ordinal);
    private readonly Task _server;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"kiln-v3-{Guid.NewGuid():N}");

    public FakeNuGetV3Feed()
    {
        Directory.CreateDirectory(_root);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Source = $"http://127.0.0.1:{port}/index.json";
        var baseAddress = $"http://127.0.0.1:{port}/flat/";
        _responses["/index.json"] = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = "3.0.0",
            resources = new[] { new Dictionary<string, string> { ["@id"] = baseAddress, ["@type"] = "PackageBaseAddress/3.0.0" } },
        });
        _server = ServeAsync();
    }

    public string Source { get; }

    public bool Offline { get; set; }

    public ConcurrentDictionary<string, int> Requests { get; } = new(StringComparer.Ordinal);

    public async Task<NuGetPluginClient> CreateClientAsync()
    {
        var repository = Repository.Factory.GetCoreV3(Source);
        var http = await repository.GetResourceAsync<HttpSourceResource>()
            ?? throw new InvalidOperationException("The fake V3 feed has no HTTP resource.");
        http.HttpSource.HttpCacheDirectory = Path.Combine(_root, "cache");
        return new NuGetPluginClient(repository);
    }

    public byte[] AddPackage(string id, string version, string? hash = "match")
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, $"{id}.nuspec",
                $"<package><metadata><id>{id}</id><version>{version}</version><authors>Test</authors>" +
                "<description>Test plugin</description><tags>kiln-plugin</tags></metadata></package>");
            WriteEntry(zip, "content/plugin.yaml", $"name: verified\nversion: {version}\n");
            WriteEntry(zip, "content/static/plugin.js", $"console.log('{version}');");
        }
        var bytes = stream.ToArray();
        SetPackageBytes(id, version, bytes, hash);
        return bytes;
    }

    public void SetPackageBytes(string id, string version, byte[] bytes, string? hash)
    {
        var normalizedId = id.ToLowerInvariant();
        var prefix = $"/flat/{normalizedId}/{version}/{normalizedId}.{version}.nupkg";
        _responses[prefix] = bytes;
        _responses[$"/flat/{normalizedId}/index.json"] = JsonSerializer.SerializeToUtf8Bytes(new { versions = new[] { version } });
        if (hash is not null)
            _responses[$"{prefix}.sha512"] = Encoding.UTF8.GetBytes(hash == "match" ? Convert.ToBase64String(SHA512.HashData(bytes)) : hash);
        else
            _responses.TryRemove($"{prefix}.sha512", out _);
    }

    public void ExpireCache()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(_root, "cache"), "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-2));
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        _listener.Dispose();
        await _server;
        _stop.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private async Task ServeAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                if (Offline)
                    continue;
                await RespondAsync(client);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // The fixture cancels its accept loop during disposal.
        }
        catch (SocketException) when (_stop.IsCancellationRequested)
        {
            // Stopping the listener may interrupt an outstanding accept.
        }
    }

    private async Task RespondAsync(TcpClient client)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var line = await reader.ReadLineAsync(_stop.Token);
        if (line is null)
            return;
        var path = line.Split(' ')[1];
        Requests.AddOrUpdate(path, 1, (_, count) => count + 1);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_stop.Token)))
        {
            // Read through the request headers before writing the response.
        }
        var found = _responses.TryGetValue(path, out var body);
        body ??= [];
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(found ? "200 OK" : "404 Not Found")}\r\nContent-Length: {body.Length.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "Content-Type: application/octet-stream\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, _stop.Token);
        await stream.WriteAsync(body, _stop.Token);
    }

    private static void WriteEntry(ZipArchive archive, string path, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open());
        writer.Write(text);
    }
}
