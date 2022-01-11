using System.IO;
using ConcurrencyAnalyzers.Rendering;
using ConcurrencyAnalyzers.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace ConcurrencyAnalyzers.Tests.Rendering
{
    public class TextRendererTests : RedirectingConsoleTest
    {
        public TextRendererTests(ITestOutputHelper helper) : base(helper)
        {
        }

        [Fact]
        public void TestErrorThreadPoolStats()
        {
            using var stringWriter = new StringWriter();
            using var textRenderer = new TextRenderer(stringWriter);
            Result<ThreadPoolStats> result = Result.Error<ThreadPoolStats>("Error!");

            textRenderer.Render(result);

            Output.WriteLine(stringWriter.ToString());
        }
    }
}