using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Chisel;

[DebuggerDisplay("{Name}/{Version}")]
internal sealed class Package : IEquatable<Package>
{
    public Package(string name, string version, PackageType type, IReadOnlyCollection<string> dependencies)
    {
        Name = name;
        Version = version;
        Type = type;
        Dependencies = dependencies;
    }

    public string Name { get; }
    public string Version { get; }
    public PackageType Type { get; }
    public IReadOnlyCollection<string> Dependencies { get; }

    public string Id => $"{Name}/{Version}";

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