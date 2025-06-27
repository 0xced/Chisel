using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;
using static VerifyXunit.Verifier;

namespace Chisel.Tests;

public class DependencyGraphTest
{
    private static readonly string[] MongoDbCopyLocalPackages =
    [
        "AWSSDK.Core",
        "AWSSDK.SecurityToken",
        "ByteSize",
        "BouncyCastle.Cryptography",
        "DnsClient",
        "Docker.DotNet",
        "Docker.DotNet.X509",
        "Microsoft.Bcl.AsyncInterfaces",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Win32.Registry",
        "MongoDB.Bson",
        "MongoDB.Driver.Core",
        "MongoDB.Driver",
        "MongoDB.Libmongocrypt",
        "Newtonsoft.Json",
        "SharpCompress",
        "SharpZipLib",
        "Snappier",
        "SSH.NET",
        "SshNet.Security.Cryptography",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Security.AccessControl",
        "System.Security.Principal.Windows",
        "System.Text.Encodings.Web",
        "System.Text.Json",
        "Testcontainers",
        "Testcontainers.MongoDb",
        "ZstdSharp.Port",
    ];

    private static readonly string[] SqlClientCopyLocalPackages =
    [
        "Azure.Core",
        "Azure.Identity",
        "Microsoft.Bcl.AsyncInterfaces",
        "Microsoft.Data.SqlClient",
        "Microsoft.Data.SqlClient.SNI.runtime",
        "Microsoft.Identity.Client.Extensions.Msal",
        "Microsoft.IdentityModel.Abstractions",
        "Microsoft.IdentityModel.JsonWebTokens",
        "Microsoft.IdentityModel.Logging",
        "Microsoft.IdentityModel.Protocols.OpenIdConnect",
        "Microsoft.IdentityModel.Protocols",
        "Microsoft.IdentityModel.Tokens",
        "Microsoft.SqlServer.Server",
        "System.Configuration.ConfigurationManager",
        "System.Diagnostics.DiagnosticSource",
        "System.Diagnostics.EventLog",
        "System.IdentityModel.Tokens.Jwt",
        "System.IO.FileSystem.AccessControl",
        "System.Memory.Data",
        "System.Runtime.Caching",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Security.AccessControl",
        "System.Security.Cryptography.Cng",
        "System.Security.Cryptography.ProtectedData",
        "System.Security.Principal.Windows",
        "System.Text.Encodings.Web",
        "System.Text.Json",
    ];

    [Theory]
    [CombinatorialData]
    public async Task MongoDbGraph(bool writeIgnoredPackages, [CombinatorialValues("graphviz", "mermaid")] string format)
    {
        var lockFile = new LockFileFormat().Read(GetAssetsPath("MongoDbGraph.json"));
        var (packages, roots) = lockFile.ReadPackages(tfm: "net8.0", rid: null, package => package.IsProjectReference || MongoDbCopyLocalPackages.Contains(package.Name));
        var graph = new DependencyGraph(packages, roots, ignores: [ "Testcontainers.MongoDb" ]);
        var (removed, notFound, removedRoots) = graph.Remove([ "MongoDB.Driver", "AWSSDK.SecurityToken", "NonExistentPackage" ]);
        await using var writer = new StringWriter();
        var graphWriter = format == "graphviz" ? GraphWriter.Graphviz(writer) : GraphWriter.Mermaid(writer);
        var graphOptions = new GraphOptions
        {
            Direction = GraphDirection.LeftToRight,
            Title = null,
            IncludeVersions = false,
            WriteIgnoredPackages = writeIgnoredPackages,
        };
        graphWriter.Write(graph, graphOptions);

        removed.Should().BeEquivalentTo("AWSSDK.SecurityToken", "AWSSDK.Core");
        notFound.Should().BeEquivalentTo("NonExistentPackage");
        removedRoots.Should().BeEquivalentTo("MongoDB.Driver");

        await Verify(writer.ToString(), format == "graphviz" ? "gv" : "mmd").UseParameters(writeIgnoredPackages, format);
    }

