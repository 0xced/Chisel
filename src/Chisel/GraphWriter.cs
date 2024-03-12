using System.IO;
using System.Linq;

namespace Chisel;

internal abstract class GraphWriter
{
    public static GraphWriter Graphviz(TextWriter writer) => new GraphvizWriter(writer);

    public static GraphWriter Mermaid(TextWriter writer) => new MermaidWriter(writer);

    protected readonly TextWriter Writer;

    protected GraphWriter(TextWriter writer) => Writer = writer;

    public void Write(DependencyGraph graph, GraphDirection graphDirection, bool writeIgnoredPackages)
    {
        WriteHeader(graphDirection);
        WriteEdges(graph, writeIgnoredPackages);
        Writer.WriteLine();
        WriteNodes(graph, writeIgnoredPackages);
        WriteFooter();
    }

    protected abstract void WriteHeader(GraphDirection graphDirection);
    protected abstract void WriteFooter();
    protected abstract void WriteNode(Package package);
    protected abstract void WriteEdge(Package package, Package dependency);

    private static bool FilterIgnored(Package package, bool writeIgnoredPackages) => writeIgnoredPackages || package.State != PackageState.Ignore;

    private void WriteNodes(DependencyGraph graph, bool writeIgnoredPackages)
    {
        foreach (var package in graph.Packages.Where(e => FilterIgnored(e, writeIgnoredPackages)).OrderBy(e => e.Id))
        {
            WriteNode(package);
        }
    }

    private void WriteEdges(DependencyGraph graph, bool writeIgnoredPackages)
    {
        foreach (var (package, dependencies) in graph.Dependencies.Select(e => (e.Key, e.Value)).Where(e => FilterIgnored(e.Key, writeIgnoredPackages)).OrderBy(e => e.Key.Id))
        {
            foreach (var dependency in dependencies.Where(e => FilterIgnored(e, writeIgnoredPackages)).OrderBy(e => e.Id))
            {
                WriteEdge(package, dependency);
            }
        }
    }
}