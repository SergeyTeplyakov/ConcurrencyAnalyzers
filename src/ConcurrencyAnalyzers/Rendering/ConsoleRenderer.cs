using System;
using System.Collections.Generic;

namespace ConcurrencyAnalyzers.Rendering
{
    public class ConsoleRenderer : TextRenderer
    {
        private static readonly Dictionary<FragmentKind, ConsoleColor> s_fragmentColors =
            new Dictionary<FragmentKind, ConsoleColor>()
            {
                [FragmentKind.Border] = ConsoleColor.DarkGreen,
                [FragmentKind.Namespace] = ConsoleColor.DarkCyan,
                [FragmentKind.TypeName] = ConsoleColor.DarkCyan,
                [FragmentKind.Separator] = ConsoleColor.White,
                [FragmentKind.MethodName] = ConsoleColor.Cyan,
                [FragmentKind.Argument] = ConsoleColor.DarkGray,
                [FragmentKind.ArgumentModifier] = ConsoleColor.Gray,
                [FragmentKind.ExceptionType] = ConsoleColor.DarkYellow,
                [FragmentKind.ExceptionMessage] = ConsoleColor.Yellow,
            };

        /// <nodoc />
        public ConsoleRenderer(bool renderRawStackFrames = false) : base(Console.Out, renderRawStackFrames)
        {
        }

        /// <inheritdoc />
        public override int RenderFragment(OutputFragment fragment)
        {
            var current = Console.ForegroundColor;
            Console.ForegroundColor = GetFragmentColor(fragment.Kind);
            var result = base.RenderFragment(fragment);
            Console.ForegroundColor = current;

            return result;
        }

        private static ConsoleColor GetFragmentColor(FragmentKind kind)
        {
            return s_fragmentColors.GetValueOrDefault(kind, ConsoleColor.White);
        }
    }
}