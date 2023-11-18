using Hexus.Commands;
using System.CommandLine;

var rootCommand = new RootCommand();

rootCommand.AddCommand(StopCommand.Command);
rootCommand.AddCommand(DaemonCommand.Command);

return await rootCommand.InvokeAsync(args);
