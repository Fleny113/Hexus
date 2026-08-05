namespace Hexus.Daemon.Extensions;

internal static class DictionaryExtensions
{
    extension<TKey, TValue>(Dictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        public TValue GetOrCreate(TKey key, Func<TKey, TValue> factory)
        {
            if (dictionary.TryGetValue(key, out var value))
                return value;

            value = factory(key);
            dictionary[key] = value;

            return value;
        }
    }
}
