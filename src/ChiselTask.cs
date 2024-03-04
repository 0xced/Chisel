using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Chisel;

/// <summary>
/// Task that determines which package to remove from the build.
/// </summary>
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
            var graph = new DependencyGraph(ProjectAssetsFile, TargetFramework, RuntimeIdentifier);
            var removed = graph.Remove(ChiselPackages.Select(e => e.ItemSpec));

            RemoveRuntimeAssemblies = RuntimeAssemblies.Where(item => removed.Contains(item.GetMetadata("NuGetPackageId"))).ToArray();
            RemoveNativeLibraries = NativeLibraries.Where(item => removed.Contains(item.GetMetadata("NuGetPackageId"))).ToArray();

            if (string.IsNullOrEmpty(Graph) || Graph.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Graph != Path.GetFileName(Graph))
            {
                Log.LogWarning($"The ChiselGraph property ({Graph}) must be a file name that does not include a directory.");
                return true;
            }

            if (!Directory.Exists(IntermediateOutputPath))
            {
                Log.LogWarning($"The IntermediateOutputPath property ({IntermediateOutputPath}) must point to an existing directory.");
                return true;
            }

            var graphPath = Path.Combine(IntermediateOutputPath, Graph);
            try
            {
                using var output = new FileStream(graphPath, FileMode.Create);
                var format = Path.GetExtension(output.Name).Equals(".svg", StringComparison.OrdinalIgnoreCase) ? GraphFormat.Svg : GraphFormat.Dot;
                graph.WriteAsync(output, format).GetAwaiter().GetResult();
                GraphPath = [ new TaskItem(graphPath) ];
            }
            catch (Exception exception)
            {
                Log.LogWarningFromException(exception, showStackTrace: true);
                File.Delete(graphPath);
            }

            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: false, showDetail: true, null);
            return false;
        }
    }
}