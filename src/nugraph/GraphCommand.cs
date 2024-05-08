using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chisel;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using OneOf;
using Spectre.Console;
using Spectre.Console.Cli;

namespace nugraph;

[GenerateOneOf]
internal partial class FileOrPackage : OneOfBase<FileSystemInfo?, PackageIdentity>
{
    public override string ToString() => Match(file => file?.FullName ?? Environment.CurrentDirectory, package => package.ToString());
}

[Description("Generates dependency graphs for .NET projects and NuGet packages.")]
internal class GraphCommand(IAnsiConsole console, CancellationToken cancellationToken) : CancelableCommand<GraphCommandSettings>(cancellationToken)
{
    protected override async Task<int> ExecuteAsync(CommandContext commandContext, GraphCommandSettings settings, CancellationToken cancellationToken)
    {
        var source = settings.Source;
        var graphUrl = await console.Status().StartAsync($"Generating dependency graph for {source}", async _ =>
        {
            var graph = await source.Match(
                file => ComputeDependencyGraphAsync(file, settings, cancellationToken),
                package => ComputeDependencyGraphAsync(package, settings, new SpectreLogger(console, settings.LogLevel), cancellationToken)
            );
            return await WriteGraphAsync(graph, settings);
        });

        if (graphUrl != null)
        {
            var url = graphUrl.ToString();
            console.WriteLine(url);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (settings.OutputFile != null)
        {
            console.MarkupLineInterpolated($"The {source} dependency graph has been written to [lime]{new Uri(settings.OutputFile.FullName)}[/]");
        }

        return 0;
    }

    private static async Task<DependencyGraph> ComputeDependencyGraphAsync(FileSystemInfo? source, GraphCommandSettings settings, CancellationToken cancellationToken)
    {
        var projectInfo = await Dotnet.RestoreAsync(source, cancellationToken);
        var targetFramework = settings.Framework ?? projectInfo.TargetFrameworks.First();
        var lockFile = new LockFileFormat().Read(projectInfo.ProjectAssetsFile);
        Predicate<Package> filter = projectInfo.CopyLocalPackages.Count > 0 ? package => projectInfo.CopyLocalPackages.Contains(package.Name) : _ => true;
        var (packages, roots) = lockFile.ReadPackages(targetFramework, settings.RuntimeIdentifier, filter);
        return new DependencyGraph(packages, roots, ignores: settings.GraphIgnore);
    }

    private static async Task<DependencyGraph> ComputeDependencyGraphAsync(PackageIdentity package, GraphCommandSettings settings, ILogger logger, CancellationToken cancellationToken)
    {
        var nugetSettings = Settings.LoadDefaultSettings(null);
        using var sourceCacheContext = new SourceCacheContext();
        var packageSources = GetPackageSources(nugetSettings, logger);
        var packageIdentityResolver = new NuGetPackageResolver(nugetSettings, logger, packageSources, sourceCacheContext);

        var packageInfo = await packageIdentityResolver.ResolvePackageInfoAsync(package, cancellationToken);

        var dependencyGraphSpec = new DependencyGraphSpec(isReadOnly: true);
        var projectName = $"dependency graph of {packageInfo.PackageIdentity}";
        // TODO: Figure out how to best guess which framework to use if none is specified.
        var targetFramework = packageInfo.DependencyGroups.Select(e => e.TargetFramework).OrderBy(e => e, NuGetFrameworkSorter.Instance).FirstOrDefault();
        var framework = settings.Framework == null ? targetFramework : NuGetFramework.Parse(settings.Framework);
        IList<TargetFrameworkInformation> targetFrameworks = [ new TargetFrameworkInformation { FrameworkName = framework } ];
        var projectSpec = new PackageSpec(targetFrameworks)
        {
            FilePath = projectName,
            Name = projectName,
            RestoreMetadata = new ProjectRestoreMetadata
            {
                ProjectName = projectName,
                ProjectPath = projectName,
                ProjectUniqueName = Guid.NewGuid().ToString(),
                ProjectStyle = ProjectStyle.PackageReference,
                // The output path is required, else we get NuGet.Commands.RestoreSpecException: Invalid restore input. Missing required property 'OutputPath' for project type 'PackageReference'.
                // But it won't be used anyway since restore is performed with RestoreRunner.RunWithoutCommit instead of RestoreRunner.RunAsync
                OutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), "nugraph"),
                OriginalTargetFrameworks = targetFrameworks.Select(e => e.ToString()).ToList(),
                Sources = packageSources,
            },
            Dependencies = [new LibraryDependency(new LibraryRange(packageInfo.PackageIdentity.Id, new VersionRange(packageInfo.PackageIdentity.Version), LibraryDependencyTarget.Package))],
        };
        dependencyGraphSpec.AddProject(projectSpec);
        dependencyGraphSpec.AddRestore(projectSpec.RestoreMetadata.ProjectUniqueName);

