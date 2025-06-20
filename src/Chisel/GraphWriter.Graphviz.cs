using System.IO;

namespace Chisel;

internal sealed class GraphvizWriter(TextWriter writer) : GraphWriter(writer)
{
    public override string FormatName => "Graphviz";

    protected override void WriteHeader(bool hasProject, bool hasIgnored, bool hasRemoved, GraphOptions options)
    {
        Writer.WriteLine($"# {GetGeneratedByComment()}");
        Writer.WriteLine();
        Writer.WriteLine("digraph");
        Writer.WriteLine("{");

        if (options.Direction == GraphDirection.LeftToRight)
            Writer.WriteLine("  rankdir=LR");
        else if (options.Direction == GraphDirection.TopToBottom)
            Writer.WriteLine("  rankdir=TB");

        if (!string.IsNullOrWhiteSpace(options.Title))
        {
            var label = options.Title!.Replace("\"", "\\\"");
            Writer.WriteLine($"  label=\"{label}\"");
            Writer.WriteLine();
        }

        Writer.WriteLine($"  node [ fontname = \"Segoe UI, sans-serif\", shape = box, style = filled, {Color(options.Color.Default)} ]");
        Writer.WriteLine();
    }

    protected override void WriteFooter()
    {
        Writer.WriteLine("}");
    }

    protected override void WriteRoot(Package package, GraphOptions options)
    {
    }

    protected override void WriteNode(Package package, GraphOptions options)
    {
        Writer.Write($"  \"{GetPackageId(package, options)}\"");
        var color = package.State switch
        {
            PackageState.Ignore => options.Color.Ignored,
            PackageState.Remove => options.Color.Removed,
            _ => package.IsProjectReference ? options.Color.Project : (Color?)null,
        };

        if (package.IsRoot || color.HasValue || options.IncludeLinks)
        {
            Writer.Write(" [");
            if (package.IsRoot)
            {
                Writer.Write(" shape = hexagon, penwidth = 4");
            }
            if (color.HasValue)
            {
                if (package.IsRoot)
                {
                    Writer.Write(',');
                }
                Writer.Write($" {Color(color.Value)}");
            }
            if (options.IncludeLinks)
            {
                if (package.IsRoot || color.HasValue)
                {
                    Writer.Write(',');
                }
                Writer.Write($" href=\"https://www.nuget.org/packages/{package.Name}/{package.Version}\"");
            }
            Writer.Write(" ]");
        }

        Writer.WriteLine();
    }

    protected override void WriteEdge(Package package, Package dependency, GraphOptions options)
    {
        Writer.WriteLine($"  \"{GetPackageId(package, options)}\" -> \"{GetPackageId(dependency, options)}\"");
    }

    private static string Color(Color color)
    {
        var colorDefinition = $"fillcolor = {Fill(color)}, color = {Stroke(color)}";
        return string.IsNullOrEmpty(color.Text) ? colorDefinition : colorDefinition + $", fontcolor = {Text(color)}";
    }

    private static string Fill(Color color) => color.Fill.StartsWith("#") ? $"\"{color.Fill}\"" : color.Fill;

    private static string Stroke(Color color) => color.Stroke.StartsWith("#") ? $"\"{color.Stroke}\"" : color.Stroke;

    private static string Text(Color color) => color.Text?.StartsWith("#") == true ? $"\"{color.Text}\"" : color.Stroke;
}