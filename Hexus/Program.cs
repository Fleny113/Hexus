using System.CommandLine;

var nameArgument = new Argument<string>("name", "The name of the application to manage");
var forceOption = new Option<bool>("--force", "Force the stop of the application");
forceOption.AddAlias("-f");

var startCommand = new Command("stop") { nameArgument, forceOption };

startCommand.SetHandler((name, force) =>
{
    var toForceStop = force ? "force" : "";
    
    Console.WriteLine($"Requested to {toForceStop}stop an application named: \"{name}\"");
}, nameArgument, forceOption);

var rootCommand = new RootCommand();
rootCommand.AddCommand(startCommand);

return await rootCommand.InvokeAsync(args);
