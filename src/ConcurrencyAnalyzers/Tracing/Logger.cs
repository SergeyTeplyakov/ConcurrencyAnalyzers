using Microsoft.Extensions.Logging;

namespace ConcurrencyAnalyzers.Tracing
{
    public partial class Logger
    {
        private readonly ILogger _logger;

        public Logger(ILogger logger) => _logger = logger;

        [LoggerMessage(
            EventId = 0,
            Level = LogLevel.Debug,
            Message = "Attaching to a process with id '{processId}'")]
        public partial void AttachingToProcess(int processId);
    }
}