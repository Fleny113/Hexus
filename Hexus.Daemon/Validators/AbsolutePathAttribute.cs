using System.ComponentModel.DataAnnotations;

namespace Hexus.Daemon.Validators;

public class AbsolutePathAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string path)
            throw new InvalidOperationException("A path needs to be a string");

        return Path.IsPathFullyQualified(path.AsSpan())
            ? ValidationResult.Success
            : new ValidationResult(
                $"The {validationContext.DisplayName} needs to be a fully qualified path.",
                new[] { validationContext.DisplayName }
            );
    }
}
