﻿using System.IO;
using System.Linq;

namespace Chisel;

internal abstract class GraphWriter
{
    public static GraphWriter Graphviz(TextWriter writer) => new GraphvizWriter(writer);

    public static GraphWriter Mermaid(TextWriter writer) => new MermaidWriter(writer);

    protected readonly TextWriter Writer;

    protected GraphWriter(TextWriter writer) => Writer = writer;

    public void Write(DependencyGraph graph, GraphOptions options)
    {
        WriteHeader(options);
        WriteEdges(graph, options);
        Writer.WriteLine();
        WriteNodes(graph, options);
        WriteFooter();
    }

    protected abstract void WriteHeader(GraphOptions options);
    protected abstract void WriteFooter();
    protected abstract void WriteNode(Package package, GraphOptions options);
    protected abstract void WriteEdge(Package package, Package dependency, GraphOptions options);

    protected static string GetPackageId(Package package, GraphOptions options) => options.IncludeVersions ? package.Id : package.Name;

    private static bool FilterIgnored(Package package, GraphOptions options) => options.WriteIgnoredPackages || package.State != PackageState.Ignore;

    private void WriteNodes(DependencyGraph graph, GraphOptions options)
    {
        foreach (var package in graph.Packages.Where(e => FilterIgnored(e, options)).OrderBy(e => e.Id))
        {
            WriteNode(package, options);
        }
    }

    private void WriteEdges(DependencyGraph graph, GraphOptions options)
    {
        foreach (var (package, dependencies) in graph.Dependencies.Select(e => (e.Key, e.Value)).Where(e => FilterIgnored(e.Key, options)).OrderBy(e => e.Key.Id))
        {
            foreach (var dependency in dependencies.Where(e => FilterIgnored(e, options)).OrderBy(e => e.Id))
            {
                WriteEdge(package, dependency, options);
            }
        }
    }
}