    [Theory]
    [InlineData("graphviz")]
    [InlineData("mermaid")]
    public async Task SqlClientGraph(string format)
    {
        var lockFile = new LockFileFormat().Read(GetAssetsPath("SqlClientGraph.json"));
        var (packages, roots) = lockFile.ReadPackages(tfm: "net8.0-windows", rid: "win-x64", package => package.IsProjectReference || SqlClientCopyLocalPackages.Contains(package.Name));
        var graph = new DependencyGraph(packages, roots, ignores: []);
        var (removed, notFound, removedRoots) = graph.Remove([ "Azure.Identity", "Microsoft.IdentityModel.JsonWebTokens", "Microsoft.IdentityModel.Protocols.OpenIdConnect", "System.Memory.Data" ]);
        await using var writer = new StringWriter();

        var graphWriter = format == "graphviz" ? GraphWriter.Graphviz(writer) : GraphWriter.Mermaid(writer);
        var graphOptions = new GraphOptions
        {
            Direction = GraphDirection.LeftToRight,
            Title = "Dependency graph of\r\n\"Microsoft.Data.SqlClient\"",
            IncludeVersions = true,
            WriteIgnoredPackages = false,
        };
        graphWriter.Write(graph, graphOptions);

        removed.Should().BeEquivalentTo([
            "Azure.Core",
            "Azure.Identity",
            "Microsoft.Bcl.AsyncInterfaces",
            "Microsoft.Identity.Client.Extensions.Msal",
            "Microsoft.IdentityModel.Abstractions",
            "Microsoft.IdentityModel.JsonWebTokens",
            "Microsoft.IdentityModel.Logging",
            "Microsoft.IdentityModel.Protocols",
            "Microsoft.IdentityModel.Protocols.OpenIdConnect",
            "Microsoft.IdentityModel.Tokens",
            "System.Diagnostics.DiagnosticSource",
            "System.IO.FileSystem.AccessControl",
            "System.IdentityModel.Tokens.Jwt",
            "System.Runtime.CompilerServices.Unsafe",
            "System.Security.AccessControl",
            "System.Security.Cryptography.Cng",
            "System.Security.Principal.Windows",
        ]);
        notFound.Should().BeEmpty();
        removedRoots.Should().BeEquivalentTo(["System.Memory.Data"]);

        await Verify(writer.ToString(), format == "graphviz" ? "gv" : "mmd").UseTextForParameters(format);
    }

    [Theory]
    [CombinatorialData]
    public async Task PollyGraphIgnoreGlob(bool includeLinks, [CombinatorialValues("graphviz", "mermaid")] string format)
    {
        var lockFile = new LockFileFormat().Read(GetAssetsPath("PollyGraph.json"));
        var (packages, roots) = lockFile.ReadPackages(tfm: "netstandard2.0", rid: "");
        var graph = new DependencyGraph(packages, roots, ignores: [ "System.*" ]);
        if (includeLinks)
        {
            foreach (var package in graph.Packages)
            {
                package.Link = new Uri($"https://www.nuget.org/packages/{package.Name}/{package.Version}");
            }
        }
        await using var writer = new StringWriter();

        var graphWriter = format == "graphviz" ? GraphWriter.Graphviz(writer) : GraphWriter.Mermaid(writer);
        var graphOptions = new GraphOptions
        {
            Direction = GraphDirection.LeftToRight,
            Title = null,
            IncludeVersions = true,
            WriteIgnoredPackages = false,
        };
        graphWriter.Write(graph, graphOptions);

        await Verify(writer.ToString(), format == "graphviz" ? "gv" : "mmd").UseParameters(includeLinks, format);
    }

    [Fact]
    public void ValidProjectVersion()
    {
        var lockFile = new LockFileFormat().Read(GetAssetsPath("SqlClientGraph.json"));
        var (packages, roots) = lockFile.ReadPackages(tfm: "net8.0-windows", rid: "win-x64", package => package.IsProjectReference || SqlClientCopyLocalPackages.Contains(package.Name));
        var graph = new DependencyGraph(packages, roots, ignores: []);

        graph.EnumerateUnsatisfiedProjectDependencies().Should().BeEmpty();
    }

    [Fact]
    public void InvalidProjectVersion()
    {
        var lockFile = new LockFileFormat().Read(GetAssetsPath("SqlClientGraph-InvalidProjectVersion.json"));
        var (packages, roots) = lockFile.ReadPackages(tfm: "net8.0-windows", rid: "win-x64", package => package.IsProjectReference || SqlClientCopyLocalPackages.Contains(package.Name));
        var graph = new DependencyGraph(packages, roots, ignores: []);

        var result = graph.EnumerateUnsatisfiedProjectDependencies().ToList();

        var versionRange = VersionRange.Parse("4.56.0");
        result.Should().BeEquivalentTo([
            (Package("Microsoft.Identity.Client"), Package("Azure.Identity"), new Dependency("Microsoft.Identity.Client", versionRange)),
            (Package("Microsoft.Identity.Client"), Package("Microsoft.Data.SqlClient"), new Dependency("Microsoft.Identity.Client", versionRange)),
            (Package("Microsoft.Identity.Client"), Package("Microsoft.Identity.Client.Extensions.Msal"), new Dependency("Microsoft.Identity.Client", versionRange))
        ]);
        result.Select(e => e.Project).Distinct().Should().ContainSingle().Which.Version.Should().Be(new NuGetVersion(1, 22, 333));

        // Package objects are compared by name only
        static Package Package(string name) => new(name, default!, default, default!);
    }

    private static string GetAssetsPath(string file, [CallerFilePath] string path = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "ProjectAssets", file));
}