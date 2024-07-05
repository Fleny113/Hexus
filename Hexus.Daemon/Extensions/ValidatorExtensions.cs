using FluentValidation;
using FluentValidation.Results;

namespace Hexus.Daemon.Extensions;

internal static class ValidatorExtensions
{
    public static IRuleBuilderOptions<T, string?> IsAbsolutePath<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(path => Path.IsPathFullyQualified(path.AsSpan()))
            .WithMessage("'{PropertyName}' must be a fully qualified path.");
    }

    public static bool Validate<T>(this IValidator<T> validator, T instance, out ValidationResult result)
    {
        result = validator.Validate(instance);

        return result.IsValid;
    }
}
