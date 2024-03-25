using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Chisel;

/// <summary>
/// Resolves assemblies by looking in the latest installed dotnet SDK directory.
/// So that there's no need to package <c>NuGet.ProjectModel.dll</c>, <c>NuGet.LibraryModel.dll</c> and <c>NuGet.Versioning.dll</c> inside Chisel.
/// </summary>
internal static class SdkAssemblyResolver
{
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

            var dotnetSdkDirectory = GetDotnetSdkDirectory(dotnet, log);
            if (dotnetSdkDirectory is not { Exists: true })
            {
                return null;
            }

            var assemblyName = new AssemblyName(args.Name);
            log($"ResolveAssembly({assemblyName})");

            var assemblyFile = Path.Combine(dotnetSdkDirectory.FullName, $"{assemblyName.Name}.dll");
            if (File.Exists(assemblyFile))
            {
                log($"Loading {assemblyFile}");
                var assembly = Assembly.LoadFrom(assemblyFile);
                log($"Loaded {assembly}");
                return assembly;
            }

            log($"Assembly not found: {assemblyFile}");
            return null;
        }
        catch (Exception exception)
        {
            log($"Unexpected exception: {exception}");
            return null;
        }
    }

    private static void DebugLog(string message)
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