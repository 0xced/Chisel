using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace nugraph;

public class NuGetPackageResolver
{
    private readonly ISettings _settings;
    private readonly ILogger _logger;
    private readonly IList<PackageSource> _packageSources;
    private readonly SourceCacheContext _sourceCacheContext;

    public NuGetPackageResolver(ISettings settings, ILogger logger, IList<PackageSource> packageSources, SourceCacheContext sourceCacheContext)
    {
        _settings = settings;
        _logger = logger;
        _packageSources = packageSources;
        _sourceCacheContext = sourceCacheContext;
    }

    /// <summary>
    /// Resolves a NuGet package by searching the configured package sources.
    /// </summary>
    /// <param name="package">The NuGet package identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>
    /// The package identity. If no version is specified in the <paramref name="package"/> then the latest non-prerelease version of the package is used.
    /// If the package only has prerelease versions, then the latest prerelease version is used.
    /// Returns <see langword="null"/> if no package is found in any of the configured package sources.
    /// </returns>
    public async Task<FindPackageByIdDependencyInfo> ResolvePackageInfoAsync(PackageIdentity package, CancellationToken cancellationToken)
    {
        var packageSources = GetPackageSources(package);

        using var sourceCacheContext = new SourceCacheContext();
        foreach (var sourceRepository in packageSources.Select(e => Repository.Factory.GetCoreV3(e)))
        {
            var packageIdentity = await GetPackageIdentityAsync(package, sourceRepository, cancellationToken);
            if (packageIdentity != null)
            {
                var findPackageByIdResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
                return await findPackageByIdResource.GetDependencyInfoAsync(packageIdentity.Id, packageIdentity.Version, sourceCacheContext, _logger, cancellationToken);
            }
        }

        if (packageSources.Count == 1)
        {
            throw new Exception($"Package {package} was not found in {packageSources[0]}");
        }

        throw new Exception($"Package {package} was not found. The following sources were searched {string.Join(", ", packageSources.Select(e => e.ToString()))}");
    }

    private IList<PackageSource> GetPackageSources(PackageIdentity package)
    {
        var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(_settings);
        if (packageSourceMapping.IsEnabled)
        {
            var sourceNames = packageSourceMapping.GetConfiguredPackageSources(package.Id);
            return _packageSources.Where(e => sourceNames.Contains(e.Name)).ToList();
        }

        return _packageSources;
    }

    private async Task<PackageIdentity?> GetPackageIdentityAsync(PackageIdentity package, SourceRepository sourceRepository, CancellationToken cancellationToken)
    {
        var metadataResource = await sourceRepository.GetResourceAsync<MetadataResource>(cancellationToken);
        if (package.Version != null)
        {
            _logger.LogDebug($"Verifying if {package} exists in {sourceRepository.PackageSource}");
            var exists = await metadataResource.Exists(package, _sourceCacheContext, _logger, cancellationToken);
            _logger.LogDebug($"  => {package} {(exists ? "found" : "not found")}");
            return exists ? package : null;
        }

        _logger.LogDebug($"Getting last release version of {package} in {sourceRepository.PackageSource}");
        var latestReleaseVersion = await metadataResource.GetLatestVersion(package.Id, includePrerelease: false, includeUnlisted: false, _sourceCacheContext, _logger, cancellationToken);
        _logger.LogDebug($"  => {package}{(latestReleaseVersion == null ? " not found" : $"/{latestReleaseVersion}")}");
        if (latestReleaseVersion is not null)
        {
            return new PackageIdentity(package.Id, latestReleaseVersion);
        }
        _logger.LogDebug($"Getting last pre-release version of {package} in {sourceRepository.PackageSource}");
        var latestPrereleaseVersion = await metadataResource.GetLatestVersion(package.Id, includePrerelease: true, includeUnlisted: false, _sourceCacheContext, _logger, cancellationToken);
        _logger.LogDebug($"  => {package}{(latestPrereleaseVersion == null ? " not found" : $"/{latestPrereleaseVersion}")}");
        return latestPrereleaseVersion is null ? null : new PackageIdentity(package.Id, latestPrereleaseVersion);
    }
}