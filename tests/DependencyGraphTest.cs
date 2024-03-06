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
    public async Task Graph01(bool writeIgnoredPackages)
    {
        var assetsFile = GetAssetsPath("Graph01.json");
        var graph = new DependencyGraph(assetsFile, tfm: "net8.0", rid: "", [ "Testcontainers.MongoDb" ]);
        var (removed, notFound, removedRoots) = graph.Remove([ "MongoDB.Driver", "AWSSDK.SecurityToken" ]);
        await using var writer = new StringWriter();
        graph.Write(writer, GraphDirection.LeftToRight, writeIgnoredPackages);

        removed.Should().BeEquivalentTo("AWSSDK.SecurityToken", "AWSSDK.Core");
        notFound.Should().BeEmpty();
        removedRoots.Should().BeEquivalentTo("MongoDB.Driver");

        await Verify(writer.ToString(), "gv").UseParameters(writeIgnoredPackages);
    }

    private static string GetAssetsPath(string file, [CallerFilePath] string path = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "ProjectAssets", file));
}