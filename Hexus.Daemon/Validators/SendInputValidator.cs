using FluentValidation;
using Hexus.Daemon.Endpoints;

namespace Hexus.Daemon.Validators;

public sealed class SendInputValidator : AbstractValidator<SendInputRequest>
{
    public SendInputValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty();
    }
}
