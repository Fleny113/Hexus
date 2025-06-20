using System.CommandLine.Parsing;

namespace Hexus.Extensions;

public static class DictionaryParser
{
    private static readonly char[] StringKeyValuePair = [':', '='];

    public static Dictionary<string, string> Parse(ArgumentResult argumentResult)
    {
        if (argumentResult.Tokens.Count == 0)
            return new Dictionary<string, string>();

        return argumentResult.Tokens
            .Select(token => token.Value.Split(StringKeyValuePair, 2))
            .Where(pair => pair.Length == 2)
            .Select(pair => (Key: pair[0], Value: pair[1]))
            .GroupBy(tuple => tuple.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value);
    }
}
