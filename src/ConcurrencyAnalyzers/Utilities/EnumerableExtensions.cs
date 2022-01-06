using System;
using System.Collections.Generic;

namespace ConcurrencyAnalyzers.Utilities
{
    internal static class EnumerableExtensions
    {
        public static Dictionary<TKey, List<TValue>> ToMultiDictionary<TSource, TKey, TValue>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TValue> valueSelector,
            IEqualityComparer<TKey>? comparer = null) where TKey : notnull
        {
            var result = new Dictionary<TKey, List<TValue>>(comparer);

            foreach (var item in source)
            {
                var key = keySelector(item);

                if (!result.TryGetValue(key, out var list))
                {
                    list = new List<TValue>();
                    result[key] = list;
                }

                list.Add(valueSelector(item));
            }

            return result;
        }
    }
}