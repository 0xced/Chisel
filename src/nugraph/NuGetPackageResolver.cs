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
using NuGet.Versioning;

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
    /// <param name="packageId">The NuGet package identifier. Can optionally contain a version, for example: <c>Serilog/3.1.1</c>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>
    /// The package identity. If no version is specified in the <paramref name="packageId"/> then the latest non-prerelease version of the package is used.
    /// If the package only has prerelease versions, then the latest prerelease version is used.
    /// Returns <see langword="null"/> if no package is found in any of the configured package sources.
    /// </returns>
    public async Task<FindPackageByIdDependencyInfo> ResolvePackageInfoAsync(string packageId, CancellationToken cancellationToken)
    {
        var request = GetPackageIdentityRequest(packageId);
        var packageSources = GetPackageSources(request);

        using var sourceCacheContext = new SourceCacheContext();
        foreach (var sourceRepository in packageSources.Select(e => Repository.Factory.GetCoreV3(e)))
        {
            var packageIdentity = await GetPackageIdentityAsync(request, sourceRepository, cancellationToken);
            if (packageIdentity != null)
            {
                var findPackageByIdResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
                return await findPackageByIdResource.GetDependencyInfoAsync(packageIdentity.Id, packageIdentity.Version, sourceCacheContext, _logger, cancellationToken);
            }
        }

        if (packageSources.Count == 1)
        {
            throw new Exception($"Package {packageId} was not found in {packageSources[0]}");
        }

        throw new Exception($"Package {packageId} was not found. The following sources were searched {string.Join(", ", packageSources.Select(e => e.ToString()))}");
    }

    private IList<PackageSource> GetPackageSources(PackageIdentityRequest request)
    {
        var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(_settings);
        if (packageSourceMapping.IsEnabled)
        {
            var sourceNames = packageSourceMapping.GetConfiguredPackageSources(request.Id);
            return _packageSources.Where(e => sourceNames.Contains(e.Name)).ToList();
        }

        return _packageSources;
    }

    private async Task<PackageIdentity?> GetPackageIdentityAsync(PackageIdentityRequest request, SourceRepository sourceRepository, CancellationToken cancellationToken)
    {
        var metadataResource = await sourceRepository.GetResourceAsync<MetadataResource>(cancellationToken);
        if (request.Version != null)
        {
            var identity = new PackageIdentity(request.Id, request.Version);
            _logger.LogDebug($"Verifying if {request} exists in {sourceRepository.PackageSource}");
            var exists = await metadataResource.Exists(identity, _sourceCacheContext, _logger, cancellationToken);
            _logger.LogDebug($"  => {request} {(exists ? "found" : "not found")}");
            return exists ? identity : null;
        }

        _logger.LogDebug($"Getting last release version of {request} in {sourceRepository.PackageSource}");
        var latestReleaseVersion = await metadataResource.GetLatestVersion(request.Id, includePrerelease: false, includeUnlisted: false, _sourceCacheContext, _logger, cancellationToken);
        _logger.LogDebug($"  => {request}{(latestReleaseVersion == null ? " not found" : $"/{latestReleaseVersion}")}");
        if (latestReleaseVersion is not null)
        {
            return new PackageIdentity(request.Id, latestReleaseVersion);
        }
        _logger.LogDebug($"Getting last pre-release version of {request} in {sourceRepository.PackageSource}");
        var latestPrereleaseVersion = await metadataResource.GetLatestVersion(request.Id, includePrerelease: true, includeUnlisted: false, _sourceCacheContext, _logger, cancellationToken);
        _logger.LogDebug($"  => {request}{(latestPrereleaseVersion == null ? " not found" : $"/{latestPrereleaseVersion}")}");
        return latestPrereleaseVersion is null ? null : new PackageIdentity(request.Id, latestPrereleaseVersion);
    }

    private static PackageIdentityRequest GetPackageIdentityRequest(string packageId)
    {
        var parts = packageId.Split('/');
        if (parts.Length == 2)
        {
            if (NuGetVersion.TryParse(parts[1], out var version))
            {
                return new PackageIdentityRequest(parts[0], version);
            }

            throw new ArgumentException($"Version {parts[1]} for package {parts[0]} is not a valid NuGet version.");
        }

        return new PackageIdentityRequest(packageId, Version: null);
    }

    private record PackageIdentityRequest(string Id, NuGetVersion? Version)
    {
        public override string ToString() => Version == null ? Id : $"{Id}/{Version}";
    }
}