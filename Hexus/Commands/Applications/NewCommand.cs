using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace Hexus.Commands.Applications;

internal static class NewCommand
{
    private static readonly Argument<string> NameArgument = 
        new("name", "The name for the application");
    private static readonly Argument<string> ExecutableArgument = 
        new("executable", "The file to execute, can resolved through the PATH env");
    private static readonly Argument<string[]> ArgumentsArgument =
        new("arguments", "The additional argument for the executable") { Arity = ArgumentArity.ZeroOrMore };
    private static readonly Option<string> WorkingDirectoryOption =
        new(["-w", "--working-directory"], "Set the current working directory for the application, defaults to the current folder");
    
    public static readonly Command Command = new("new", "Create a new application")
    {
        NameArgument,
        ExecutableArgument,
        ArgumentsArgument,
        WorkingDirectoryOption,
    };

    static NewCommand()
    {
        Command.AddAlias("create");
        
        Command.SetHandler(Handler);
    }

    private static async Task Handler(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForArgument(NameArgument);
        var executable = context.ParseResult.GetValueForArgument(ExecutableArgument);
        var arguments = string.Join(' ', context.ParseResult.GetValueForArgument(ArgumentsArgument));
        var workingDirectory = context.ParseResult.GetValueForOption(WorkingDirectoryOption);
        var ct = context.GetCancellationToken();

        if (!await HttpInvocation.CheckForRunningDaemon(ct))
        {
            PrettyConsole.Error.MarkupLine(PrettyConsole.DaemonNotRunningError);
            return;
        }

        workingDirectory ??= Environment.CurrentDirectory;
        workingDirectory = EnvironmentHelper.NormalizePath(workingDirectory);
        
        executable = Path.IsPathFullyQualified(executable) 
            ? EnvironmentHelper.NormalizePath(executable) 
            : TryResolveExecutable(executable);

        var newRequest = await HttpInvocation.HttpClient.PostAsJsonAsync(
            "/new",
            new NewApplicationRequest(
                name,
                executable,
                arguments,
                workingDirectory
            ),
            HttpInvocation.JsonSerializerOptions,
            ct
        );
        
        if (!newRequest.IsSuccessStatusCode)
        {
            ErrorResponse? response;
            
            if (newRequest is { StatusCode: HttpStatusCode.BadRequest, Content.Headers.ContentType.MediaType: "application/problem+json" })
            {
                var validationResponse = await newRequest.Content.ReadFromJsonAsync<ProblemDetails>(HttpInvocation.JsonSerializerOptions, ct);
                
                Debug.Assert(validationResponse is not null);
                
                var errorString = string.Join("\n", validationResponse.Errors.SelectMany(kvp => kvp.Value.Select(v => $"- [tan]{kvp.Key}[/]: {v}")));

                response = new ErrorResponse($"Validation errors: \n{errorString}");
            }
            else
            {
                response = await newRequest.Content.ReadFromJsonAsync<ErrorResponse>(HttpInvocation.JsonSerializerOptions, ct);
                response ??= new ErrorResponse("The daemon had an internal server error.");   
            }

            PrettyConsole.Error.MarkupLine($"There [indianred1]was an error[/] creating the application \"{name}\": {response.Error}");
            return;
        }
        
        PrettyConsole.Out.MarkupLine($"Application \"{name}\" [palegreen3]created[/]!");
    }

    private static string TryResolveExecutable(string executable)
    {
        // relative folders resolver (./.../exe)
        var absolutePath = EnvironmentHelper.NormalizePath(executable);

        if (File.Exists(absolutePath))
            return absolutePath;
        
        // PATH env resolver
        if (executable.Contains('/') || executable.Contains('\\'))
            throw new Exception("Executable cannot have slashes");
        
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? throw new Exception("Cannot get PATH environment variable");
        
        // Linux and Windows use different split char for the path
        var paths = pathEnv.Split(OperatingSystem.IsWindows() ? ';' : ':');

        if (OperatingSystem.IsWindows() && !executable.EndsWith(".exe"))
            executable = $"{executable}.exe";

        var resolvedExecutable = paths
            .Select(path => Path.Combine(path, executable))
            .Where(File.Exists)
            .Select(EnvironmentHelper.NormalizePath)
            .FirstOrDefault();

        if (resolvedExecutable is not null)
            return resolvedExecutable;
        
        // No executable found
        throw new FileNotFoundException("Cannot find the executable");
    }
}
