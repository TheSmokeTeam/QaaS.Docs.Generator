using QaaS.Docs.Tools.Infrastructure;

namespace QaaS.Docs.Tools.Commands;

/// <summary>
/// Mirrors the stable schema download assets from the package mirror into the docs repository.
/// </summary>
internal sealed class SyncSchemaAssetsCommand : ICommandHandler
{
    public async Task<int> ExecuteAsync(DocsToolContext context, CommandArguments arguments)
    {
        var check = arguments.HasFlag("--check");
        var sourceRoot = Path.Combine(context.MirrorRoot, "schemas");
        var destinationRoot = Path.Combine(context.DocsRoot, "docs", "assets", "schemas");

        foreach (
            var obsoletePath in new[]
            {
                Path.Combine(destinationRoot, "index.json"),
                Path.Combine(destinationRoot, "runner-family"),
                Path.Combine(destinationRoot, "mocker-family"),
                Path.Combine(context.DocsRoot, "docs", "assets", "mirror-state"),
            }
        )
        {
            RemoveOrCheckObsoletePath(obsoletePath, check);
        }

        await CopyOrCheckFileAsync(
            Path.Combine(sourceRoot, "runner-family", "latest", "schema.json"),
            Path.Combine(destinationRoot, "runner-family-schema.json"),
            check
        );
        await CopyOrCheckFileAsync(
            Path.Combine(sourceRoot, "mocker-family", "latest", "schema.json"),
            Path.Combine(destinationRoot, "mocker-family-schema.json"),
            check
        );

        Console.WriteLine(
            check
                ? "Validated mirrored docs assets schema assets."
                : "Synced mirrored docs assets schema assets."
        );
        return 0;
    }

    private static async Task CopyOrCheckFileAsync(
        string sourcePath,
        string destinationPath,
        bool check
    )
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                $"Missing schema asset source file: {sourcePath}",
                sourcePath
            );
        }

        if (check)
        {
            if (!File.Exists(destinationPath))
            {
                throw new FileNotFoundException(
                    $"Missing copied schema asset: {destinationPath}",
                    destinationPath
                );
            }

            var sourceContent = Utf8File.NormalizeLineEndings(
                await Utf8File.ReadAllTextAsync(sourcePath)
            );
            var destinationContent = Utf8File.NormalizeLineEndings(
                await Utf8File.ReadAllTextAsync(destinationPath)
            );
            if (!string.Equals(sourceContent, destinationContent, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Schema asset drift detected: {destinationPath}"
                );
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void RemoveOrCheckObsoletePath(string path, bool check)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        if (check)
        {
            throw new InvalidOperationException(
                $"Obsolete mirrored docs asset still exists: {path}"
            );
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return;
        }

        File.Delete(path);
    }
}
