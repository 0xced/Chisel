using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Exceptions;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Chisel.Tests;

[Trait("Category", "Integration")]
public sealed class ChiseledAppTests : IDisposable, IClassFixture<TestApp>
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestApp _testApp;
    private readonly AssertionScope _scope;

    public ChiseledAppTests(ITestOutputHelper outputHelper, TestApp testApp)
    {
        _outputHelper = outputHelper;
        _testApp = testApp;
        _scope = new AssertionScope();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }

    public static readonly TheoryData<PublishMode> PublishModeData = new(Enum.GetValues<PublishMode>());

    [Theory]
    [MemberData(nameof(PublishModeData))]
    public async Task RunTestApp(PublishMode publishMode)
    {
        var (stdOut, stdErr) = await RunTestAppAsync(publishMode);
        var allDlls = stdOut.Split(Environment.NewLine).Where(e => e.EndsWith(".dll"));
        var expectedDlls = new[]
        {
            "Microsoft.Data.SqlClient.dll",
            "Microsoft.Data.SqlClient.SNI.dll",
            "Microsoft.Extensions.DependencyModel.dll",
            "Microsoft.Identity.Client.dll",
            "Microsoft.IdentityModel.Abstractions.dll",
            "Microsoft.SqlServer.Server.dll",
            "System.Configuration.ConfigurationManager.dll",
            "System.Diagnostics.EventLog.dll",
            "System.Diagnostics.EventLog.Messages.dll",
            "System.Runtime.Caching.dll",
            "System.Security.Cryptography.ProtectedData.dll",
            "TestApp.dll",
        };
        allDlls.Except(expectedDlls).Should().BeEmpty();
        stdOut.Should().Contain("✅");
        stdErr.Should().BeEmpty();

        await Verifier.VerifyFile(_testApp.IntermediateOutputPath.File("TestApp.Chisel.gv")).DisableRequireUniquePrefix();
    }

    private async Task<(string StdOut, string StdErr)> RunTestAppAsync(PublishMode publishMode, params string[] args)
    {
        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();

        var command = Cli.Wrap(_testApp.GetExecutablePath(publishMode))
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuilder))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuilder));

        _outputHelper.WriteLine(command.ToString());

        var stopwatch = Stopwatch.StartNew();
        var result = await command.ExecuteAsync();
        var executionTime = stopwatch.ElapsedMilliseconds;

        var stdOut = stdOutBuilder.ToString().Trim();
        var stdErr = stdErrBuilder.ToString().Trim();

        _outputHelper.WriteLine($"⌚ Executed in {executionTime} ms");
        _outputHelper.WriteLine(stdOut);

        if (result.ExitCode != 0)
        {
            throw new CommandExecutionException(command, result.ExitCode, $"An unexpected exception has occurred while running {command}{Environment.NewLine}{stdErr}".Trim());
        }

        return (stdOut, stdErr);
    }
}
