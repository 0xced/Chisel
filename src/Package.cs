using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Chisel;

[DebuggerDisplay("{Name}/{Version}")]
internal sealed class Package : IEquatable<Package>
{
    public Package(string name, string version, IReadOnlyCollection<string> dependencies)
    {
        Name = name;
        Version = version;
        Dependencies = dependencies;
    }

    public string Name { get; }
    public string Version { get; }
    public IReadOnlyCollection<string> Dependencies { get; }

    public string Id => $"{Name}/{Version}";

    public bool Keep { get; set; } = true;

    public override string ToString() => Name;

    public bool Equals(Package? other) => PackageComparer.Instance.Equals(this, other);

    public override bool Equals(object? other) => PackageComparer.Instance.Equals(this, other as Package);

    public override int GetHashCode() => PackageComparer.Instance.GetHashCode(this);
}