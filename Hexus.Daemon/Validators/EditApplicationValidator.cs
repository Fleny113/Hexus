using FluentValidation;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Extensions;

namespace Hexus.Daemon.Validators;

internal class EditApplicationValidator : AbstractValidator<EditApplicationRequest>
{
    public EditApplicationValidator()
    {
        RuleFor(r => r.Name).NotEmpty();
        RuleFor(r => r.Executable).NotEmpty().IsAbsolutePath();
        RuleFor(r => r.WorkingDirectory).NotEmpty().IsAbsolutePath();
        RuleFor(r => r.Note).NotNull();
        RuleFor(r => r.NewEnvironmentVariables).NotNull();
        RuleFor(r => r.RemoveEnvironmentVariables).NotNull();
        RuleFor(r => r.IsReloadingEnvironmentVariables).NotNull();
        // This is validated after the transform the value to handle -1, so the validation message does look out of place
        RuleFor(r => r.MemoryLimit)
            .Must(x => x is null or >= 0)
            .WithMessage("Memory limit must be -1 or greater when specified");
    }
}
