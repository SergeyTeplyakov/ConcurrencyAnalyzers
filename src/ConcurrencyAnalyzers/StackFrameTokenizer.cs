using System;

using ConcurrencyAnalyzers.Utilities;

namespace ConcurrencyAnalyzers
{
    public static class StackFrameTokenizer
    {
        /// <summary>
        /// A fairly naive implementation that parses a type or a method name into tokens, like 'name' + 'separator' + 'name' etc.
        /// For instance for 'a.b&lt;d,e&gt;' we'll get: token('a'), separator('.'), token('b'), separator('&lt;'), token(d), separator(','), token('e'), separator('&gt;').
        /// </summary>
        public static void TokenizeTypeOrMethodName(
            ReadOnlySpan<char> typeOrMethodName,
            char[] tokens,
            Action<(string token, bool isSeparator)> handler)
        {
            // Super naive but its fine!
            foreach (var itemRange in typeOrMethodName.SplitAny(tokens))
            {
                var item = typeOrMethodName.Slice(itemRange);
                handler((token: item.ToString(), isSeparator: false));

                if (typeOrMethodName.Length > itemRange.End.Value)
                {
                    handler((token: typeOrMethodName[itemRange.End.Value].ToString(), isSeparator: true));
                }
            }
        }

        /// <summary>
        /// A fairly naive implementation that parses a full argument list into a set of arguments.
        /// </summary>
        /// <remarks>
        /// An expected format is: 'ref TypeName', or 'TypeName'
        /// </remarks>
        public static void TokenizeArgumentList(
            ReadOnlySpan<char> arguments,
            char[] nameSeparators,
            Action<(string token, bool isSeparator, bool isModifier)> handler)
        {
            bool first = true;
            foreach (var itemRange in arguments.Split(","))
            {
                if (!first)
                {
                    handler((token: ", ", isSeparator: true, isModifier: false));
                }

                first = false;

                var item = arguments.Slice(itemRange).Trim(' ');
                if (item.Contains(' '))
                {
                    int position = 0;
                    // item is: 'ref A.B.C'.
                    foreach (var argRange in item.Split(' '))
                    {
                        var arg = item.Slice(argRange);
                        if (position == 0)
                        {
                            // this is 'ref'
                            handler((token: arg.ToString(), isSeparator: false, isModifier: true));
                            handler((token: " ", isSeparator: true, isModifier: false));
                        }
                        else
                        {
                            TokenizeTypeOrMethodName(arg, nameSeparators,
                                tpl =>
                                {
                                    handler((token: tpl.token, isSeparator: tpl.isSeparator, isModifier: false));
                                });
                        }

                        position++;
                    }
                }
                else
                {
                    TokenizeTypeOrMethodName(item, nameSeparators,
                        tpl =>
                        {
                            handler((token: tpl.token, isSeparator: tpl.isSeparator, isModifier: false));
                        });
                }
            }
        }
    }
}