namespace ConcurrencyAnalyzers;

public static class NullableValueTypesExtensions
{
    public static (T value, bool hasValue) ToTuple<T>(this T? value) where T : struct
    {
        if (value is null)
        {
            return (value: default, hasValue: false);
        }

        return (value: value.Value, hasValue: true);
    }
}