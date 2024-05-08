using System;
using System.Threading.Tasks;
using NuGet.Common;
using Spectre.Console;

namespace nugraph;

internal class SpectreLogger(IAnsiConsole console, LogLevel minimumLevel) : LoggerBase
{
    public override void Log(ILogMessage message)
    {
        if (message.Level < minimumLevel)
            return;

        var color = GetColor(message.Level);
        console.WriteLine(message.Code == NuGetLogCode.Undefined ? message.Message : $"[{message.Code}] {message.Message}", color);
    }

    public override Task LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }

    private static Color GetColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => Color.Grey74,
            LogLevel.Verbose => Color.Grey58,
            LogLevel.Information => Color.Black,
            LogLevel.Minimal => Color.Black,
            LogLevel.Warning => Color.Orange1,
            LogLevel.Error => Color.Red,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, $"The value of argument '{nameof(level)}' ({level}) is invalid for enum type '{nameof(LogLevel)}'.")
        };
    }
}