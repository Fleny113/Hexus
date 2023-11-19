using Hexus.Commands;
using System.CommandLine;

var rootCommand = new RootCommand("The Hexus management CLI");

rootCommand.AddCommand(StopCommand.Command);
rootCommand.AddCommand(StartCommand.Command);

rootCommand.AddCommand(DaemonCommand.Command);

return await rootCommand.InvokeAsync(args);
