using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;

namespace Hexus.Extensions;

public class DictionaryBinder(Option<Dictionary<string, string>> option) : BinderBase<Dictionary<string, string>>
{
    private static readonly char[] StringKeyValuePair = [':', '='];
    
    protected override Dictionary<string, string> GetBoundValue(BindingContext bindingContext)
    {
        if (option is not Option genericOption)
        {
            throw new ArgumentException("Couldn't convert option to the generic variant");
        }

        var tokens = bindingContext.ParseResult.GetValueForOption(genericOption);

        if (tokens is null)
            return [];
        
        if (tokens is not List<Token> tokenList)
        {
            throw new Exception("Couldn't parse the options as a list of strings");
        }

        return tokenList
            .Select(token => token.Value)
            .Select(stringPair => stringPair.Split(StringKeyValuePair, 2))
            .Where(pair => pair is { Length: 2 })
            .Select(pair => (Key: pair[0], Value: pair[1]))
            .GroupBy(tuple => tuple.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value);
    }
}
