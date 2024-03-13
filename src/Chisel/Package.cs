using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Chisel;

[DebuggerDisplay("{Name}/{Version}")]
internal sealed class Package(string name, string version, PackageType type, IReadOnlyCollection<string> dependencies) : IEquatable<Package>
{
    public string Name { get; } = name;
    public string Version { get; } = version;
    public PackageType Type { get; } = type;
    public IReadOnlyCollection<string> Dependencies { get; } = dependencies;

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