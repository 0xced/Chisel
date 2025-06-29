using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Chisel;

/// <summary>
/// Task that determines which package to remove from the build.
/// </summary>
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global", Justification = "For MSBuild")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "For MSBuild")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "For MSBuild")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "For MSBuild")]
public class Chisel : Task
{
    /// <summary>
    /// The project assets file path (project.assets.json).
    /// </summary>
    [Required]
    public string ProjectAssetsFile { get; set; } = "";

    /// <summary>
    /// The target framework.
    /// </summary>
    [Required]
    public string TargetFramework { get; set; } = "";

    /// <summary>
    /// The runtime identifier (rid).
    /// </summary>
    public string RuntimeIdentifier { get; set; } = "";

    /// <summary>
    /// The intermediate output path where the <see cref="GraphName"/> is saved.
    /// </summary>
    public string IntermediateOutputPath { get; set; } = "";

    /// <summary>
    /// The name of the project referencing Chisel. Used to produce a high quality warnings.
    /// </summary>
    public string ProjectName { get; set; } = "";

    /// <summary>
    /// The output type of the project referencing Chisel. Used to ensure Chisel is not used on a class library.
    /// </summary>
    public string OutputType { get; set; } = "";

    /// <summary>
    /// The list of resolved runtime assemblies (<c>RuntimeCopyLocalItems</c>).
    /// </summary>
    [Required]
    public ITaskItem[] RuntimeAssemblies { get; set; } = [];

    /// <summary>
    /// The list of resolved native libraries (<c>NativeCopyLocalItems</c>).
    /// </summary>
    [Required]
    public ITaskItem[] NativeLibraries { get; set; } = [];

    /// <summary>
    /// The optional dependency graph file name.
    /// If the file name ends with <c>.mmd</c> or <c>.mermaid</c> then a <a href="https://mermaid.js.org/syntax/flowchart.html">Mermaid graph</a> is written.
    /// Otherwise, a <a href="https://graphviz.org/doc/info/lang.html">Graphviz dot file</a> is written.
    /// Use <c>none</c> to disable writing the dependency graph file.
    /// </summary>
    public string GraphName { get; set; } = "";

    /// <summary>
    /// The dependency graph direction. Allowed values are <c>LeftToRight</c> and <c>TopToBottom</c>.
    /// </summary>
    public string GraphDirection { get; set; } = nameof(global::Chisel.GraphDirection.LeftToRight);

    /// <summary>
    /// The dependency graph title.
    /// Use <c>none</c> to have no title.
    /// </summary>
    public string GraphTitle { get; set; } = "";

    /// <summary>
    /// Include links to nuget.org for all dependencies in the generated dependency graph file.
    /// </summary>
    public bool GraphIncludeLinks { get; set; }

    /// <summary>
    /// Include the version numbers in the generated dependency graph file.
    /// </summary>
    public bool GraphIncludeVersions { get; set; }

    /// <summary>
    /// Writes ignored packages (<c>ChiselGraphIgnore</c>) to the dependency graph file in gray. Used for debugging.
    /// </summary>
    public bool GraphWriteIgnoredPackages { get; set; }

    /// <summary>
    /// The package references to ignore when building the dependency graph.
    /// </summary>
    public ITaskItem[] GraphIgnores { get; set; } = [];

    /// <summary>
    /// The package references to remove from the build.
    /// </summary>
    public ITaskItem[] ChiselPackages { get; set; } = [];

    /// <summary>
    /// The <c>RuntimeCopyLocalItems</c> to remove from the build.
    /// </summary>
    [Output]
    public ITaskItem[] RemoveRuntimeAssemblies { get; private set; } = [];

    /// <summary>
    /// The <c>NativeCopyLocalItems</c> to remove from the build.
    /// </summary>
    [Output]
    public ITaskItem[] RemoveNativeLibraries { get; private set; } = [];

    /// <summary>
    /// The path where the dependency graph was written or an empty array none was written.
    /// </summary>
    [Output]
    public ITaskItem[] Graph { get; private set; } = [];

    /// <summary>
    /// The total number of bytes from all the runtime assemblies and native libraries that were removed by Chisel.
    /// </summary>
    [Output]
    public long? BytesSaved { get; private set; }

    static Chisel() => AppDomain.CurrentDomain.AssemblyResolve += SdkAssemblyResolver.ResolveAssembly;

    /// <inheritdoc />
    public override bool Execute()
    {
        if (string.Equals(OutputType, "library", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogError($"Chisel can't be used on {ProjectName} because it's a class library (OutputType = {OutputType})");
            return false;
        }

        try
        {
            LogNuGetAssemblies();

            var graph = ProcessGraph();

            foreach (var (project, dependent, dependency) in graph.EnumerateUnsatisfiedProjectDependencies())
            {
                LogWarning("CHISEL007", $"{dependent.Name}/{dependent.Version} requires {dependency.Id} to satisfy {dependency.VersionRange} but {project.Version} does not");
            }

            if (GraphIncludeLinks)
            {
                graph.AddLinksAsync().GetAwaiter().GetResult();
            }

            WriteGraph(graph);
            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true, showDetail: true, null);
            return false;
        }
    }

