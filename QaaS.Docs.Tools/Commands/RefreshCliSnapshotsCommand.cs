using System.Xml.Linq;
using QaaS.Docs.Tools.Infrastructure;

namespace QaaS.Docs.Tools.Commands;

/// <summary>
/// Rebuilds the committed Runner and Mocker CLI snapshot JSON files by compiling temporary host apps against the
/// current workspace sources.
/// </summary>
internal sealed class RefreshCliSnapshotsCommand : ICommandHandler
{
    public async Task<int> ExecuteAsync(DocsToolContext context, CommandArguments arguments)
    {
        var toolRoot = Path.Combine(context.DocsRoot, "tools", "QaaS.Docs.Generator");
        var runnerProject = Path.Combine(context.RunnerRoot, "QaaS.Runner", "QaaS.Runner.csproj");
        var runnerInfrastructureProject = Path.Combine(context.RunnerRoot, "QaaS.Runner.Infrastructure", "QaaS.Runner.Infrastructure.csproj");
        var mockerProject = Path.Combine(context.MockerRoot, "QaaS.Mocker", "QaaS.Mocker.csproj");
        var frameworkSolution = Path.Combine(context.FrameworkRoot, "QaaS.Framework.sln");
        var runnerSnapshotPath = Path.Combine(toolRoot, "Snapshots", "runner-cli.json");
        var mockerSnapshotPath = Path.Combine(toolRoot, "Snapshots", "mocker-cli.json");

        EnsureExists(runnerProject, "Runner project");
        EnsureExists(runnerInfrastructureProject, "Runner infrastructure project");
        EnsureExists(mockerProject, "Mocker project");
        EnsureExists(frameworkSolution, "Framework solution");

        var runnerFrameworkVersion = GetPackageReferenceVersion(runnerInfrastructureProject, "QaaS.Framework.Executions");
        var mockerFrameworkVersion = GetPackageReferenceVersion(mockerProject, "QaaS.Framework.Executions");
        if (string.IsNullOrWhiteSpace(runnerFrameworkVersion))
        {
            throw new InvalidOperationException($"Could not resolve QaaS.Framework.Executions package version from {runnerInfrastructureProject}");
        }

        if (string.IsNullOrWhiteSpace(mockerFrameworkVersion))
        {
            throw new InvalidOperationException($"Could not resolve QaaS.Framework.Executions package version from {mockerProject}");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"qaas-cli-snapshot-refresh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var frameworkFeedRoot = Path.Combine(tempRoot, "framework-feed");
            var restorePackagesRoot = Path.Combine(tempRoot, "packages");
            var restoreConfigPath = Path.Combine(tempRoot, "NuGet.Config");
            Directory.CreateDirectory(frameworkFeedRoot);
            Directory.CreateDirectory(restorePackagesRoot);

            await Utf8File.WriteAllTextAsync(
                restoreConfigPath,
                $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="framework-feed" value="{{XmlEscape(frameworkFeedRoot)}}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                  </packageSources>
                </configuration>
                """);

            foreach (var frameworkVersion in new[] { runnerFrameworkVersion, mockerFrameworkVersion }.Distinct(StringComparer.Ordinal))
            {
                await ProcessRunner.RunAsync(
                    "dotnet",
                    [
                        "pack",
                        frameworkSolution,
                        "-c",
                        "Release",
                        "-o",
                        frameworkFeedRoot,
                        $"-p:PackageVersion={frameworkVersion}",
                        $"-p:Version={frameworkVersion}"
                    ],
                    context.FrameworkRoot);
            }

            await InvokeSnapshotHostAsync(
                tempRoot,
                "runner-host",
                [runnerProject],
                restoreConfigPath,
                restorePackagesRoot,
                Path.Combine(context.ResourcesRoot, "RunnerSnapshotHost.cs"),
                runnerSnapshotPath);
            await InvokeSnapshotHostAsync(
                tempRoot,
                "mocker-host",
                [mockerProject],
                restoreConfigPath,
                restorePackagesRoot,
                Path.Combine(context.ResourcesRoot, "MockerSnapshotHost.cs"),
                mockerSnapshotPath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        Console.WriteLine("Refreshed runner and mocker CLI snapshots.");
        return 0;
    }

    private static async Task InvokeSnapshotHostAsync(
        string tempRoot,
        string hostDirectoryName,
        IReadOnlyList<string> projectReferences,
        string restoreConfigPath,
        string restorePackagesPath,
        string programTemplatePath,
        string outputPath)
    {
        var hostDirectory = Path.Combine(tempRoot, hostDirectoryName);
        Directory.CreateDirectory(hostDirectory);
        var projectPath = Path.Combine(hostDirectory, "SnapshotHost.csproj");
        var programPath = Path.Combine(hostDirectory, "Program.cs");

        var projectReferenceXml = string.Join(
            Environment.NewLine,
            projectReferences.Select(reference => $"    <ProjectReference Include=\"{XmlEscape(reference)}\" />"));
        await Utf8File.WriteAllTextAsync(
            projectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <RestorePackagesPath>{{XmlEscape(restorePackagesPath)}}</RestorePackagesPath>
              </PropertyGroup>
              <ItemGroup>
            {{projectReferenceXml}}
              </ItemGroup>
            </Project>
            """);
        await Utf8File.WriteAllTextAsync(programPath, await Utf8File.ReadAllTextAsync(programTemplatePath));

        await ProcessRunner.RunAsync(
            "dotnet",
            [
                "restore",
                projectPath,
                "--configfile",
                restoreConfigPath,
                "--packages",
                restorePackagesPath
            ],
            hostDirectory);
        await ProcessRunner.RunAsync(
            "dotnet",
            [
                "run",
                "--project",
                projectPath,
                "-c",
                "Release",
                "--no-restore",
                "--",
                "--output",
                outputPath
            ],
            hostDirectory);
    }

    private static string? GetPackageReferenceVersion(string projectPath, string packageId)
    {
        var project = XDocument.Load(projectPath);
        return project
            .Descendants("PackageReference")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Include"), packageId, StringComparison.Ordinal))?
            .Attribute("Version")?
            .Value;
    }

    private static void EnsureExists(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{description} not found at {path}", path);
        }
    }

    private static string XmlEscape(string value)
    {
        return System.Security.SecurityElement.Escape(value) ?? value;
    }
}
