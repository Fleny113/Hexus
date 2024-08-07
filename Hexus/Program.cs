﻿using Hexus;
using Hexus.Commands;
using Hexus.Commands.Applications;
using Hexus.Commands.Utils;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

var rootCommand = new RootCommand("The Hexus management CLI");

rootCommand.AddCommand(NewCommand.Command);

rootCommand.AddCommand(ListCommand.Command);
rootCommand.AddCommand(InfoCommand.Command);
rootCommand.AddCommand(LogsCommand.Command);

rootCommand.AddCommand(InputCommand.Command);
rootCommand.AddCommand(StartCommand.Command);
rootCommand.AddCommand(EditCommand.Command);
rootCommand.AddCommand(StopCommand.Command);
rootCommand.AddCommand(RestartCommand.Command);
rootCommand.AddCommand(DeleteCommand.Command);

rootCommand.AddCommand(DaemonCommand.Command);

rootCommand.AddCommand(UpdateCommand.Command);
rootCommand.AddCommand(StartupCommand.Command);
rootCommand.AddCommand(MigratePm2Command.Command);

var builder = new CommandLineBuilder(rootCommand);

builder.UseDefaults();
builder.UseExceptionHandler((exception, _) => PrettyConsole.Error.WriteException(exception), 1);

var app = builder.Build();

return await app.InvokeAsync(args);
