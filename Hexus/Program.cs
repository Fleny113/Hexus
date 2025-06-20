using Hexus;
using Hexus.Commands;
using Hexus.Commands.Applications;
using Hexus.Commands.Utils;
using Spectre.Console;
using System.CommandLine;

var rootCommand = new RootCommand("The Hexus management CLI")
{
    NewCommand.Command,

    ListCommand.Command,
    InfoCommand.Command,
    LogsCommand.Command,

    InputCommand.Command,
    StartCommand.Command,
    EditCommand.Command,
    StopCommand.Command,
    RestartCommand.Command,
    DeleteCommand.Command,

    DaemonCommand.Command,

    UpdateCommand.Command,
    StartupCommand.Command,
    MigratePm2Command.Command,
    ShowTimezones.Command,
};

// Allow "hexus [diagram] ..." to show the parse diagram
rootCommand.Directives.Add(new DiagramDirective());

var configuration = new CommandLineConfiguration(rootCommand);

configuration.ThrowIfInvalid();

try
{
    return await configuration.InvokeAsync(args);
}
catch (Exception exception)
{
    PrettyConsole.Error.WriteException(exception, ExceptionFormats.ShortenPaths);
    return 1;
}
