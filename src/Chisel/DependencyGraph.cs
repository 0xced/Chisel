using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.ProjectModel;

namespace Chisel;

internal sealed class DependencyGraph
{
    private readonly HashSet<Package> _roots;
    private readonly Dictionary<Package, HashSet<Package>> _graph = new();
    private readonly Dictionary<Package, HashSet<Package>> _reverseGraph = new();

    private static Package CreatePackage(LockFileTargetLibrary library)
    {
        var name = library.Name ?? throw new ArgumentException("The library must have a name", nameof(library));
        var version = library.Version?.ToString() ?? throw new ArgumentException($"The library \"{name}\" must have a version", nameof(library));
        var type = library.Type switch
        {
            "package" => PackageType.Package,
            "project" => PackageType.Project,
            _ => PackageType.Unknown,
        };
        var dependencies = library.Dependencies.Select(e => e.Id).ToList();
        return new Package(name, version, type, dependencies);
    }

    public DependencyGraph(HashSet<string> resolvedPackages, string projectAssetsFile, string tfm, string rid, IEnumerable<string> ignores)
    {
        var assetsLockFile = new LockFileFormat().Read(projectAssetsFile);
        var frameworks = assetsLockFile.PackageSpec?.TargetFrameworks?.Where(e => e.TargetAlias == tfm).ToList() ?? [];
        var framework = frameworks.Count switch
        {
            0 => throw new ArgumentException($"Target framework \"{tfm}\" is not available in assets at \"{projectAssetsFile}\" (JSON path: project.frameworks)", nameof(tfm)),
            1 => frameworks[0],
            _ => throw new ArgumentException($"Multiple target frameworks are matching \"{tfm}\" in assets at \"{projectAssetsFile}\" (JSON path: project.frameworks)", nameof(tfm)),
        };
        var targets = assetsLockFile.Targets.Where(e => e.TargetFramework == framework.FrameworkName && (string.IsNullOrEmpty(rid) || e.RuntimeIdentifier == rid)).ToList();
        // https://github.com/NuGet/NuGet.Client/blob/6.10.0.52/src/NuGet.Core/NuGet.ProjectModel/LockFile/LockFileTarget.cs#L17
        var targetId = framework.FrameworkName + (string.IsNullOrEmpty(rid) ? "" : $"/{rid}");
        var target = targets.Count switch
        {
            0 => throw new ArgumentException($"Target \"{targetId}\" is not available in assets at \"{projectAssetsFile}\" (JSON path: targets)", nameof(rid)),
            1 => targets[0],
            _ => throw new ArgumentException($"Multiple targets are matching \"{targetId}\" in assets at \"{projectAssetsFile}\" (JSON path: targets)", nameof(rid)),
        };
        var relevantPackages = new HashSet<string>(resolvedPackages.Union(target.Libraries.Where(e => e.Type == "project").Select(e => e.Name ?? "")), StringComparer.OrdinalIgnoreCase);
        var packages = target.Libraries.Where(e => relevantPackages.Contains(e.Name ?? "")).ToDictionary(e => e.Name ?? "", CreatePackage, StringComparer.OrdinalIgnoreCase);

        foreach (var package in packages.Values)
        {
            var dependencies = new HashSet<Package>(package.Dependencies.Where(relevantPackages.Contains).Select(e => packages[e]));

            if (dependencies.Count > 0)
            {
                _graph.Add(package, dependencies);
            }

            foreach (var dependency in dependencies)
            {
                if (_reverseGraph.TryGetValue(dependency, out var reverseDependencies))
                {
                    reverseDependencies.Add(package);
                }
                else
                {
                    _reverseGraph[dependency] = [package];
                }
            }
        }

        _roots = new HashSet<Package>(framework.Dependencies.Where(e => relevantPackages.Contains(e.Name)).Select(e => packages[e.Name]).Except(_reverseGraph.Keys));

        foreach (var root in _roots)
        {
            _reverseGraph[root] = [ root ];
        }

        Ignore(ignores);
    }

