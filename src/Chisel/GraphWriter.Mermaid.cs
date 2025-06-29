﻿using System.Collections.Generic;
using System.IO;

namespace Chisel;

internal sealed class MermaidWriter(TextWriter writer) : GraphWriter(writer)
{
    private readonly HashSet<string> _rootEdges = [];

    public override string FormatName => "Mermaid";

    private static string ClassDef(string name, Color color)
    {
        var classDef = $"classDef {name} fill:{color.Fill},stroke:{color.Stroke}";
        return string.IsNullOrEmpty(color.Text) ? classDef : classDef + ",color:" + color.Text;
    }

    protected override void WriteHeader(bool hasProject, bool hasIgnored, bool hasRemoved, bool hasNuGetLink, GraphOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Title))
        {
            // Multi-line titles are not yet supported, anticipating https://github.com/mermaid-js/mermaid/pull/6444
            var title = options.Title!.Replace("\r", "").Replace("\n", "\\n");
            Writer.WriteLine("---");
            Writer.WriteLine($"title: {title}");
            Writer.WriteLine("---");
            Writer.WriteLine();
        }

        Writer.WriteLine($"%% {GetGeneratedByComment()}");
        Writer.WriteLine();
        Writer.Write("graph");
        if (options.Direction == GraphDirection.LeftToRight)
            Writer.Write(" LR");
        else if (options.Direction == GraphDirection.TopToBottom)
            Writer.Write(" TB");
        Writer.WriteLine();

        Writer.WriteLine();
        Writer.WriteLine("classDef root stroke-width:4px");
        Writer.WriteLine(ClassDef("default", options.Color.Default));
        if (hasProject)
            Writer.WriteLine(ClassDef("project", options.Color.Project));
        if (hasIgnored)
            Writer.WriteLine(ClassDef("ignored", options.Color.Ignored));
        if (hasRemoved)
            Writer.WriteLine(ClassDef("removed", options.Color.Removed));
        if (hasNuGetLink)
            Writer.WriteLine(ClassDef("private", options.Color.Private));
        Writer.WriteLine();
    }

    protected override void WriteFooter()
    {
    }

    protected override void WriteRoot(Package package, GraphOptions options)
    {
        var packageId = GetPackageId(package, options);
        Writer.WriteLine($"{packageId}{{{{{packageId}}}}}");
    }

    protected override void WriteNode(Package package, bool hasNuGetLink, GraphOptions options)
    {
        var packageId = GetPackageId(package, options);
        if (package.IsRoot)
        {
            Writer.WriteLine($"class {packageId} root");
        }
        var className = package.State switch
        {
            PackageState.Ignore => "ignored",
            PackageState.Remove => "removed",
            _ when package.IsProjectReference => "project",
            _ when hasNuGetLink && package.Link == null => "private",
            _ => "default",
        };
        Writer.WriteLine($"class {packageId} {className}");
        if (package.Link != null)
        {
            Writer.WriteLine($"click {packageId} \"{package.Link}\" \"{package.Name} {package.Version}\"");
        }
    }

    protected override void WriteEdge(Package package, Package dependency, GraphOptions options)
    {
        var packageId = GetPackageId(package, options);
        var source = package.IsRoot && _rootEdges.Add(packageId) ? $"{packageId}{{{{{packageId}}}}}" : packageId;
        Writer.WriteLine($"{source} --> {GetPackageId(dependency, options)}");
    }
}