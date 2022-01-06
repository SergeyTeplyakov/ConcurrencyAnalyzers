using System;
using System.Diagnostics.ContractsLight;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace ConcurrencyAnalyzers;

/// <summary>
/// Helper class that makes stack frames more human readable.
/// </summary>
public static class StackFrameSanitizer
{
    /// <summary>
    /// Gets a display string for a given <paramref name="frame"/>.
    /// </summary>
    /// <remarks>
    /// By default <code>frame.ToString()</code> will produce hard to understand string representation without any special
    /// syntax representation for anonymous methods, generics etc.
    /// The idea of this method is to prettify that representation into more human readable form.
    /// </remarks>
    public static StackFrame? StackFrameToDisplayString(ClrStackFrame frame)
    {
        if (frame.Method is null)
        {
            return null;
        }

        var method = frame.Method;
        if (method.Signature is null)
        {
            return null;
        }

        // Technically, we can use 'method.Type.Name' and 'method.Name' here but the results are not correct
        // if the generics are involved.
        // So just parsing everything manually.

        string result = RawStackFrameSignatureToDisplayString(method.Signature, out var typeName,
            out var methodName, out var arguments);
        return new StackFrame(typeName.ToString(), methodName.ToString(), arguments.ToString(), Signature: result);
    }

    public static string RawStackFrameSignatureToDisplayString(string fullSignature,
        out ReadOnlySpan<char> type,
        out ReadOnlySpan<char> method,
        out ReadOnlySpan<char> args)
    {
        type = method = args = default;

        if (!SplitSignature(fullSignature, out var signature, out var arguments))
        {
            // Trace?
            return fullSignature;
        }
            
        
        var simplifiedSignature = PrettifySignature(signature);

        SplitFullName(simplifiedSignature, out type, out method);

        args = PrettifyArguments(arguments);

        return $"{simplifiedSignature}({args})";
    }

    public static string PrettifySignature(ReadOnlySpan<char> signature)
    {
        // The signature may look like this:
        // System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start
        //  [[System.Threading.Tasks.Parallel+<>c__50`1+<<ForEachAsync>b__50_0>d
        //      [[System.Int32, System.Private.CoreLib]]
        //          , System.Threading.Tasks.Parallel]]
        // and in this case the result should be:
        // System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start
        //      System.Threading.Tasks.Parallel+<>c__50`1+<<ForEachAsync>b__50_0>d
        //          System.Int32, System.Private.CoreLib

        int previousStartIndex = 0;
        int startIndex = 0;
        var sb = new StringBuilder();

        while (true)
        {
            // This implementation does not check that the name is correct and brackets are balanced.
            // It just replaces '[[' to '<' and ']]' to '>' and also simplifies the type name
            // and removes module names generics.
            var ((index, separatorLength, separatorKind), hasValue) = getNextSeparator(signature, startIndex).ToTuple();
            if (!hasValue)
            {
                // Processing the last section.
                ReadOnlySpan<char> section = signature.Slice(start: startIndex);
                SplitFullName(section, out var typeName, out var methodName);

                typeName = PrettifyTypeName(typeName);
                sb.Append(typeName);

                methodName = PrettifyMethodName(methodName);

                // This is a place where we breaking the loop.
                // So the current sb's state represents the type.

                if (typeName.Length != 0 || methodName.Length != 0)
                {
                    sb.Append('.');
                }

                sb.Append(methodName);
                break;
            }

            startIndex = index + separatorLength;

            if (separatorKind == GenericSeparator.OpenDelimiter)
            {
                // If we found '[' then a substring to the left ifs the type name.
                var segmentTypeName = signature.Slice(start: previousStartIndex, length: startIndex - previousStartIndex - separatorLength);
                segmentTypeName = PrettifyTypeName(segmentTypeName);
                sb.Append(segmentTypeName);

                // '[[' ']]' separate the top level generics and '[' ']' separate generic parameters.
                if (separatorLength == 2)
                {
                    sb.Append('<');
                }
            }
            else
            {
                // We found closing separator.
                // In this case the stuff between separators should be like:
                // typename, moduleName
                var nameAndType = signature.Slice(start: previousStartIndex, length: startIndex - previousStartIndex - separatorLength);
                // We really should find ', '.
                int commaIndex = nameAndType.IndexOf(',');

                var segmentTypeName = nameAndType;

                if (commaIndex != -1)
                {
                    segmentTypeName = nameAndType[..commaIndex];
                    segmentTypeName = PrettifyTypeName(segmentTypeName);
                }

                // else warn? Because this is strange!

                sb.Append(segmentTypeName);
                if (separatorLength == 2)
                {
                    sb.Append('>');
                }
            }

            previousStartIndex = startIndex;
        }

        // Dealing with arguments
        return sb.ToString();

        static (int startIndex, int count, GenericSeparator separatorkKind)? getNextSeparator(ReadOnlySpan<char> inputString, int startIndex)
        {
            // '[[' and ']]' used for a top level generic,
            // but the following is legit:
            // [[int, bcl], [object, bcl]]

            var input = inputString.Slice(startIndex);

            var bracketIndex = input.IndexOfAny('[', ']');
            if (bracketIndex == -1)
            {
                return null;
            }

            char bracket = input[bracketIndex];
            Contract.Assert(bracket is '[' or ']');

            int count = input.Slice(bracketIndex).CountConsecutiveChars(bracket);
            return (startIndex: startIndex + bracketIndex, count, (GenericSeparator)bracket);
        }
    }

