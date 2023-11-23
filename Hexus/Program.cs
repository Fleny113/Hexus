using Hexus;
using Hexus.Commands;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

var rootCommand = new RootCommand("The Hexus management CLI");

rootCommand.AddCommand(NewCommand.Command);
rootCommand.AddCommand(ListCommand.Command);
rootCommand.AddCommand(StopCommand.Command);
rootCommand.AddCommand(StartCommand.Command);
rootCommand.AddCommand(DeleteCommand.Command);

rootCommand.AddCommand(DaemonCommand.Command);

var builder = new CommandLineBuilder(rootCommand);

builder.UseDefaults();
builder.UseExceptionHandler((exception, _) => PrettyConsole.Error.WriteException(exception), 1);

return await builder.Build().InvokeAsync(args);
