namespace Hexus.Daemon.Extensions;

internal static class DictionaryExtensions {
    public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> factory) where TKey : notnull
    {
        if (dictionary.TryGetValue(key, out var value))
            return value;
        
        value = factory(key);
        dictionary[key] = value;
        
        return value;
    }
}