    // The method allocates a new string.
    public static string PrettifyArguments(ReadOnlySpan<char> arguments)
    {
        var sb = new StringBuilder();

        foreach (var argumentIterator in arguments.Split(','))
        {
            // We split by ',' but usually args have spaces after the ',', like 'int, object'.
            var argument = arguments.Slice(argumentIterator).Trim(' ');

            // We don't have names here, so the argument can just have 'ByRef' or something similar
            int sectionNumber = 0;
            ReadOnlySpan<char> typeName = argument;
            string modifier = string.Empty;
            foreach (var sectionIterator in argument.Split(' '))
            {
                if (sectionNumber == 0)
                {
                    typeName = argument.Slice(sectionIterator);
                }
                // Can't directly compare span<char> with a string literal.
                else if (argument.Slice(sectionIterator).EqualsInvariant("ByRef"))
                {
                    // TODO: are there any other modifiers?
                    modifier = "ref";
                }

                sectionNumber++;
            }

            typeName = PrettifyTypeName(typeName);

            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            if (!string.IsNullOrEmpty(modifier))
            {
                sb.Append($"{modifier} ");
            }

            sb.Append(typeName);
        }

        return sb.ToString();
    }

    internal enum GenericSeparator
    {
        OpenDelimiter = '[',
        CloseDelimiter = ']',
    }

