using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace nugraph;

/// <summary>
/// Runs <c>dotnet</c> commands.
/// </summary>
internal static partial class Dotnet
{
    public static async Task<ProjectInfo> RestoreAsync(FileSystemInfo? source, CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var jsonPipe = new JsonPipeTarget<Result>(SourceGenerationContext.Default.Result);
        var dotnet = Cli.Wrap("dotnet")
            .WithArguments(args =>
            {
                args.Add("restore");
                if (source != null)
                {
                    args.Add(source.FullName);
                }

                // !!! Requires a recent .NET SDK (see https://github.com/dotnet/msbuild/issues/3911)
                // arguments.Add("--target:ResolvePackageAssets"); // may enable if the project is an exe in order to get RuntimeCopyLocalItems + NativeCopyLocalItems
                args.Add($"--getProperty:{nameof(Property.ProjectAssetsFile)}");
                args.Add($"--getProperty:{nameof(Property.TargetFramework)}");
                args.Add($"--getProperty:{nameof(Property.TargetFrameworks)}");
                args.Add($"--getItem:{nameof(Item.RuntimeCopyLocalItems)}");
                args.Add($"--getItem:{nameof(Item.NativeCopyLocalItems)}");
            })
            .WithEnvironmentVariables(env => env
                .Set("DOTNET_NOLOGO", "1")
                .Set("DOTNET_CLI_UI_LANGUAGE", "en")
            )
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.Merge(jsonPipe, PipeTarget.ToStringBuilder(stdout)))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr));

        var commandResult = await dotnet.ExecuteAsync(forcefulCancellationToken: cancellationToken, gracefulCancellationToken: CancellationToken.None);

        if (!commandResult.IsSuccess)
        {
            var message = stderr.Length > 0 && stdout.Length > 0 ? $"{stderr}{Environment.NewLine}{stdout}" : $"{stderr}{stdout}";
            if (message.Contains("MSB1001"))
            {
                throw new Exception("nugraph requires the .NET 8 SDK. Make sure that it's installed and that the global.json file (if any) is configured to use it.");
            }
            throw new Exception($"Running \"{dotnet}\" in \"{dotnet.WorkingDirPath}\" failed with exit code {commandResult.ExitCode}.{Environment.NewLine}{message}");
        }

        var (properties, items) = jsonPipe.Result ?? throw new Exception($"Running \"{dotnet}\" in \"{dotnet.WorkingDirPath}\" returned a literal 'null' JSON payload");
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