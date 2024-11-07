using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Chisel;

/// <summary>
/// Resolves assemblies by looking in the right place.
/// So that there's no need to package <c>NuGet.ProjectModel.dll</c>, <c>NuGet.LibraryModel.dll</c> and <c>NuGet.Versioning.dll</c> inside Chisel.
/// </summary>
/// <remarks>
/// To understand when this is required, go to Rider settings -> Build, Execution, Deployment -> Toolset and Build then choose
/// <c>C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe</c> instead of the auto detected <c>C:\Program Files\dotnet\sdk\8.0.200\MSBuild.dll</c>
/// </remarks>
internal static class SdkAssemblyResolver
{
    public static Assembly? ResolveAssembly(object sender, ResolveEventArgs args)
    {
        var dotnet = "dotnet";
        var log = DebugLog;

        try
        {
            if (sender is ValueTuple<string, Action<string>> tuple)
            {
                dotnet = tuple.Item1;
                log = tuple.Item2;
            }

            var assemblyName = new AssemblyName(args.Name);
            var assemblyFileName = $"{assemblyName.Name}.dll";
            log($"ResolveAssembly({assemblyName})");

            var directories = GetNuGetDirectories(sender as AppDomain, assemblyFileName, dotnet, log);
            foreach (var directory in directories)
            {
                var assemblyFile = new FileInfo(Path.Combine(directory, assemblyFileName));
                if (assemblyFile.Exists)
                {
                    log($"Loading {assemblyFile.FullName}");
                    try
                    {
                        var assembly = Assembly.LoadFile(assemblyFile.FullName);
                        log($"Loaded {assembly}");
                        return assembly;
                    }
                    catch (Exception exception)
                    {
                        log($"Failed to load {assemblyFile.FullName}: {exception}");
                    }
                }

                log($"Assembly {assemblyFile.FullName} not found");
            }

            return null;
        }
        catch (Exception exception)
        {
            log($"Unexpected exception: {exception}");
            return null;
        }
    }

    private static IEnumerable<string> GetNuGetDirectories(AppDomain? appDomain, string assemblyFileName, string dotnet, Action<string> log)
    {
        var nugetDirectories = new HashSet<string>();

        var loadedAssemblies = appDomain?.GetAssemblies().Where(e => e.GetName().Name.StartsWith("NuGet.")).ToList() ?? [];
        foreach (var (assembly, i) in loadedAssemblies.Select((e, i) => (e, i + 1)))
        {
            log($"Already loaded NuGet assembly from \"{appDomain?.FriendlyName}\" ({i}/{loadedAssemblies.Count}): {assembly} @ {assembly.Location}");
        }

        var loadedDirectories = loadedAssemblies.OrderBy(e => e.GetName().Name == "NuGet.ProjectModel" ? 0 : 1).Select(e => Path.GetDirectoryName(e.Location)).Distinct().ToList();
        foreach (var (directory, i) in loadedDirectories.Select((e, i) => (e, i + 1)))
        {
            nugetDirectories.Add(directory);
            log($"NuGet directory from already loaded assembly ({i}/{loadedDirectories.Count}): {directory}");
        }

        if (nugetDirectories.Count > 0)
        {
            return nugetDirectories;
        }

        var dotnetSdkDirectory = GetDotnetSdkDirectory(dotnet, log);
        if (dotnetSdkDirectory?.Exists == true)
        {
            var toolsDirectory = Path.Combine(dotnetSdkDirectory.FullName, "Sdks", "Microsoft.NET.Sdk", "tools");
            if (Directory.Exists(toolsDirectory))
            {
                log($"Searching for {assemblyFileName} in {toolsDirectory} (recursively)");
                foreach (var assemblyFilePath in Directory.EnumerateFiles(toolsDirectory, assemblyFileName, SearchOption.AllDirectories))
                {
                    var nugetDirectory = Path.GetDirectoryName(assemblyFilePath);
                    nugetDirectories.Add(nugetDirectory);
                }
            }
            else
            {
                log($"{toolsDirectory} not found");
            }
        }

        foreach (var (directory, i) in nugetDirectories.Select((e, i) => (e, i + 1)))
        {
            log($"NuGet directory from SDK ({i}/{nugetDirectories.Count}): {directory}");
        }

        if (nugetDirectories.Count == 0)
        {
            log("NuGet directory not found");
        }

        return nugetDirectories;
    }

    private static readonly Regex SdkRegex = new(@"(.*) \[(.*)\]", RegexOptions.Compiled);

    private static DirectoryInfo? GetDotnetSdkDirectory(string dotnet, Action<string> log)
    {
        var listSdks = new Process
        {
            StartInfo = new ProcessStartInfo(dotnet)
            {
                Arguments = "--list-sdks",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            },
        };
        log($"▶️ {listSdks.StartInfo.FileName} {listSdks.StartInfo.Arguments}");
        listSdks.Start();

        DirectoryInfo? dotnetSdkDirectory = null;

        while (listSdks.StandardOutput.ReadLine() is {} line)
        {
            var match = SdkRegex.Match(line);
            if (match.Success)
            {
                dotnetSdkDirectory = new DirectoryInfo(Path.Combine(match.Groups[2].Value, match.Groups[1].Value));
                var symbol = dotnetSdkDirectory.Exists ? "📁" : "❓";
                log($"{symbol} {dotnetSdkDirectory}");
            }
            else
            {
                log($"{line}{Environment.NewLine}    does not match {SdkRegex}");
            }
        }

        listSdks.WaitForExit(2000);

        return dotnetSdkDirectory;
    }

    internal static void DebugLog(string message)
    {
        var debugFile = Environment.GetEnvironmentVariable("CHISEL_DEBUG_FILE");
        if (debugFile != null)
        {
            using var stream = new FileStream(debugFile, FileMode.Append);
            using var writer = new StreamWriter(stream);
            writer.WriteLine($"[{DateTime.Now:O}] {message}");
        }
    }
}