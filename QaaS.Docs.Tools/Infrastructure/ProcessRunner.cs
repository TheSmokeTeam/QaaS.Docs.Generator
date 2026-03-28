using System.Diagnostics;
using System.Text;

namespace QaaS.Docs.Tools.Infrastructure;

/// <summary>
/// Runs external tools and captures their complete stdout and stderr for deterministic validation and diagnostics.
/// </summary>
internal static class ProcessRunner
{
    /// <summary>
    /// Runs an external process and returns its captured stdout, stderr, and formatted command text.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        bool throwOnFailure = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var entry in environmentVariables)
            {
                startInfo.Environment[entry.Key] = entry.Value;
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var result = new ProcessResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask,
            $"{fileName} {string.Join(" ", arguments.Select(EscapeArgument))}".Trim());

        if (throwOnFailure && result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.ToDisplayString());
        }

        return result;
    }

    /// <summary>
    /// Escapes a single argument for diagnostic output.
    /// </summary>
    private static string EscapeArgument(string argument)
    {
        return argument.Contains(' ', StringComparison.Ordinal)
            ? $"\"{argument}\""
            : argument;
    }
}

/// <summary>
/// Captures the outcome of an executed external process.
/// </summary>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, string CommandText)
{
    /// <summary>
    /// Formats the process result into a readable exception/error message.
    /// </summary>
    public string ToDisplayString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Command failed with exit code {ExitCode}: {CommandText}");

        if (!string.IsNullOrWhiteSpace(StandardOutput))
        {
            builder.AppendLine(StandardOutput.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(StandardError))
        {
            builder.AppendLine(StandardError.TrimEnd());
        }

        return builder.ToString().TrimEnd();
    }
}
