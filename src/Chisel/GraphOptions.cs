namespace Chisel;

internal struct GraphOptions
{
    public required GraphDirection Direction { get; init; }
    public required bool IncludeVersions { get; init; }
    public required bool WriteIgnoredPackages { get; init; }
}