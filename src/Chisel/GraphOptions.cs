namespace Chisel;

internal struct GraphOptions
{
    public GraphOptions()
    {
        Color = new Colors
        {
            // Fill colors from https://graphviz.org/doc/info/colors.html and Stroke color chosen with https://paletton.com
            Default = new Color { Fill = "aquamarine", Stroke = "#009061", Text = "#333333" },
            Project = new Color { Fill = "skyblue", Stroke = "#05587C" },
            Private = new Color { Fill = "moccasin", Stroke = "#AF8844" },
            Removed = new Color { Fill = "lightcoral", Stroke = "#A42A2A" },
            Ignored = new Color { Fill = "lightgray", Stroke = "#7A7A7A" },
        };
    }

    public required GraphDirection Direction { get; init; }
    public required string? Title { get; init; }
    public required bool IncludeVersions { get; init; }
    public required bool WriteIgnoredPackages { get; init; }
    public Colors Color { get; }
}

internal struct Colors
{
    public required Color Default { get; init; }
    public required Color Project { get; init; }
    public required Color Private { get; init; }
    public required Color Removed { get; init; }
    public required Color Ignored { get; init; }
}

internal struct Color
{
    public required string Fill { get; init; }
    public required string Stroke { get; init; }
    public string? Text { get; init; }
}