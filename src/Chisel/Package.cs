using System;
using System.Collections.Generic;
using System.Diagnostics;
using NuGet.Versioning;

namespace Chisel;

[DebuggerDisplay("{Name}/{Version}")]
internal sealed class Package(string name, NuGetVersion version, bool isProjectReference, IReadOnlyCollection<Dependency> dependencies) : IEquatable<Package>
{
    public string Name { get; } = name;
    public NuGetVersion Version { get; } = version;
    public bool IsProjectReference { get; } = isProjectReference;
    public IReadOnlyCollection<Dependency> Dependencies { get; } = dependencies;

    public bool IsRoot { get; set; }

    public Uri? Link { get; set; }

    public PackageState State { get; set; } = PackageState.Keep;

    public override string ToString() => Name;

    public bool Equals(Package? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is Package other && Equals(other);

    public override int GetHashCode() => Name.GetHashCode();
}

internal sealed record Dependency(string Id, VersionRange VersionRange);