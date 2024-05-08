using System;
using System.IO;
using System.Text;
using System.Threading;
using Spectre.Console;

namespace nugraph;

public static class RedirectionFriendlyConsole
{
    public static IAnsiConsole Out { get; } = CreateRedirectionFriendlyConsole(Console.Out);
    public static IAnsiConsole Error { get; } = CreateRedirectionFriendlyConsole(Console.Error);

    private static IAnsiConsole CreateRedirectionFriendlyConsole(TextWriter textWriter)
    {
        var output = new RedirectionFriendlyAnsiConsoleOutput(new AnsiConsoleOutput(textWriter));
        var settings = new AnsiConsoleSettings
        {
            Out = output,
            Ansi = output.IsTerminal ? AnsiSupport.Detect : AnsiSupport.No,
        };
        return AnsiConsole.Create(settings);
    }

    private sealed class RedirectionFriendlyAnsiConsoleOutput(IAnsiConsoleOutput ansiConsoleOutput) : IAnsiConsoleOutput
    {
        public TextWriter Writer
        {
            get
            {
                var count = 0;
                while (ansiConsoleOutput.Width == 80 && Environment.GetEnvironmentVariable("IDEA_INITIAL_DIRECTORY") != null && count < 100)
                {
                    // Because of https://youtrack.jetbrains.com/issue/IJPL-112721/Terminal-width-is-applied-asynchronously-which-leads-to-inconsistent-line-breaking-on-Windows
                    Thread.Sleep(millisecondsTimeout: 10);
                    count++;
                }
                return ansiConsoleOutput.Writer;
            }
        }

        public void SetEncoding(Encoding encoding) => ansiConsoleOutput.SetEncoding(encoding);
        public bool IsTerminal => ansiConsoleOutput.IsTerminal;
        public int Width => IsTerminal ? ansiConsoleOutput.Width : 320;
        public int Height => IsTerminal ? ansiConsoleOutput.Height : 240;
    }
}