    internal (HashSet<string> Removed, HashSet<string> NotFound, HashSet<string> RemovedRoots) Remove(IEnumerable<string> packageNames)
    {
        var notFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new HashSet<Package>();
        foreach (var packageName in packageNames.Distinct())
        {
            var package = _reverseGraph.Keys.SingleOrDefault(e => e.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
            if (package == null)
            {
                notFound.Add(packageName);
            }
            else
            {
                if (_roots.Contains(package))
                {
                    removedRoots.Add(package.Name);
                }
                else
                {
                    packages.Add(package);
                }
            }
        }

        foreach (var package in packages)
        {
            Remove(package);
            Restore(package, packages);
        }

        return ([.._reverseGraph.Keys.Where(e => e.State == PackageState.Remove).Select(e => e.Name)], notFound, removedRoots);
    }

    private void Ignore(IEnumerable<string> packageNames)
    {
        var packages = new HashSet<Package>(packageNames.Intersect(_reverseGraph.Keys.Select(p => p.Name), StringComparer.OrdinalIgnoreCase)
            .Select(e => _reverseGraph.Keys.Single(p => p.Name.Equals(e, StringComparison.OrdinalIgnoreCase))));

        foreach (var package in packages)
        {
            Ignore(package);
            Restore(package, packages);
        }
    }

    private void Remove(Package package) => UpdateDependencies(package, PackageState.Remove);
    private void Ignore(Package package) => UpdateDependencies(package, PackageState.Ignore);

    private void UpdateDependencies(Package package, PackageState state)
    {
        package.State = state;
        if (_graph.TryGetValue(package, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                UpdateDependencies(dependency, state);
            }
        }
    }

    private void Restore(Package package, HashSet<Package> excludePackages)
    {
        if ((_reverseGraph[package].Any(e => e.State == PackageState.Keep) && !excludePackages.Contains(package)) || _roots.Except(excludePackages).Contains(package))
        {
            package.State = PackageState.Keep;
        }

        if (_graph.TryGetValue(package, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                Restore(dependency, excludePackages);
            }
        }
    }

    public void Write(TextWriter writer, GraphDirection graphDirection = GraphDirection.LeftToRight, bool writeIgnoredPackages = false)
    {
        bool FilterIgnored(Package package) => writeIgnoredPackages || package.State != PackageState.Ignore;

        writer.WriteLine("# Generated by https://github.com/0xced/Chisel");
        writer.WriteLine("digraph");
        writer.WriteLine("{");

        if (graphDirection == GraphDirection.LeftToRight)
            writer.WriteLine("  rankdir=LR");
        else if (graphDirection == GraphDirection.TopToBottom)
            writer.WriteLine("  rankdir=TB");

        writer.WriteLine("  node [ fontname = \"Segoe UI, sans-serif\", shape = box, style = filled, color = aquamarine ]");
        writer.WriteLine();

        foreach (var package in _reverseGraph.Keys.Where(FilterIgnored).OrderBy(e => e.Id))
        {
            writer.Write($"  \"{package.Id}\"");
            if (package.State == PackageState.Ignore)
            {
                writer.Write(" [ color = lightgray ]");
            }
            else if (package.State == PackageState.Remove)
            {
                writer.Write(" [ color = lightcoral ]");
            }
            else if (package.Type == PackageType.Project)
            {
                writer.Write(" [ color = skyblue ]");
            }
            else if (package.Type == PackageType.Unknown)
            {
                writer.Write(" [ color = khaki ]");
            }
            writer.WriteLine();
        }
        writer.WriteLine();

        foreach (var (package, dependencies) in _graph.Select(e => (e.Key, e.Value)).Where(e => FilterIgnored(e.Key)).OrderBy(e => e.Key.Id))
        {
            foreach (var dependency in dependencies.Where(FilterIgnored).OrderBy(e => e.Id))
            {
                writer.WriteLine($"  \"{package.Id}\" -> \"{dependency.Id}\"");
            }
        }

        writer.WriteLine("}");
    }
}
