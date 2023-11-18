using Hexus;
using System.CommandLine;

var rootCommand = new RootCommand();

rootCommand.AddCommand(StopCommand.Command);

return await rootCommand.InvokeAsync(args);
