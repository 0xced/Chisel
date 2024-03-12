using System.IO;

namespace Chisel;

internal sealed class GraphvizWriter : GraphWriter
{
    public GraphvizWriter(TextWriter writer) : base(writer)
    {
    }

    protected override void WriteHeader(GraphDirection graphDirection)
    {
        Writer.WriteLine("# Generated by https://github.com/0xced/Chisel");
        Writer.WriteLine();
        Writer.WriteLine("digraph");
        Writer.WriteLine("{");

        if (graphDirection == GraphDirection.LeftToRight)
            Writer.WriteLine("  rankdir=LR");
        else if (graphDirection == GraphDirection.TopToBottom)
            Writer.WriteLine("  rankdir=TB");

        Writer.WriteLine("  node [ fontname = \"Segoe UI, sans-serif\", shape = box, style = filled, color = aquamarine ]");
        Writer.WriteLine();
    }

    protected override void WriteFooter()
    {
        Writer.WriteLine("}");
    }

    protected override void WriteNode(Package package)
    {
        Writer.Write($"  \"{package.Id}\"");
        if (package.State == PackageState.Ignore)
        {
            Writer.Write(" [ color = lightgray ]");
        }
        else if (package.State == PackageState.Remove)
        {
            Writer.Write(" [ color = lightcoral ]");
        }
        else if (package.Type == PackageType.Project)
        {
            Writer.Write(" [ color = skyblue ]");
        }
        else if (package.Type == PackageType.Unknown)
        {
            Writer.Write(" [ color = khaki ]");
        }

        Writer.WriteLine();
    }

    protected override void WriteEdge(Package package, Package dependency)
    {
        Writer.WriteLine($"  \"{package.Id}\" -> \"{dependency.Id}\"");
    }
}