using System;
using FluentAssertions;
using Xunit;

namespace Chisel.Tests;

public class SdkAssemblyResolverTest(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void LoadNuGetLibraryModel()
    {
        var sender = ("dotnet", (Action<string>)testOutputHelper.WriteLine);
        var assembly = SdkAssemblyResolver.ResolveAssembly(sender, new ResolveEventArgs("NuGet.LibraryModel, Version=6.9.1.3, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
        assembly.Should().NotBeNull();
    }

    [Fact]
    public void FailToLoadNuGetLibraryModel()
    {
        var sender = ("not-an-existing-command", (Action<string>)testOutputHelper.WriteLine);
        var assembly = SdkAssemblyResolver.ResolveAssembly(sender, new ResolveEventArgs("NuGet.LibraryModel, Version=6.9.1.3, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
        assembly.Should().BeNull();
    }
}