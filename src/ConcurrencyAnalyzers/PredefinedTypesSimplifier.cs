using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using ConcurrencyAnalyzers.Utilities;

namespace ConcurrencyAnalyzers
{
    /// <summary>
    /// A helper class for converting <code>System.Int32</code> names to <code>int</code>.
    /// </summary>
    public static class PredefinedTypesSimplifier
    {
        private static readonly Dictionary<string, string> s_predefinedTypeMap = new Dictionary<string, string>()
        {
            ["Object"] = "object",
            ["Boolean"] = "bool",
            ["Char"] = "char",
            ["SByte"] = "sbyte",
            ["Byte"] = "byte",
            ["Int16"] = "short",
            ["UInt16"] = "ushort",
            ["Int32"] = "int",
            ["UInt32"] = "uint",
            ["Int64"] = "long",
            ["UInt64"] = "ulong",
            ["Single"] = "float",
            ["Double"] = "double",
            ["Decimal"] = "decimal",
            ["String"] = "string",
        };

        private static readonly Dictionary<string, string> s_predefinedTypeMapWithSystemPrefix = s_predefinedTypeMap.ToDictionary(kvp => $"System.{kvp.Key}", kvp => kvp.Value);

        public static bool TrySimplify(ReadOnlySpan<char> typeName, [NotNullWhen(true)] out string? predefinedType)
        {
            // Trading speed over allocations.
            // We have dictionaries, but using sequential search, because we can't use 'Span<char>' directly to lookup the values.

            if (typeName.StartsWith("System."))
            {
                foreach (var (key, value) in s_predefinedTypeMapWithSystemPrefix)
                {
                    // See the comment inside EqualsInvariant
                    if (typeName.EqualsInvariant(key))
                    {
                        predefinedType = value;
                        return true;
                    }
                }
            }
            else
            {
                foreach (var (key, value) in s_predefinedTypeMap)
                {
                    // See the comment inside EqualsInvariant
                    if (typeName.EqualsInvariant(key))
                    {
                        predefinedType = value;
                        return true;
                    }
                }
            }

            predefinedType = null;
            return false;
        }

        public static bool IsPredefinedType(ReadOnlySpan<char> typeName) => TrySimplify(typeName, out _);

        public static ReadOnlySpan<char> SimplifyTypeNameIfPossible(ReadOnlySpan<char> typeName)
        {
            if (TrySimplify(typeName, out var simplified))
            {
                return simplified;
            }

            return typeName;
        }
    }
}