using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Chisel;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Cli;

namespace nugraph;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global", Justification = "Required for Spectre.Console.Cli binding")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Required for Spectre.Console.Cli binding")]
internal class GraphCommandSettings : CommandSettings
{
    [CommandArgument(0, "[SOURCE]")]
    [Description("The source of the graph. Can be either a directory containing a .NET project, a .NET project file (csproj) or the name of a NuGet package, " +
                 "optionally with a specific version, e.g. [b]Newtonsoft.Json/13.0.3[/].")]
    public string? SourceInput { get; init; }

    public FileOrPackage Source { get; private set; } = (FileSystemInfo?)null;

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

    [CommandOption("-l|--log <LEVEL>")]
    [Description($"The NuGet operations log level. Possible values are [b]{nameof(LogLevel.Debug)}[/], [b]{nameof(LogLevel.Verbose)}[/], [b]{nameof(LogLevel.Information)}[/], [b]{nameof(LogLevel.Minimal)}[/], [b]{nameof(LogLevel.Warning)}[/] and [b]{nameof(LogLevel.Error)}[/]")]
#if DEBUG
    [DefaultValue(LogLevel.Debug)]
#else
    [DefaultValue(LogLevel.Warning)]
#endif
    public LogLevel LogLevel { get; init; }

    [CommandOption("--nuget-root")]
    [Description("The NuGet root directory. Can be used to completely isolate nugraph from default NuGet operations.")]
    public string? NuGetRoot { get; init; }

    [CommandOption("--include-ignored-packages", IsHidden = true)]
    [Description("Include ignored packages in the dependency graph. Used for debugging.")]
    [DefaultValue(false)]
    public bool GraphWriteIgnoredPackages { get; init; }

    [CommandOption("--parallel", IsHidden = true)]
    [Description("The maximum degree of parallelism.")]
    [DefaultValue(16)]
    public int MaxDegreeOfParallelism { get; init; }

    public override ValidationResult Validate()
    {
        try
        {
            Source = GetSource();
            return base.Validate();
        }
        catch (Exception exception)
        {
            return ValidationResult.Error(exception.Message);
        }
    }


    private FileOrPackage GetSource()
    {
        if (SourceInput == null)
            return (FileSystemInfo?)null;

        var file = new FileInfo(SourceInput);
        if (file.Exists)
        {
            return file;
        }

        var directory = new DirectoryInfo(SourceInput);
        if (directory.Exists)
        {
            return directory;
        }

        return GetPackageIdentity(SourceInput);
    }

    private static PackageIdentity GetPackageIdentity(string packageId)
    {
        var parts = packageId.Split('/');
        if (parts.Length == 2)
        {
            if (NuGetVersion.TryParse(parts[1], out var version))
            {
                return new PackageIdentity(parts[0], version);
            }

            throw new ArgumentException($"Version {parts[1]} for package {parts[0]} is not a valid NuGet version.", nameof(packageId));
        }

        return new PackageIdentity(packageId, version: null);
    }
}