using System;
using System.IO;
using System.Text;
using Xunit.Abstractions;

namespace ConcurrencyAnalyzers.Tests
{
    public class RedirectingConsoleTest : IDisposable
    {
        private readonly TextWriter _oldWriter;
        public RedirectingConsoleTest(ITestOutputHelper helper)
        {
            Output = helper;
            _oldWriter = System.Console.Out;
            Console.SetOut(new RedirectingTextWriter(helper));
        }

        public ITestOutputHelper Output { get; }

        public void Dispose()
        {
            Console.SetOut(_oldWriter);
        }

        private class RedirectingTextWriter : TextWriter
        {
            private readonly StringBuilder _lineBuilder = new StringBuilder();
            private readonly ITestOutputHelper _output;

            public RedirectingTextWriter(ITestOutputHelper output)
            {
                _output = output;
            }

            /// <inheritdoc />
            public override Encoding Encoding => Encoding.UTF8;

            public override void WriteLine(ReadOnlySpan<char> buffer)
            {
                _lineBuilder.AppendLine(buffer.ToString());
                _output.WriteLine(_lineBuilder.ToString());
                _lineBuilder.Clear();
            }

            public override void WriteLine(string? value)
            {
                _lineBuilder.AppendLine(value);
                _output.WriteLine(_lineBuilder.ToString());
                _lineBuilder.Clear();
            }

            public override void WriteLine()
            {
                _output.WriteLine(_lineBuilder.ToString());
                _lineBuilder.Clear();
            }

            public override void Write(string? value)
            {
                _lineBuilder.Append(value);
            }
        }
    }
}