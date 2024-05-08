using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CliWrap;

namespace nugraph;

internal static partial class Dotnet
{
    private static readonly Dictionary<string, string?> EnvironmentVariables = new() { ["DOTNET_NOLOGO"] = "true" };

    public static async Task<ProjectInfo> RestoreAsync(FileSystemInfo? source)
    {
        var arguments = new List<string> { "restore" }; // may use "build" instead of "restore" if the project is an exe
        if (source != null)
        {
            arguments.Add(source.FullName);
        }
        // !!! Requires a recent .NET SDK (see https://github.com/dotnet/msbuild/issues/3911)
        // arguments.Add("--target:ResolvePackageAssets"); // may enable if the project is an exe in order to get RuntimeCopyLocalItems + NativeCopyLocalItems
        arguments.Add($"--getProperty:{nameof(Property.ProjectAssetsFile)}");
        arguments.Add($"--getProperty:{nameof(Property.TargetFramework)}");
        arguments.Add($"--getProperty:{nameof(Property.TargetFrameworks)}");
        arguments.Add($"--getItem:{nameof(Item.RuntimeCopyLocalItems)}");
        arguments.Add($"--getItem:{nameof(Item.NativeCopyLocalItems)}");

        var dotnet = Cli.Wrap("dotnet").WithArguments(arguments).WithEnvironmentVariables(EnvironmentVariables).WithValidation(CommandResultValidation.None);
        var jsonPipe = new JsonPipeTarget<Result>(SourceGenerationContext.Default.Result, () => new Exception($"Running \"{dotnet}\" in \"{dotnet.WorkingDirPath}\" returned a literal 'null' JSON payload"));
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var commandResult = await dotnet
            .WithStandardOutputPipe(PipeTarget.Merge(jsonPipe, PipeTarget.ToStringBuilder(stdout)))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr)).ExecuteAsync();

        if (!commandResult.IsSuccess)
        {
            var message = stderr.Length > 0 ? stderr.ToString() : stdout.ToString();
            throw new Exception($"Running \"{dotnet}\" in \"{dotnet.WorkingDirPath}\" failed with exit code {commandResult.ExitCode}.{Environment.NewLine}{message}");
        }

        var properties = jsonPipe.Result.Properties;
        var items = jsonPipe.Result.Items;
        var copyLocalPackages = items.RuntimeCopyLocalItems.Concat(items.NativeCopyLocalItems).Select(e => e.NuGetPackageId).ToHashSet();
        return new ProjectInfo(properties.ProjectAssetsFile, properties.GetTargetFrameworks(), copyLocalPackages);
    }

    public record ProjectInfo(string ProjectAssetsFile, IReadOnlyCollection<string> TargetFrameworks, IReadOnlyCollection<string> CopyLocalPackages);

    [JsonSerializable(typeof(Result))]
    private partial class SourceGenerationContext : JsonSerializerContext;

    private record Result(Property Properties, Item Items);

    private record Property(string ProjectAssetsFile, string TargetFramework, string TargetFrameworks)
    {
        public IReadOnlyCollection<string> GetTargetFrameworks()
        {
            var targetFrameworks = TargetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            return targetFrameworks.Count > 0 ? targetFrameworks : [TargetFramework];
        }
    }

    private record Item(CopyLocalItem[] RuntimeCopyLocalItems, CopyLocalItem[] NativeCopyLocalItems);

    private record CopyLocalItem(string NuGetPackageId);
}