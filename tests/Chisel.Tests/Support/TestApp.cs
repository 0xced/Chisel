using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Exceptions;
using FluentAssertions;
using NuGet.Frameworks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Chisel.Tests;

public class TestApp : IAsyncLifetime
{
    private readonly IMessageSink _messageSink;
    private readonly DirectoryInfo _workingDirectory;
    private readonly Dictionary<PublishMode, FileInfo> _executables;

    public TestApp(IMessageSink messageSink)
    {
        _messageSink = messageSink;
        var tfm = NuGetFramework.Parse(typeof(TestApp).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? throw new InvalidOperationException("TargetFrameworkAttribute not found"));
        _workingDirectory = GetDirectory("tests", $"TestApp-{tfm}");
        _workingDirectory.Create();
        foreach (var file in GetDirectory("tests", "TestApp").EnumerateFiles())
        {
            file.CopyTo(_workingDirectory.File(file.Name).FullName, overwrite: true);
        }
        _executables = new Dictionary<PublishMode, FileInfo>();
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await CreateTestAppAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        _workingDirectory.Delete(recursive: true);
        return Task.CompletedTask;
    }

    public string GetExecutablePath(PublishMode publishMode) => _executables[publishMode].FullName;

    public async Task<string> GetIntermediateOutputPathAsync()
    {
        var result = await RunDotnetAsync(_workingDirectory, "build", "--getProperty:IntermediateOutputPath", "--configuration", "Release");
        return Path.Combine(_workingDirectory.FullName, result.TrimEnd());
    }

    private async Task CreateTestAppAsync()
    {
        // It might be tempting to do pack -> restore -> build --no-restore -> publish --no-build (and parallelize over publish modes)
        // But this would fail because of https://github.com/dotnet/sdk/issues/17526 and probably because of other unforeseen bugs
        // preventing from running multiple `dotnet publish` commands with different parameters.

        await PackAsync();
        await RestoreAsync();
        foreach (var publishMode in Enum.GetValues<PublishMode>())
        {
            await PublishAsync(publishMode);
        }
    }

    private async Task PackAsync()
    {
        var projectFile = GetFile("src", "Chisel", "Chisel.csproj");
        var packArgs = new[] {
            "pack", projectFile.FullName,
            "--configuration", "Release",
            "--output", _workingDirectory.FullName,
            "-p:MinVerSkip=true",
            "-p:Version=0.0.0-IntegrationTest.0",
        };
        await RunDotnetAsync(_workingDirectory, packArgs);
    }

    private async Task RestoreAsync()
    {
        // Can't use "--source . --source https://api.nuget.org/v3/index.json" because of https://github.com/dotnet/sdk/issues/27202 => a nuget.config file is used instead.
        // It also has the benefit of using settings _only_ from the specified config file, ignoring the global nuget.config where package source mapping could interfere with the local source.
        var restoreArgs = new[] {
            "restore",
            "--configfile", "nuget.config",
            "-p:Configuration=Release",
        };
        await RunDotnetAsync(_workingDirectory, restoreArgs);
    }

    private async Task PublishAsync(PublishMode publishMode)
    {
        var publishDirectory = _workingDirectory.SubDirectory("publish");

        var outputDirectory = publishDirectory.SubDirectory(publishMode.ToString());

        var publishArgsBase = new[] {
            "publish",
            "--no-restore",
            "--configuration", "Release",
            "--output", outputDirectory.FullName,
        };
        var publishSingleFile = $"-p:PublishSingleFile={publishMode is PublishMode.SingleFile}";
        var publishArgs = publishArgsBase.Append(publishSingleFile).ToArray();
        await RunDotnetAsync(_workingDirectory, publishArgs);

        var executableFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "TestApp.exe" : "TestApp";
        var executableFile = new FileInfo(Path.Combine(outputDirectory.FullName, executableFileName));
        executableFile.Exists.Should().BeTrue();
        var dlls = executableFile.Directory!.EnumerateFiles("*.dll");
        if (publishMode == PublishMode.Standard)
        {
            dlls.Should().NotBeEmpty(because: $"the test app was _not_ published as single-file ({publishMode})");
        }
        else
        {
            dlls.Should().BeEmpty(because: $"the test app was published as single-file ({publishMode})");
            executableFile.Directory.EnumerateFiles().Should().ContainSingle().Which.FullName.Should().Be(executableFile.FullName);
        }

        _executables[publishMode] = executableFile;
    }

    private async Task<string> RunDotnetAsync(DirectoryInfo workingDirectory, params string[] arguments)
    {
        var outBuilder = new StringBuilder();
        var errBuilder = new StringBuilder();
        var command = Cli.Wrap("dotnet")
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(workingDirectory.FullName)
            .WithArguments(arguments)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
            {
                outBuilder.AppendLine(line);
                _messageSink.OnMessage(new DiagnosticMessage($"==> out: {line}"));
            }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                errBuilder.AppendLine(line);
                _messageSink.OnMessage(new DiagnosticMessage($"==> err: {line}"));
            }));

        _messageSink.OnMessage(new DiagnosticMessage($"📁 {workingDirectory.FullName} 🛠️ {command}"));

        var result = await command.ExecuteAsync();
        if (result.ExitCode != 0)
        {
            throw new CommandExecutionException(command, result.ExitCode, $"An unexpected exception has occurred while running {command}{Environment.NewLine}{errBuilder}{outBuilder}".Trim());
        }

        return outBuilder.ToString();
    }

    private static DirectoryInfo GetDirectory(params string[] paths) => new(GetFullPath(paths));

    private static FileInfo GetFile(params string[] paths) => new(GetFullPath(paths));

    private static string GetFullPath(params string[] paths) => Path.GetFullPath(Path.Combine(new[] { GetThisDirectory(), "..", "..", ".." }.Concat(paths).ToArray()));

    private static string GetThisDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
