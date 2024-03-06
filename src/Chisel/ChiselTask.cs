using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Chisel;

/// <summary>
/// Task that determines which package to remove from the build.
/// </summary>
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global", Justification = "For MSBuild")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "For MSBuild")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "For MSBuild")]
public class ChiselTask : Task
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
    /// The package references to remove from the build.
    /// </summary>
    public ITaskItem[] ChiselPackages { get; set; } = [];

    /// <summary>
    /// The package references to ignore when building the dependency graph.
    /// </summary>
    public ITaskItem[] ChiselIgnores { get; set; } = [];

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
    /// The intermediate output path where the <see cref="Graph"/> is saved.
    /// </summary>
    public string IntermediateOutputPath { get; set; } = "";

    /// <summary>
    /// The optional dependency graph file name.
    /// If the file name ends with <c>.svg</c> then Graphviz <c>dot</c> command line is used to produce a SVG file.
    /// Otherwise, a Graphviz <a href="https://graphviz.org/doc/info/lang.html">dot file</a> is written.
    /// Use <c>false</c> to disable writing the dependency graph.
    /// </summary>
    public string Graph { get; set; } = "";

    /// <summary>
    /// The dependency graph direction. Allowed values are <c>LeftToRight</c> and <c>TopToBottom</c>.
    /// </summary>
    public string GraphDirection { get; set; } = nameof(Chisel.GraphDirection.LeftToRight);

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
    public ITaskItem[] GraphPath { get; private set; } = [];

    /// <inheritdoc />
    public override bool Execute()
    {
        try
        {
            var graph = new DependencyGraph(ProjectAssetsFile, TargetFramework, RuntimeIdentifier, ChiselIgnores.Select(e => e.ItemSpec));
            var (removed, notFound, removedRoots) = graph.Remove(ChiselPackages.Select(e => e.ItemSpec));

            foreach (var packageName in notFound)
            {
                Log.LogWarning($"The package {packageName} (defined in ChiselPackages) was not found in the dependency graph");
            }

            foreach (var packageName in removedRoots)
            {
                Log.LogWarning($"The package {packageName} (defined in ChiselPackages) can't be removed from the dependency graph because it's a root");
            }

            RemoveRuntimeAssemblies = RuntimeAssemblies.Where(item => removed.Contains(item.GetMetadata("NuGetPackageId"))).ToArray();
            RemoveNativeLibraries = NativeLibraries.Where(item => removed.Contains(item.GetMetadata("NuGetPackageId"))).ToArray();

            if (string.IsNullOrEmpty(Graph) || Graph.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Graph != Path.GetFileName(Graph))
            {
                Log.LogWarning($"The ChiselGraph property ({Graph}) must be a file name that does not include a directory");
                return true;
            }

            if (!Directory.Exists(IntermediateOutputPath))
            {
                Log.LogWarning($"The IntermediateOutputPath property ({IntermediateOutputPath}) must point to an existing directory");
                return true;
            }

            var graphPath = Path.Combine(IntermediateOutputPath, Graph);
            try
            {
                if (!Enum.TryParse<GraphDirection>(GraphDirection, ignoreCase: true, out var graphDirection))
                {
                    Log.LogWarning($"The ChiselGraphDirection property ({GraphDirection}) must be either {nameof(Chisel.GraphDirection.LeftToRight)} or {nameof(Chisel.GraphDirection.TopToBottom)}");
                }
                using var graphStream = new FileStream(graphPath, FileMode.Create);
                using var writer = new StreamWriter(graphStream);
                graph.Write(writer, graphDirection);
                GraphPath = [ new TaskItem(graphPath) ];
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

            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true, showDetail: true, null);
            return false;
        }
    }
}