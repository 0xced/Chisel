using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyModel;

try
{
    foreach (var dll in EnumerateDlls(Environment.ProcessPath).Distinct().Order())
    {
        Console.WriteLine(dll);
    }

    var connectionString = args.Length > 0 && !args[^1].StartsWith("--") ? args[^1] : "Server=sqlprosample.database.windows.net;Database=sqlprosample;user=sqlproro;password=nh{Zd?*8ZU@Y}Bb#";
    await using var dataSource = SqlClientFactory.Instance.CreateDataSource(connectionString);
    await using var command = dataSource.CreateCommand("Select @@version");
    var result = await command.ExecuteScalarAsync();

    Console.WriteLine($"[OK] {result}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"[ERROR] {exception}");
    return 1;
}

static IEnumerable<string> EnumerateDlls(string appPath)
{
    using var depsJsonStream = GetJsonDepsStream(appPath);
    using var reader = new DependencyContextJsonReader();
    var dependencyContext = reader.Read(depsJsonStream);

    foreach (var assembly in dependencyContext.GetRuntimeAssemblyNames(RuntimeInformation.RuntimeIdentifier))
    {
        yield return $"{assembly.Name}.dll";
    }

    foreach (var file in dependencyContext.GetRuntimeNativeRuntimeFileAssets(RuntimeInformation.RuntimeIdentifier))
    {
        yield return Path.GetFileName(file.Path);
    }
}

// See https://github.com/0xced/SingleFileAppDependencyContext
static Stream GetJsonDepsStream(string appPath)
{
    var depsJsonRegex = new Regex(@"DepsJson Offset:\[([0-9a-fA-F]+)\] Size\[([0-9a-fA-F]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    var startInfo = new ProcessStartInfo(appPath)
    {
        EnvironmentVariables =
        {
            ["COREHOST_TRACE"] = "1",
            ["COREHOST_TRACE_VERBOSITY"] = "3",
            ["DOTNET_STARTUP_HOOKS"] = " ",
        },
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
    };

    using var process = new Process { StartInfo = startInfo };
    long? depsJsonOffset = null;
    long? depsJsonSize = null;
    process.ErrorDataReceived += (sender, args) =>
    {
        var match = depsJsonRegex.Match(args.Data ?? "");
        if (match.Success)
        {
            var p = (Process)sender;
            depsJsonOffset = Convert.ToInt64(match.Groups[1].Value, 16);
            depsJsonSize = Convert.ToInt64(match.Groups[2].Value, 16);
            p.CancelErrorRead();
            p.Kill();
        }
    };

    process.Start();
    process.BeginErrorReadLine();
    if (!process.WaitForExit(2000))
    {
        process.Kill();
    }

    if (depsJsonOffset.HasValue && depsJsonSize.HasValue)
    {
        using var appHostFile = MemoryMappedFile.CreateFromFile(appPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        return appHostFile.CreateViewStream(depsJsonOffset.Value, depsJsonSize.Value, MemoryMappedFileAccess.Read);
    }

    return new FileStream(Path.ChangeExtension(appPath, ".deps.json"), FileMode.Open);
}