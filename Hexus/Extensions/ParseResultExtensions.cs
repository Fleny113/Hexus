using System.CommandLine.Binding;

namespace Hexus.Extensions;

internal static class ParseResultExtensions
{
    public static T? GetValueForBinder<T>(this BindingContext bindingContext, BinderBase<T> binder) where T : class
    {
        if (binder is IValueSource valueSource && valueSource.TryGetValue(binder, bindingContext, out var boundValue) && boundValue is T value)
            return value;

        return null;
    }
}
