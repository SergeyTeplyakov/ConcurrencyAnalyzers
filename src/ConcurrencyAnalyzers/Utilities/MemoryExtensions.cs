using System;
using System.Globalization;

namespace ConcurrencyAnalyzers;

internal static class MemoryExtensions
{
    public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> source, Range range)
    {
        return source.Slice(start: range.Start.Value, length: range.End.Value - range.Start.Value);
    }

    public static void SplitInTwo(this ReadOnlySpan<char> input, char value, bool useLastIndex, out ReadOnlySpan<char> lhs,
        out ReadOnlySpan<char> rhs)
    {
        lhs = rhs = default;
        int valueIndex = useLastIndex ? input.LastIndexOf(value) : input.IndexOf(value);

        if (valueIndex == -1)
        {
            lhs = input;
        }
        else
        {
            lhs = input.Slice(0, valueIndex);
            rhs = input.Slice(start: valueIndex + 1);
        }
    }

    public static bool EqualsInvariant(this ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        // This is a legit way to compare Span<char> as two strings.
        return CultureInfo.InvariantCulture.CompareInfo.Compare(left, right) == 0;
    }

    public static int CountConsecutiveChars(this ReadOnlySpan<char> input, char value)
    {
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] != value)
            {
                return i;
            }
        }

        return input.Length;
    }

    /// <summary>
    /// Returns a type that allows for enumeration of each element within a split span
    /// using a single space as a separator character.
    /// </summary>
    /// <param name="span">The source span to be enumerated.</param>
    /// <returns>Returns a <see cref="SpanSplitEnumerator{T}"/>.</returns>
    public static SpanSplitEnumerator<char> Split(this ReadOnlySpan<char> span)
        => new SpanSplitEnumerator<char>(span, ' ');

    /// <summary>
    /// Returns a type that allows for enumeration of each element within a split span
    /// using the provided separator character.
    /// </summary>
    /// <param name="span">The source span to be enumerated.</param>
    /// <param name="separator">The separator character to be used to split the provided span.</param>
    /// <returns>Returns a <see cref="SpanSplitEnumerator{T}"/>.</returns>
    public static SpanSplitEnumerator<char> Split(this ReadOnlySpan<char> span, char separator)
        => new SpanSplitEnumerator<char>(span, separator);

    public static SpanSplitEnumerator<char> SplitAny(this ReadOnlySpan<char> span, ReadOnlySpan<char> separators)
        => new SpanSplitEnumerator<char>(span, separators, splitAny: true);

    /// <summary>
    /// Returns a type that allows for enumeration of each element within a split span
    /// using the provided separator string.
    /// </summary>
    /// <param name="span">The source span to be enumerated.</param>
    /// <param name="separator">The separator string to be used to split the provided span.</param>
    /// <returns>Returns a <see cref="SpanSplitEnumerator{T}"/>.</returns>
    public static SpanSplitEnumerator<char> Split(this ReadOnlySpan<char> span, string separator)
        => new SpanSplitEnumerator<char>(span, separator ?? string.Empty);
}