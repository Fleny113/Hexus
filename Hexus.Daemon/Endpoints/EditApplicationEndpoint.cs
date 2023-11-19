using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Hexus.Daemon.Endpoints;

internal sealed class EditApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Patch, "/{name}")]
    public static Results<NoContent, NotFound, Conflict<ErrorResponse>, ValidationProblem> Handle(
        [FromRoute] string name,
        [FromBody] EditApplicationRequest request,
        [FromServices] HexusConfigurationManager configurationManager)
    {
        if (!configurationManager.Configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        if (ProcessManagerService.IsApplicationRunning(application))
            return TypedResults.Conflict(ErrorResponses.CantEditRunningApplication);

        if (request.Name is not null && configurationManager.Configuration.Applications.TryGetValue(request.Name, out _))
            return TypedResults.Conflict(ErrorResponses.ApplicationWithTheSameNameAlreadyExiting);
        
        var editRequest = new EditApplicationRequest(
            Name: request.Name ?? application.Name,
            Executable: EnvironmentHelper.NormalizePath(request.Executable ?? application.Executable),
            Arguments: request.Arguments ?? application.Arguments,
            WorkingDirectory: EnvironmentHelper.NormalizePath(request.WorkingDirectory ?? application.WorkingDirectory)
        );
        
        if (!editRequest.ValidateContract(out var errors))
            return TypedResults.ValidationProblem(errors);

        // With the ?? on the EditApplicationRequest it should never get to a state where these are null
        Debug.Assert(editRequest.Name is not null);
        Debug.Assert(editRequest.Executable is not null);
        Debug.Assert(editRequest.Arguments is not null);
        Debug.Assert(editRequest.WorkingDirectory is not null);
        
        // Rename the log file
        File.Move(
            sourceFileName: $"{EnvironmentHelper.LogsDirectory}/{application.Name}.log",
            destFileName: $"{EnvironmentHelper.LogsDirectory}/{editRequest.Name}.log",
            overwrite: true
        );
        
        // Edit the configuration
        configurationManager.Configuration.Applications.Remove(application.Name);
        configurationManager.Configuration.Applications.Add(editRequest.Name, application);
        
        application.Name = editRequest.Name;
        application.Executable = editRequest.Executable;
        application.Arguments = editRequest.Arguments;
        application.WorkingDirectory = editRequest.WorkingDirectory;
        
        configurationManager.SaveConfiguration();
        
        return TypedResults.NoContent();
    }
}