    private void LogNuGetAssemblies()
    {
        var assemblyDescriptions = new[]
        {
            ("NuGet.ProjectModel", typeof(NuGet.ProjectModel.LockFile)),
            ("NuGet.LibraryModel", typeof(NuGet.LibraryModel.Library)),
            ("NuGet.Versioning", typeof(NuGet.Versioning.NuGetVersion)),
        };
        foreach (var (assemblyName, assemblyType) in assemblyDescriptions)
        {
            var message = $"Chisel is using {assemblyName} from {assemblyType.Assembly.Location}";
            SdkAssemblyResolver.DebugLog(message);
            if (Environment.GetEnvironmentVariable("CHISEL_DEBUG_FILE") != null)
            {
                LogWarning("CHISEL000", message);
            }
            Log.LogMessage(MessageImportance.Low, message);
        }
    }

    private DependencyGraph ProcessGraph()
    {
        var lockFile = new NuGet.ProjectModel.LockFileFormat().Read(ProjectAssetsFile);
        var copyLocalPackages = new HashSet<string>(RuntimeAssemblies.Select(NuGetPackageId).Concat(NativeLibraries.Select(NuGetPackageId)));
        var (packages, roots) = lockFile.ReadPackages(TargetFramework, RuntimeIdentifier, package => package.IsProjectReference || copyLocalPackages.Contains(package.Name));
        var graph = new DependencyGraph(packages, roots, GraphIgnores.Select(e => e.ItemSpec));
        var (removed, notFound, removedRoots) = graph.Remove(ChiselPackages.Select(e => e.ItemSpec));

        foreach (var packageName in notFound)
        {
            LogWarning("CHISEL002", $"The package {packageName} (defined in ChiselPackage) was not found in the dependency graph of {ProjectName}");
        }

        foreach (var packageName in removedRoots)
        {
            LogWarning("CHISEL003", $"The package {packageName} (defined in ChiselPackage) can't be removed because it's a direct dependency of {ProjectName}");
        }

        RemoveRuntimeAssemblies = RuntimeAssemblies.Where(item => removed.Contains(NuGetPackageId(item))).ToArray();
        RemoveNativeLibraries = NativeLibraries.Where(item => removed.Contains(NuGetPackageId(item))).ToArray();

        try
        {
            BytesSaved = RemoveRuntimeAssemblies.Concat(RemoveNativeLibraries).Sum(e => new FileInfo(e.ItemSpec).Length);
            Log.LogMessage($"Chisel saved {BytesSaved / (1024.0 * 1024):F1} MiB");
        }
        catch (Exception exception)
        {
            Log.LogWarningFromException(exception, showStackTrace: true);
        }

        return graph;
    }

    private void WriteGraph(DependencyGraph graph)
    {
        if (string.IsNullOrEmpty(GraphName) || GraphName.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (GraphName != Path.GetFileName(GraphName))
        {
            LogWarning("CHISEL020", $"The ChiselGraph property ({GraphName}) must be a file name that does not include a directory");
            return;
        }

        if (!Directory.Exists(IntermediateOutputPath))
        {
            LogWarning("CHISEL021", $"The IntermediateOutputPath property ({IntermediateOutputPath}) must point to an existing directory");
            return;
        }

        var graphPath = Path.Combine(IntermediateOutputPath, GraphName);
        try
        {
            using var graphStream = new FileStream(graphPath, FileMode.Create);
            using var writer = new StreamWriter(graphStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var isMermaid = Path.GetExtension(GraphName) is ".mmd" or ".mermaid";
            var graphWriter = isMermaid ? GraphWriter.Mermaid(writer) : GraphWriter.Graphviz(writer);
            graphWriter.Write(graph, GetGraphOptions());
            var graphItem = new TaskItem(graphPath);
            graphItem.SetMetadata("Format", graphWriter.FormatName);
            Graph = [ graphItem ];
        }
        catch (Exception exception)
        {
            Log.LogWarningFromException(exception, showStackTrace: true);
            try
            {
                File.Delete(graphPath);
            }
            catch (Exception deleteException)
            {
                Log.LogWarningFromException(deleteException, showStackTrace: true);
            }
        }
    }

    private GraphOptions GetGraphOptions()
    {
        if (!Enum.TryParse<GraphDirection>(GraphDirection, ignoreCase: true, out var direction))
        {
            LogWarning("CHISEL022", $"The ChiselGraphDirection property ({GraphDirection}) must be either {nameof(global::Chisel.GraphDirection.LeftToRight)} or {nameof(global::Chisel.GraphDirection.TopToBottom)}");
        }

        return new GraphOptions
        {
            Direction = direction,
            Title = string.Equals(GraphTitle, "none", StringComparison.OrdinalIgnoreCase) ? null : GraphTitle,
            IncludeVersions = GraphIncludeVersions,
            WriteIgnoredPackages = GraphWriteIgnoredPackages,
        };
    }

    private string NuGetPackageId(ITaskItem item)
    {
        var packageId = item.GetMetadata("NuGetPackageId");
        if (string.IsNullOrEmpty(packageId))
        {
            var metadataNames = string.Join(", ", item.MetadataNames.OfType<string>().Select(e => $"\"{e}\""));
            LogWarning("CHISEL001", $"\"{item.ItemSpec}\" should contain \"NuGetPackageId\" metadata but contains {metadataNames}");
        }
        return packageId;
    }

    private void LogWarning(string warningCode, string message)
    {
        Log.LogWarning(
            subcategory: default,
            warningCode: warningCode,
            helpKeyword: default,
            file: default,
            lineNumber: default,
            columnNumber: default,
            endLineNumber: default,
            endColumnNumber: default,
            message: message
        );
    }
}