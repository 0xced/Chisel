using System;
using System.Collections.Generic;

namespace Chisel;

internal sealed class PackageComparer : IEqualityComparer<Package>
{
    public static PackageComparer Instance { get; } = new();

    private PackageComparer()
    {
    }

    public bool Equals(Package? x, Package? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(Package package) => package.Name.GetHashCode();
}