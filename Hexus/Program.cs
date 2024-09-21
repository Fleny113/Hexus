using Hexus;
using Hexus.Commands;
using Hexus.Commands.Applications;
using Hexus.Commands.Utils;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

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

var builder = new CommandLineBuilder(rootCommand);

builder.UseDefaults();
builder.UseExceptionHandler((exception, _) => PrettyConsole.Error.WriteException(exception, ExceptionFormats.ShortenPaths), 1);

var app = builder.Build();

// TODO(commit): Remove this test code
args = ["logs", "cmd", "--no-streaming"];
 
return await app.InvokeAsync(args);
