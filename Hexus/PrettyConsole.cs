using Spectre.Console;

namespace Hexus;

public static class PrettyConsole
{
    public const string DaemonNotRunningError =
        "There [indianred1]isn't the daemon running[/]. Start it using the '[indianred1]daemon[/] [darkcyan]start[/]' command.";

    public const string DaemonAlreadyRunningError =
        "The [indianred1]daemon is already running[/]. Stop it first to run a new instance using the '[indianred1]daemon[/] [darkseagreen1_1]stop[/]' command.";

    public static IAnsiConsole Out { get; } = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.Detect,
        ColorSystem = ColorSystemSupport.Detect,
        Out = new AnsiConsoleOutput(Console.Out),
    });


    public static IAnsiConsole Error { get; } = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.Detect,
        ColorSystem = ColorSystemSupport.Detect,
        Out = new AnsiConsoleOutput(Console.Error),
    });
}
