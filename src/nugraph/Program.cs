using nugraph;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<GraphCommand>();
app.Configure(config =>
{
    // TODO: add some more examples
    config.AddExample("spectre.console/src/Spectre.Console.Cli/Spectre.Console.Cli.csproj", "-v");
    config.AddExample("Serilog.Sinks.MSSqlServer", "--ignore", "Microsoft.Data.SqlClient");
#if DEBUG
    config.ValidateExamples();
#endif
    config.ConfigureConsole(RedirectionFriendlyConsole.Out);
    config.SetExceptionHandler((exception, _) =>
    {
        if (exception is CommandAppException commandAppException)
        {
            RedirectionFriendlyConsole.Error.Write(commandAppException.Pretty ?? Markup.FromInterpolated($"[red]Error:[/] {exception.Message}\n"));
            app.Run(["--help"]);
            return 64; // EX_USAGE -- The command was used incorrectly, e.g., with the wrong number of arguments, a bad flag, a bad syntax in a parameter, or whatever.
        }

        RedirectionFriendlyConsole.Error.WriteException(exception);
        return 70; // EX_SOFTWARE -- An internal software error has been detected.
    });
});
return app.Run(args);
