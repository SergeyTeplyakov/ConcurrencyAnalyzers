using System.IO;

namespace ConcurrencyAnalyzers.Rendering
{
    public class FileRenderer : TextRenderer
    {
        private readonly StreamWriter _streamWriter;

        private FileRenderer(StreamWriter streamWriter, bool renderRawStackFrames, int maxWidth) : base(streamWriter, renderRawStackFrames, maxWidth)
        {
            _streamWriter = streamWriter;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();

            _streamWriter.Flush();
            _streamWriter.Dispose();
        }

        public static FileRenderer Create(string path, int maxWidth = 160)
        {
            var file = File.OpenWrite(path);
            var writer = new StreamWriter(file, leaveOpen: false);
            return new FileRenderer(writer, renderRawStackFrames: false, maxWidth);
        }
    }
}