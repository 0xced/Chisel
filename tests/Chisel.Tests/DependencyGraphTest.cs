using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using static VerifyXunit.Verifier;

namespace Chisel.Tests;

public class DependencyGraphTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MongoDbGraph(bool writeIgnoredPackages)
    {
        HashSet<string> resolvedPackages =
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
        var assetsFile = GetAssetsPath("MongoDbGraph.json");
        var graph = new DependencyGraph(resolvedPackages, assetsFile, tfm: "net8.0", rid: "", ignores: [ "Testcontainers.MongoDb" ]);
        var (removed, notFound, removedRoots) = graph.Remove([ "MongoDB.Driver", "AWSSDK.SecurityToken" ]);
        await using var writer = new StringWriter();
        graph.Write(writer, GraphDirection.LeftToRight, writeIgnoredPackages);

        removed.Should().BeEquivalentTo("AWSSDK.SecurityToken", "AWSSDK.Core");
        notFound.Should().BeEmpty();
        removedRoots.Should().BeEquivalentTo("MongoDB.Driver");

        await Verify(writer.ToString(), "gv").UseParameters(writeIgnoredPackages);
    }

    [Fact]
    public async Task SqlClientGraph()
    {
        HashSet<string> resolvedPackages =
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
        var assetsFile = GetAssetsPath("SqlClientGraph.json");
        var graph = new DependencyGraph(resolvedPackages, assetsFile, tfm: "net8.0", rid: "win-x64", ignores: []);
        var (removed, notFound, removedRoots) = graph.Remove([ "Azure.Identity", "Microsoft.IdentityModel.JsonWebTokens", "Microsoft.IdentityModel.Protocols.OpenIdConnect" ]);
        await using var writer = new StringWriter();
        graph.Write(writer, writeIgnoredPackages: true);

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
            "System.Memory.Data",
            "System.Runtime.CompilerServices.Unsafe",
            "System.Security.AccessControl",
            "System.Security.Cryptography.Cng",
            "System.Security.Principal.Windows",
            "System.Text.Encodings.Web",
            "System.Text.Json",
        ]);
        notFound.Should().BeEmpty();
        removedRoots.Should().BeEmpty();

        await Verify(writer.ToString(), "gv");
    }

    private static string GetAssetsPath(string file, [CallerFilePath] string path = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "ProjectAssets", file));
}