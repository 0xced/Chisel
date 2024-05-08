using System;
using System.Collections.Generic;
using System.Linq;

namespace Chisel;

internal sealed class DependencyGraph
{
    private readonly IReadOnlyCollection<Package> _roots;
    /*
     * ┌───────────────────┐
     * │  Azure.Identity   │───────────────────────────┐
     * └───────────────────┘                           ▼
     *           │                         ┌───────────────────────┐
     *           │           ┌────────────▶│   System.Text.Json    │
     *           │           │             └───────────────────────┘
     *           │           │                         ▲
     *           │           │                         │
     *           │ ┌───────────────────┐   ┌───────────────────────┐
     *           └▶│    Azure.Core     │──▶│  System.Memory.Data   │
     *             └───────────────────┘   └───────────────────────┘
     *
     * _graph:        Key = Azure.Identity,   Values = [ Azure.Core, System.Text.Json ]
     * _reverseGraph: Key = System.Text.Json, Values = [ Azure.Identity, Azure.Core, System.Memory.Data ]
     */
    private readonly Dictionary<Package, HashSet<Package>> _graph = new();
    private readonly Dictionary<Package, HashSet<Package>> _reverseGraph = new();

    public DependencyGraph(IReadOnlyDictionary<string, Package> packages, IReadOnlyCollection<Package> roots, IEnumerable<string> ignores)
    {
        foreach (var package in packages.Values)
        {
            var dependencies = new HashSet<Package>(package.Dependencies.Where(e => packages.ContainsKey(e.Id)).Select(e => packages[e.Id]));

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

        _roots = roots;

        foreach (var root in _roots)
        {
            _reverseGraph[root] = [ root ];
        }

        Ignore(ignores);
    }

    public (HashSet<string> Removed, HashSet<string> NotFound, HashSet<string> RemovedRoots) Remove(IEnumerable<string> packageNames)
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

    public IEnumerable<(Package Project, Package Dependent, Dependency Dependency)> EnumerateUnsatisfiedProjectDependencies()
    {
        foreach (var (project, dependents) in _reverseGraph.Where(e => e.Key.IsProjectReference).Select(e => (e.Key, e.Value)))
        {
            foreach (var dependent in dependents)
            {
                foreach (var dependency in dependent.Dependencies.Where(e => e.Id == project.Name))
                {
                    if (!dependency.VersionRange.Satisfies(project.Version))
                    {
                        yield return (project, dependent, dependency);
                    }
                }
            }
        }
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

    public IEnumerable<Package> Packages => _reverseGraph.Keys;

    public IReadOnlyDictionary<Package, HashSet<Package>> Dependencies
    {
        get
        {
            var graph = new Dictionary<Package, HashSet<Package>>(_graph);
            foreach (var root in _roots.Where(root => !graph.ContainsKey(root)))
            {
                graph.Add(root, []);
            }
            return graph;
        }
    }
}