    public static ReadOnlySpan<char> PrettifyTypeName(ReadOnlySpan<char> typeName)
    {
        // We don't need another impl!
        // This method is different from 'PrettifyTypeName' which is not that ugly as the one that can be passed here.
        // An example of a "raw" type name:
        // System.Threading.Tasks.Parallel+<>c__50`1+<<ForEachAsync>b__50_0>d

        // This is logic is not perfect and was reversed engineered based on 'GeneratedNames.cs' and 'GeneratedNameKind.cs'.

        // General format looks like this:
        // '<' + OptionalName + '>' + Kind " '__' + OptionalSuffix + OptionalSuffixTerminator + 'Id'

        // 1. Closure: Kind = 'c'. OptionalName is 'DisplayClass' for capturing lambdas.
        // 2. Lambda: Kind = 'b'
        // 3. StateMachine: Kind = 'd'

        // This method does the following:
        // 1. Replaces '+' with '.'
        // 2. Removes '<>c__' section(s)
        // 3. Removes leading '<'
        // 4. Replaces '>b' with 'AnonymousMethod'
        // 5. Replaces '>d' with 'StateMachine'
        // 6. Replaces '>g'
        // 7. Removes `1, `2 etc

        if (PredefinedTypesSimplifier.IsPredefinedType(typeName))
        {
            return PredefinedTypesSimplifier.SimplifyTypeNameIfPossible(typeName);
        }

        var sb = new StringBuilder();
        foreach (var sectionRange in typeName.Split('+'))
        {
            var section = typeName.Slice(sectionRange);

            if (section.StartsWith("<>c"))
            {
                // This is quite tricky.
                // Here are two cases:
                // DumpSources.ParallelForBlocked+<>c__DisplayClass1_0+<<Run>b__0>d.MoveNext()
                // DumpSources.ParallelForBlocked+<>c__DisplayClass1_0.<Run>b__0(Int32, System.Threading.CancellationToken)
                // The first case separates the 'DisplayClass' section from 'Run' with '+', but the second one does not.
                // We should support both of them to keep 'Run.AnonymousMethod__0(int)'
                int methodNameIndex = section.IndexOf(".<", StringComparison.OrdinalIgnoreCase);
                if (methodNameIndex == -1)
                {
                    // This is the first case, we'll find '+<<Run' in the next section.
                    continue;
                }

                // Just cutting everything before '.<Run'
                section = section.Slice(methodNameIndex + 2);
            }

            // Skipping closures completely. I don't think that having 'DisplayClass' is useful anywhere.
            if (sb.Length > 0)
            {
                sb.Append('.');
            }

            // Still need to prettify the name.
            PrettifyGeneratedNames(section, sb);
        }
        
        ReadOnlySpan<char> result = sb.ToString();
        
        // The type name can potentially be generic, like end with `1 or `2 etc. Removing that part.
        // The generics marker can be at the end or in the middle of the type name, like A`1.B`1 is a valid thing.
        if (result.Contains('`'))
        {
            // This is not a good approach, but its simple enough not to care too much:
            sb.Replace("`1", "")
                .Replace("`2", "")
                .Replace("`3", "")
                .Replace("`4", "");
            result = sb.ToString();
        }
        
        return result;
    }

    private static void PrettifyGeneratedNames(ReadOnlySpan<char> section, StringBuilder sb)
    {
        // Skipping all the leading '<' that specify that the name is compiler generated.
        section = section.TrimStart('<');

        sb.Append(section);
        sb.Replace(">b", ".AnonymousMethod");
        sb.Replace(">d", ".StateMachine");

        // Removing the aspects related to local functions.
        // Local functions represented by '>g' and they have a special separator '|', that should be removed.
        sb.Replace(">g__", ".");
        sb.Replace("|", "");

        // Removing '<' as well, because the name may look like this:
        // <FooBar>g__FooBaz|3_0
        sb.Replace(".<", ".");
    }

    public static ReadOnlySpan<char> PrettifyMethodName(ReadOnlySpan<char> methodName)
    {
        var sb = new StringBuilder();
        PrettifyGeneratedNames(methodName, sb);
        return sb.ToString();
    }

    public static void SplitFullName(ReadOnlySpan<char> input, out ReadOnlySpan<char> typeName,
        out ReadOnlySpan<char> methodName)
    {
        // This is not a very simple task because we just have a string and due to generics we can't just use '.' as a separator.
        // like A<C.D>.Foo<X.Y, Z.W>
        // So what we're going to do is we're going to split that input into:
        // A<C.D> and Foo<X.Y, Z.W> and the last thing would be a method name.

        if (input.IsEmpty)
        {
            typeName = methodName = input;
            return;
        }

        // So the idea is to find the last '.' that is not inside the generics.
        int lastDotIndex = input.Length - 1;
        int bracketsBalanceCount = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];
            switch (current)
            {
                case '.' when bracketsBalanceCount == 0:
                    lastDotIndex = i;
                    break;
                case '<':
                    bracketsBalanceCount++;
                    break;
                case '>':
                    bracketsBalanceCount--;
                    break;
            }
        }

        // lastDotIndex is 'length - 1' if not found.
        typeName = input.Slice(0, lastDotIndex);
        methodName = input.Slice(lastDotIndex + 1); // can slice with input.Slice(input.Length); will get an empty result.
    }

    private static bool SplitSignature(string fullSignature, out ReadOnlySpan<char> signature, out ReadOnlySpan<char> arguments)
    {
        fullSignature.AsSpan().SplitInTwo('(', useLastIndex: true, out signature, out arguments);
        arguments = arguments.TrimEnd(')');

        return signature.Length != fullSignature.Length;
    }
}