namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PageBundleAssetCatalogTests
{
    [Test]
    public async Task EnumeratesVisibleFilesRecursivelyAndOmitsMarkdownFiles()
    {
        var root = CreateTempDirectory();
        try
        {
            await WriteFileAsync(root, "index.md").ConfigureAwait(false);
            await WriteFileAsync(root, "child/index.md").ConfigureAwait(false);
            await WriteFileAsync(root, "child/photo.png").ConfigureAwait(false);
            await WriteFileAsync(root, "notes/article.md").ConfigureAwait(false);
            await WriteFileAsync(root, "notes/data.zip").ConfigureAwait(false);
            await WriteFileAsync(root, "images/manual/README.MD").ConfigureAwait(false);
            await WriteFileAsync(root, "images/manual/diagram.svg").ConfigureAwait(false);
            await WriteFileAsync(root, "draft/index.md", "---\ndraft: true\n---\n").ConfigureAwait(false);
            await WriteFileAsync(root, "draft/preview.png").ConfigureAwait(false);
            await WriteFileAsync(root, "invalid-frontmatter/article.md", "not front matter").ConfigureAwait(false);
            await WriteFileAsync(root, "invalid-frontmatter/attachment.txt").ConfigureAwait(false);

            var catalog = new PageBundleAssetCatalog(root);

            await Assert.That(catalog.RelativePaths).IsEquivalentTo(
                [
                    Path.Combine("child", "photo.png"),
                    Path.Combine("notes", "data.zip"),
                    Path.Combine("images", "manual", "diagram.svg"),
                    Path.Combine("draft", "preview.png"),
                    Path.Combine("invalid-frontmatter", "attachment.txt")
                ]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task OmitsHiddenFilesAndDoesNotTraverseHiddenDirectories()
    {
        var root = CreateTempDirectory();
        try
        {
            await WriteFileAsync(root, "visible.txt").ConfigureAwait(false);
            await WriteFileAsync(root, ".secret").ConfigureAwait(false);
            await WriteFileAsync(root, "images/.metadata").ConfigureAwait(false);
            await WriteFileAsync(root, ".private/picture.jpg").ConfigureAwait(false);
            await WriteFileAsync(root, "images/.cache/x.jpg").ConfigureAwait(false);

            var catalog = new PageBundleAssetCatalog(root);

            await Assert.That(catalog.RelativePaths).IsEquivalentTo(["visible.txt"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task OmitsFileAndDirectorySymlinks()
    {
        var root = CreateTempDirectory();
        var targetDirectory = CreateTempDirectory();
        try
        {
            await WriteFileAsync(root, "visible.txt").ConfigureAwait(false);
            await WriteFileAsync(targetDirectory, "outside.txt").ConfigureAwait(false);
            File.CreateSymbolicLink(Path.Combine(root, "linked-file.txt"), Path.Combine(targetDirectory, "outside.txt"));
            Directory.CreateSymbolicLink(Path.Combine(root, "linked-directory"), targetDirectory);

            var catalog = new PageBundleAssetCatalog(root);

            await Assert.That(catalog.RelativePaths).IsEquivalentTo(["visible.txt"]);
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(targetDirectory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kiln-bundle-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task WriteFileAsync(string root, string relativePath, string content = "asset")
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
    }
}
