using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Chisel;

internal static class DependencyGraphExtensions
{
    public static async Task AddLinksAsync(this DependencyGraph dependencyGraph, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        var repository = Repository.Factory.GetCoreV3(NuGetConstants.V3FeedUrl);
        var findPackageById = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        using var sourceCacheContext = new SourceCacheContext();
        var packages = dependencyGraph.Packages;

#if NETSTANDARD2_0
        var ct = cancellationToken;
        foreach (var package in packages)
#else
        await Parallel.ForEachAsync(packages, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 20 }, async (package, ct) =>
#endif
        {
            var packageExists = await findPackageById.DoesPackageExistAsync(package.Name, package.Version, sourceCacheContext, logger ?? NullLogger.Instance, ct);
            if (packageExists)
            {
                package.Link = new Uri($"https://www.nuget.org/packages/{package.Name}/{package.Version}");
            }
        }
#if !NETSTANDARD2_0
        );
#endif
    }
}