using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Chisel;
using Spectre.Console.Cli;

namespace nugraph;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global", Justification = "Required for Spectre.Console.Cli binding")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Required for Spectre.Console.Cli binding")]
internal class GraphCommandSettings : CommandSettings
{
    // TODO: Support only one package? Users can create a project with multiple package reference.
    [CommandArgument(0, "[SOURCE]")]
    [Description("The source of the graph. Can be either a directory containing a .NET project, a .NET project file (csproj) or names of NuGet packages.")]
    public string[] Sources { get; init; } = [];

    // TODO: perform of NuGet package id (including version) in the Validate method
    internal FileOrPackages GetSource()
    {
        if (Sources.Length == 0)
            return (FileSystemInfo?)null;

        if (Sources.Length == 1)
        {
            var file = new FileInfo(Sources[0]);
            if (file.Exists)
            {
                return file;
            }

            var directory = new DirectoryInfo(Sources[0]);
            if (directory.Exists)
            {
                return directory;
            }
        }

        return Sources;
    }

    [CommandOption("-V|--version")]
    [Description("Prints version information")]
    public bool PrintVersion { get; init; }

    [CommandOption("-o|--output <OUTPUT>")]
    [Description("The path to the dependency graph output file. If not specified, the dependency graph URL is written on the standard output and opened in the browser.")]
    public FileInfo? OutputFile { get; init; }

    [CommandOption("-f|--framework <FRAMEWORK>")]
    [Description("The target framework to consider when building the dependency graph.")]
    public string? Framework { get; init; }

    [CommandOption("-r|--runtime <RUNTIME_IDENTIFIER>")]
    [Description("The target runtime to consider when building the dependency graph.")]
    public string? RuntimeIdentifier { get; init; }

    // TODO: option to choose Mermaid with https://mermaid.live vs Graphviz/DOT with https://edotor.net

    // TODO: option to disable opening the url in the default web browser in case (thus only printing the URL on stdout)

    [CommandOption("-m|--mode <MERMAID_MODE>")]
    [Description($"The mode to use for the Mermaid Live Editor (https://mermaid.live). Possible values are [b]{nameof(MermaidEditorMode.View)}[/] and [b]{nameof(MermaidEditorMode.Edit)}[/]. " +
                 $"Used only when no output path is specified.")]
    [DefaultValue(MermaidEditorMode.View)]
    public MermaidEditorMode MermaidEditorMode { get; init; }

    [CommandOption("-d|--direction <GRAPH_DIRECTION>")]
    [Description($"The direction of the dependency graph. Possible values are [b]{nameof(GraphDirection.LeftToRight)}[/] and [b]{nameof(GraphDirection.TopToBottom)}[/]")]
    [DefaultValue(GraphDirection.LeftToRight)]
    public GraphDirection GraphDirection { get; init; }

    [CommandOption("-v|--include-version")]
    [Description("Include package versions in the dependency graph. E.g. [b]Serilog/3.1.1[/] instead of [b]Serilog[/]")]
    [DefaultValue(false)]
    public bool GraphIncludeVersions { get; init; }

    [CommandOption("-i|--ignore")]
    [Description("Packages to ignore in the dependency graph. May be used multiple times.")]
    public string[] GraphIgnore { get; init; } = [];

    // TODO: Add an option to control the minimum log level, default to Warning

    [CommandOption("--include-ignored-packages", IsHidden = true)]
    [Description("Include ignored packages in the dependency graph. Used for debugging.")]
    [DefaultValue(false)]
    public bool GraphWriteIgnoredPackages { get; init; }

    [CommandOption("--parallel", IsHidden = true)]
    [Description("The maximum degree of parallelism.")]
    [DefaultValue(16)]
    public int MaxDegreeOfParallelism { get; init; }
}