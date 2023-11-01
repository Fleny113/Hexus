using Hexus.Daemon.Contracts;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Hexus.Daemon.Validators;

internal static class ValidatorExtensions
{
    public static bool ValidateContract(this IContract contract, [NotNullWhen(false)] out IDictionary<string, string[]>? validationErrors)
    {
        var context = new ValidationContext(contract);
        var validationResults = new List<ValidationResult>();

        validationErrors = null;

        if (Validator.TryValidateObject(contract, context, validationResults, true))
            return true;

        validationErrors = validationResults
            .SelectMany(result => result.MemberNames, (result, member) => (Member: member, result.ErrorMessage))
            .GroupBy(tuple => tuple.Member)
            .ToDictionary(
                tuples => tuples.Key, 
                tuples => tuples.Select(x => x.ErrorMessage ?? string.Empty).ToArray()
            );
        
        return false;
    }
}

