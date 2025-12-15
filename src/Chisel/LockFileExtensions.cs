using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Chisel;

internal static class LockFileExtensions
{
    public static (IReadOnlyDictionary<string, Package> Packages, IReadOnlyCollection<Package> Roots) ReadPackages(this LockFile lockFile, string tfm, string? rid, Predicate<Package>? filter = null)
    {
        var runtimeIdentifier = string.IsNullOrEmpty(rid) ? null : rid;
        var frameworks = lockFile.PackageSpec?.TargetFrameworks?.Where(e => e.TargetAlias == tfm).ToList() ?? [];
        var framework = frameworks.Count switch
        {
            0 => throw new ArgumentException($"Target framework \"{tfm}\" is not available in assets at \"{lockFile.Path}\" (JSON path: project.frameworks.*.targetAlias)", nameof(tfm)),
            1 => frameworks[0],
            _ => throw new ArgumentException($"Multiple target frameworks are matching \"{tfm}\" in assets at \"{lockFile.Path}\" (JSON path: project.frameworks.*.targetAlias)", nameof(tfm)),
        };
        var targets = lockFile.Targets.Where(e => e.TargetFramework == framework.FrameworkName && e.RuntimeIdentifier == runtimeIdentifier).ToList();
        // https://github.com/NuGet/NuGet.Client/blob/6.10.0.52/src/NuGet.Core/NuGet.ProjectModel/LockFile/LockFileTarget.cs#L17
        var targetId = framework.FrameworkName + (string.IsNullOrEmpty(runtimeIdentifier) ? "" : $"/{runtimeIdentifier}");
        var target = targets.Count switch
        {
            0 => throw new ArgumentException($"Target \"{targetId}\" is not available in assets at \"{lockFile.Path}\" (JSON path: targets)", nameof(rid)),
            1 => targets[0],
            _ => throw new ArgumentException($"Multiple targets are matching \"{targetId}\" in assets at \"{lockFile.Path}\" (JSON path: targets)", nameof(rid)),
        };
        var packages = target.Libraries.Where(e => e.Name != null && e.Version != null).Select(CreatePackage).Where(e => filter == null || filter(e)).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var projectDependencies = lockFile.ProjectFileDependencyGroups.Where(e => NuGetFramework.Parse(e.FrameworkName) == framework.FrameworkName).SelectMany(e => e.Dependencies).Select(ParseProjectFileDependency);
        var packageDependencies = framework.GetDependencies().Select(e => e.Name);
        var roots = new HashSet<Package>(projectDependencies.Concat(packageDependencies).Where(e => packages.ContainsKey(e)).Select(e => packages[e]));
        foreach (var root in roots)
        {
            root.IsRoot = true;
        }
        return (packages, roots);
    }

    private static Package CreatePackage(LockFileTargetLibrary library)
    {
        var name = library.Name ?? throw new ArgumentException("The library must have a name", nameof(library));
        var version = library.Version ?? throw new ArgumentException($"The library \"{name}\" must have a version", nameof(library));
        // https://github.com/dotnet/sdk/blob/v8.0.202/documentation/specs/runtime-configuration-file.md#libraries-section-depsjson
        // > `type` - the type of the library. `package` for NuGet packages. `project` for a project reference. Can be other things as well.
        var isProjectReference = library.Type == LibraryType.Project;
        var dependencies = library.Dependencies.Select(e => new Dependency(e.Id, e.VersionRange)).ToList();
        return new Package(name, version, isProjectReference, dependencies);
    }

    private static string ParseProjectFileDependency(string dependency)
    {
        // Extract the dependency name by "reversing" NuGet.LibraryModel.LibraryRange.ToLockFileDependencyGroupString()
        // See https://github.com/NuGet/NuGet.Client/blob/6.9.1.3/src/NuGet.Core/NuGet.LibraryModel/LibraryRange.cs#L76-L115
        var spaceIndex = dependency.IndexOf(' ');
        return spaceIndex != -1 ? dependency[..spaceIndex] : dependency;
    }

    private static IEnumerable<LibraryDependency> GetDependencies(this TargetFrameworkInformation targetFrameworkInformation)
    {
        // Can't use targetFrameworkInformation.Dependencies because NuGet.ProjectModel 6.13.1 changed the `Dependencies` property type
        // from IList<LibraryDependency> to ImmutableArray<LibraryDependency>, leading to this exception (use the .NET SDK 9.0.200 with Chisel 1.1.1 to reproduce)
        // > Chisel.targets(12,5): Error  : MissingMethodException: Method not found: 'System.Collections.Generic.IList`1<NuGet.LibraryModel.LibraryDependency> NuGet.ProjectModel.TargetFrameworkInformation.get_Dependencies()'.
        // > at Chisel.LockFileExtensions.ReadPackages(LockFile lockFile, String tfm, String rid, Predicate`1 filter)
        // > at Chisel.Chisel.ProcessGraph() in /_/src/Chisel/Chisel.cs:line 178
        // > at Chisel.Chisel.Execute() in /_/src/Chisel/Chisel.cs:line 137
        var type = targetFrameworkInformation.GetType();
        var dependenciesProperty = type.GetProperty(nameof(targetFrameworkInformation.Dependencies)) ?? throw new MissingMemberException(type.FullName, nameof(targetFrameworkInformation.Dependencies));
        return (IEnumerable<LibraryDependency>)(dependenciesProperty.GetValue(targetFrameworkInformation) ?? (IEnumerable<LibraryDependency>)[]);
    }
}