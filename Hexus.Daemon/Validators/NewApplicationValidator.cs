// using FluentValidation;
// using Hexus.Daemon.Contracts;
//
// namespace Hexus.Daemon.Validators;
//
// public sealed class NewApplicationValidator : AbstractValidator<NewApplicationRequest>
// {
//     public NewApplicationValidator()
//     {
//         RuleFor(x => x.Name)
//             .NotEmpty();
//
//         RuleFor(x => x.Executable)
//             .NotEmpty()
//             .Custom((executable, context) =>
//             {
//                 if (!Path.IsPathFullyQualified(executable.AsSpan()))
//                     context.AddFailure(nameof(NewApplicationRequest.Executable), "The path of the executable must be fully qualified");
//             });
//
//         RuleFor(x => x.WorkingDirectory)
//             .Custom((workingDirectory, context) =>
//             {
//                 if (string.IsNullOrWhiteSpace(workingDirectory))
//                     return;
//
//                 if (!Path.IsPathFullyQualified(workingDirectory.AsSpan()))
//                     context.AddFailure(nameof(NewApplicationRequest.WorkingDirectory),
//                         "The working directory, when specified, must be fully qualified");
//             });
//     }
// }
