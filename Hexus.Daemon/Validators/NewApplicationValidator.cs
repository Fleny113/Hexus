using FluentValidation;
using Hexus.Daemon.Contracts.Requests;
using Hexus.Daemon.Extensions;

namespace Hexus.Daemon.Validators;

internal class NewApplicationValidator : AbstractValidator<NewApplicationRequest>
{
    public NewApplicationValidator()
    {
        RuleFor(r => r.Name).NotEmpty();
        RuleFor(r => r.Executable).NotEmpty().IsAbsolutePath();
        RuleFor(r => r.Arguments).NotNull();
        RuleFor(r => r.WorkingDirectory).NotEmpty().IsAbsolutePath();
        RuleFor(r => r.Note).NotNull();
        RuleFor(r => r.EnvironmentVariables).NotNull();
        RuleFor(r => r.MemoryLimit).GreaterThan(0);
    }
}
