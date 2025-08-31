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
        RuleFor(r => r.Arguments).NotNull();
        RuleFor(r => r.WorkingDirectory).NotEmpty().IsAbsolutePath();
        RuleFor(r => r.Note).NotNull();
        RuleFor(r => r.NewEnvironmentVariables).NotNull();
        RuleFor(r => r.RemoveEnvironmentVariables).NotNull();
        RuleFor(r => r.IsReloadingEnvironmentVariables).NotNull();
        RuleFor(r => r.MemoryLimit).GreaterThan(-1);
    }
}
