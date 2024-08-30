using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    private string _packageVersion = "N/A";

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
        if (Environment.GetEnvironmentVariable("ContinuousIntegrationBuild") == "true")
        {
            // The NuGet package was already built as part of the tests (PackAsync),
            // so move it to the root of the repository for the "Upload NuGet package artifact" step to pick it.
            var packageName = $"Chisel.{_packageVersion}.nupkg";
            var packageFile = _workingDirectory.File(packageName);
            packageFile.MoveTo(GetFullPath(packageName), overwrite: false);
        }

        try
        {
            _workingDirectory.Delete(recursive: true);
        }
        catch (UnauthorizedAccessException exception)
        {
            // This sometimes happen on the Windows runner in GitHub actions
            // > [Test Class Cleanup Failure (Chisel.Tests.ChiseledAppTests)]: System.UnauthorizedAccessException : Access to the path 'TestApp.dll' is denied.
            _messageSink.OnMessage(new DiagnosticMessage($"Deleting {_workingDirectory} failed: {exception}"));
        }
        return Task.CompletedTask;
    }

    public string GetExecutablePath(PublishMode publishMode) => _executables[publishMode].FullName;

    public DirectoryInfo IntermediateOutputPath { get; private set; } = new(".");

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
            "--no-build",
            "--output", _workingDirectory.FullName,
            "--getProperty:PackageVersion",
        };
        var packageVersion = await RunDotnetAsync(_workingDirectory, packArgs);
        _packageVersion = packageVersion.TrimEnd();
    }

    private async Task RestoreAsync()
    {
        // Can't use "--source . --source https://api.nuget.org/v3/index.json" because of https://github.com/dotnet/sdk/issues/27202 => a nuget.config file is used instead.
        // It also has the benefit of using settings _only_ from the specified config file, ignoring the global nuget.config where package source mapping could interfere with the local source.
        var restoreArgs = new[] {
            "restore",
            "--configfile", "nuget.config",
            $"-p:ChiselPackageVersion={_packageVersion}",
        };
        await RunDotnetAsync(_workingDirectory, restoreArgs);
    }

    private async Task PublishAsync(PublishMode publishMode)
    {
        var publishDirectory = _workingDirectory.SubDirectory("publish");

        var outputDirectory = publishDirectory.SubDirectory(publishMode.ToString());

        var publishArgs = new[] {
            "publish",
            "--no-restore",
            "--output", outputDirectory.FullName,
            $"-p:PublishSingleFile={publishMode is PublishMode.SingleFile}",
            "--getProperty:IntermediateOutputPath",
        };
        var intermediateOutputPath = await RunDotnetAsync(_workingDirectory, publishArgs);
        IntermediateOutputPath = _workingDirectory.SubDirectory(intermediateOutputPath.TrimEnd());

        var executableFileName = OperatingSystem.IsWindows() ? "TestApp.exe" : "TestApp";
        var executableFile = outputDirectory.File(executableFileName);
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
            .WithEnvironmentVariables(env =>
            {
                var path = Environment.GetEnvironmentVariable("PATH");
                var dotnetInstallDir = Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");
                if (path != null && dotnetInstallDir != null && !path.Contains(dotnetInstallDir))
                {
                    env.Set("PATH", $"{path}{Path.PathSeparator}{dotnetInstallDir}");
                }
            })
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
