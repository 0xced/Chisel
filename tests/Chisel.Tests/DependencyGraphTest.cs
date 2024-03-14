using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Versioning;
using Xunit;
using static VerifyXunit.Verifier;

namespace Chisel.Tests;

public class DependencyGraphTest
{
    private static readonly string[] MongoDbResolvedPackages =
    [
        "AWSSDK.Core",
        "AWSSDK.SecurityToken",
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

    private static readonly string[] SqlClientResolvedPackages =
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task MongoDbGraph(bool writeIgnoredPackages)
    {
        var assetsFile = GetAssetsPath("MongoDbGraph.json");
        var graph = new DependencyGraph(MongoDbResolvedPackages, assetsFile, tfm: "net8.0", rid: "", ignores: [ "Testcontainers.MongoDb" ]);
        var (removed, notFound, removedRoots) = graph.Remove([ "MongoDB.Driver", "AWSSDK.SecurityToken", "NonExistentPackage" ]);
        await using var writer = new StringWriter();
        GraphWriter.Graphviz(writer).Write(graph, new GraphOptions { Direction = GraphDirection.LeftToRight, IncludeVersions = false, WriteIgnoredPackages = writeIgnoredPackages });

        removed.Should().BeEquivalentTo("AWSSDK.SecurityToken", "AWSSDK.Core");
        notFound.Should().BeEquivalentTo("NonExistentPackage");
        removedRoots.Should().BeEquivalentTo("MongoDB.Driver");

        await Verify(writer.ToString(), "gv").UseParameters(writeIgnoredPackages);
    }

    [Theory]
    [InlineData("graphviz")]
    [InlineData("mermaid")]
    public async Task SqlClientGraph(string graphFormat)
    {
        var assetsFile = GetAssetsPath("SqlClientGraph.json");
        var graph = new DependencyGraph(SqlClientResolvedPackages, assetsFile, tfm: "net8.0-windows", rid: "win-x64", ignores: []);
        var (removed, notFound, removedRoots) = graph.Remove([ "Azure.Identity", "Microsoft.IdentityModel.JsonWebTokens", "Microsoft.IdentityModel.Protocols.OpenIdConnect", "System.Memory.Data" ]);
        await using var writer = new StringWriter();

        var graphWriter = graphFormat == "graphviz" ? GraphWriter.Graphviz(writer) : GraphWriter.Mermaid(writer);
        graphWriter.Write(graph, new GraphOptions { Direction = GraphDirection.LeftToRight, IncludeVersions = true, WriteIgnoredPackages = false });

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

        await Verify(writer.ToString(), graphFormat == "graphviz" ? "gv" : "mmd").UseTextForParameters(graphFormat);
    }

    [Fact]
    public void ValidProjectVersion()
    {
        var assetsFile = GetAssetsPath("SqlClientGraph.json");
        var graph = new DependencyGraph(SqlClientResolvedPackages, assetsFile, tfm: "net8.0-windows", rid: "win-x64", ignores: []);

        graph.EnumerateUnsatisfiedProjectDependencies().Should().BeEmpty();
    }

    [Fact]
    public void InvalidProjectVersion()
    {
        var assetsFile = GetAssetsPath("SqlClientGraph-InvalidProjectVersion.json");
        var graph = new DependencyGraph(SqlClientResolvedPackages, assetsFile, tfm: "net8.0-windows", rid: "win-x64", ignores: []);

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