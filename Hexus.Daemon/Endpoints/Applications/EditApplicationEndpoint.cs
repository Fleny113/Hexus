using EndpointMapper;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Services;
using Hexus.Daemon.Validators;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Hexus.Daemon.Endpoints.Applications;

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
            Note: request.Note ?? application.Note,
            WorkingDirectory: EnvironmentHelper.NormalizePath(request.WorkingDirectory ?? application.WorkingDirectory),
            NewEnvironmentVariables: request.NewEnvironmentVariables ?? [],
            RemoveEnvironmentVariables: request.RemoveEnvironmentVariables ?? [],
            IsReloadingEnvironmentVariables: request.IsReloadingEnvironmentVariables ?? false
        );

        if (!editRequest.ValidateContract(out var errors))
            return TypedResults.ValidationProblem(errors);

        // With the ?? on the EditApplicationRequest it should never get to a state where these are null
        Debug.Assert(editRequest.Name is not null);
        Debug.Assert(editRequest.Executable is not null);
        Debug.Assert(editRequest.Arguments is not null);
        Debug.Assert(editRequest.Note is not null);
        Debug.Assert(editRequest.WorkingDirectory is not null);
        Debug.Assert(editRequest.NewEnvironmentVariables is not null);
        Debug.Assert(editRequest.RemoveEnvironmentVariables is not null);
        Debug.Assert(editRequest.IsReloadingEnvironmentVariables is not null);

        // Rename the log file
        File.Move(
            sourceFileName: $"{EnvironmentHelper.LogsDirectory}/{application.Name}.log",
            destFileName: $"{EnvironmentHelper.LogsDirectory}/{editRequest.Name}.log",
            overwrite: true
        );

        // Edit the configuration

        // Edit the name
        configurationManager.Configuration.Applications.Remove(application.Name);
        configurationManager.Configuration.Applications.Add(editRequest.Name, application);

        // If we are reloading from shell, use the new object entirely and discard our
        var newEnvironmentVariables = editRequest.IsReloadingEnvironmentVariables == true
            ? editRequest.NewEnvironmentVariables
            : application.EnvironmentVariables;

        // If we aren't reloading from shell, we need to overwrite the keys based on editRequest.NewEnvironmentVariables
        if (editRequest.IsReloadingEnvironmentVariables == false)
        {
            foreach (var (key, value) in editRequest.NewEnvironmentVariables)
                newEnvironmentVariables[key] = value;
        }

        foreach (var env in editRequest.RemoveEnvironmentVariables)
            newEnvironmentVariables.Remove(env);

        // Edit the configuration
        application.Name = editRequest.Name;
        application.Executable = editRequest.Executable;
        application.Arguments = editRequest.Arguments;
        application.Note = editRequest.Note;
        application.WorkingDirectory = editRequest.WorkingDirectory;
        application.EnvironmentVariables = newEnvironmentVariables;

        configurationManager.SaveConfiguration();

        return TypedResults.NoContent();
    }
}
