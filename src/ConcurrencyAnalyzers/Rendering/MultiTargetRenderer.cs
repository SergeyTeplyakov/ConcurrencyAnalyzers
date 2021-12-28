using System.IO;
using System.Text;

namespace ConcurrencyAnalyzers;

public class MultiTargetRenderer : TextRenderer
{
    private readonly TextRenderer[] _renderers;
    public MultiTargetRenderer(TextRenderer[] renderers) : base(new NullTextWriter())
    {
        _renderers = renderers;
    }

    public override void Dispose()
    {
        foreach (var renderer in _renderers)
        {
            renderer.Dispose();
        }
    }

    public override void RenderNewLine()
    {
        foreach (var renderer in _renderers)
        {
            renderer.RenderNewLine();
        }
    }

    public override int RenderFragment(OutputFragment fragment)
    {
        int result = 0;
        foreach (var renderer in _renderers)
        {
            // All the renderers should return the same result.
            result = renderer.RenderFragment(fragment);
        }

        return result;
    }

    private class NullTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}