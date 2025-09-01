using EndpointMapper;
using FluentValidation;
using Hexus.Daemon.Configuration;
using Hexus.Daemon.Contracts;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Extensions;
using Hexus.Daemon.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Hexus.Daemon.Endpoints.Applications;

internal sealed class EditApplicationEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Patch, "/{name}")]
    public static Results<NoContent, NotFound, ValidationProblem> Handle(
        [FromRoute] string name,
        [FromBody] EditApplicationRequest request,
        [FromServices] IValidator<EditApplicationRequest> validator,
        [FromServices] ProcessManagerService processManagerService,
        [FromServices] HexusConfigurationManager configurationManager)
    {
        if (!configurationManager.Configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        if (processManagerService.IsApplicationRunning(application, out _))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationRunningWhileEditing);

        if (request.Name is not null && configurationManager.Configuration.Applications.TryGetValue(request.Name, out _))
            return TypedResults.ValidationProblem(ErrorResponses.ApplicationAlreadyExists);

        // Fill the rest of the request with the data from the application to edit
        request = new EditApplicationRequest(
            Name: request.Name ?? application.Name,
            Executable: Path.GetFullPath(request.Executable ?? application.Executable),
            Arguments: request.Arguments ?? application.Arguments,
            Note: request.Note ?? application.Note ?? "",
            WorkingDirectory: Path.GetFullPath(request.WorkingDirectory ?? application.WorkingDirectory),
            NewEnvironmentVariables: request.NewEnvironmentVariables ?? [],
            RemoveEnvironmentVariables: request.RemoveEnvironmentVariables ?? [],
            IsReloadingEnvironmentVariables: request.IsReloadingEnvironmentVariables ?? false,
            MemoryLimit: request.MemoryLimit switch
            {
                null => application.MemoryLimit,
                -1 => null,
                _ => request.MemoryLimit,
            }
        );

        if (!validator.Validate(request, out var validationResult))
            return TypedResults.ValidationProblem(validationResult.ToDictionary());

        // With the ?? on the EditApplicationRequest it should never get to a state where these are null
        Debug.Assert(request.Name is not null);
        Debug.Assert(request.Executable is not null);
        Debug.Assert(request.Note is not null);
        Debug.Assert(request.WorkingDirectory is not null);
        Debug.Assert(request.NewEnvironmentVariables is not null);
        Debug.Assert(request.RemoveEnvironmentVariables is not null);
        Debug.Assert(request.IsReloadingEnvironmentVariables is not null);

        // Rename the log file
        File.Move(
            sourceFileName: $"{EnvironmentHelper.ApplicationLogsDirectory}/{application.Name}.log",
            destFileName: $"{EnvironmentHelper.ApplicationLogsDirectory}/{request.Name}.log",
            overwrite: true
        );

        // Edit the configuration

        // Edit the name
        configurationManager.Configuration.Applications.Remove(application.Name);
        configurationManager.Configuration.Applications.Add(request.Name, application);

        // If we are reloading from shell, use the new object entirely and discard our
        var newEnvironmentVariables = request.IsReloadingEnvironmentVariables == true
            ? request.NewEnvironmentVariables
            : application.EnvironmentVariables;

        // If we aren't reloading from shell, we need to overwrite the keys based on editRequest.NewEnvironmentVariables
        if (request.IsReloadingEnvironmentVariables == false)
        {
            foreach (var (key, value) in request.NewEnvironmentVariables)
                newEnvironmentVariables[key] = value;
        }

        foreach (var env in request.RemoveEnvironmentVariables)
            newEnvironmentVariables.Remove(env);

        // Edit the configuration
        application.Name = request.Name;
        application.Executable = request.Executable;
        application.Arguments = request.Arguments;
        application.Note = request.Note;
        application.WorkingDirectory = request.WorkingDirectory;
        application.EnvironmentVariables = newEnvironmentVariables;
        application.MemoryLimit = request.MemoryLimit;

        configurationManager.SaveConfiguration();

        return TypedResults.NoContent();
    }
}