        var restoreCommandProvidersCache = new RestoreCommandProvidersCache();
        var dependencyGraphSpecRequestProvider = new DependencyGraphSpecRequestProvider(restoreCommandProvidersCache, dependencyGraphSpec, nugetSettings);
        var restoreContext = new RestoreArgs
        {
            CacheContext = sourceCacheContext,
            Log = logger,
            GlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(nugetSettings),
            PreLoadedRequestProviders = [ dependencyGraphSpecRequestProvider ],
        };

        var requests = await RestoreRunner.GetRequests(restoreContext);
        // TODO: Single() => how can I be sure? If only one request? And how can I be sure that there's only one request created out of the restore context?
        var restoreResultPair = (await RestoreRunner.RunWithoutCommit(requests, restoreContext)).Single();
        // TODO: filter log messages, only those with LogLevel == Error ?
        if (!restoreResultPair.Result.Success)
            throw new Exception(string.Join(Environment.NewLine, restoreResultPair.Result.LogMessages.Select(e => $"[{e.Code}] {e.Message}")));

        var lockFile = restoreResultPair.Result.LockFile;
        // TODO: build the package and roots out of restoreResultPair.Result.RestoreGraphs instead of the lock file?
        var (packages, roots) = lockFile.ReadPackages(targetFrameworks.First().TargetAlias, settings.RuntimeIdentifier);
        return new DependencyGraph(packages, roots, settings.GraphIgnore);
    }

    private static List<PackageSource> GetPackageSources(ISettings settings, ILogger logger)
    {
        var packageSourceProvider = new PackageSourceProvider(settings);
        var packageSources = packageSourceProvider.LoadPackageSources().Where(e => e.IsEnabled).Distinct().ToList();

        if (packageSources.Count == 0)
        {
            var officialPackageSource = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.NuGetHostName);
            packageSources.Add(officialPackageSource);
            var configFilePaths = settings.GetConfigFilePaths().Distinct();
            logger.LogWarning($"No NuGet sources could be found in {string.Join(", ", configFilePaths)}. Using {officialPackageSource}");
        }

        return packageSources;
    }

    private static async Task<Uri?> WriteGraphAsync(DependencyGraph graph, GraphCommandSettings settings)
    {
        await using var fileStream = settings.OutputFile?.OpenWrite();
        await using var memoryStream = fileStream == null ? new MemoryStream(capacity: 2048) : null;
        var stream = (fileStream ?? memoryStream as Stream)!;
        await using (var streamWriter = new StreamWriter(stream, leaveOpen: true))
        {
            var isMermaid = fileStream == null || Path.GetExtension(fileStream.Name) is ".mmd" or ".mermaid";
            var graphWriter = isMermaid ? GraphWriter.Mermaid(streamWriter) : GraphWriter.Graphviz(streamWriter);
            var graphOptions = new GraphOptions
            {
                Direction = settings.GraphDirection,
                IncludeVersions = settings.GraphIncludeVersions,
                WriteIgnoredPackages = settings.GraphWriteIgnoredPackages,
            };
            graphWriter.Write(graph, graphOptions);
        }

        return memoryStream == null ? null : Mermaid.GetLiveEditorUri(memoryStream.AsSpan(), settings.MermaidEditorMode);
    }
}