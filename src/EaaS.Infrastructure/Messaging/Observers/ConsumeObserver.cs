using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EaaS.Infrastructure.Messaging.Observers;

/// <summary>
/// Observes all MassTransit consume operations to provide structured logging and metrics.
/// Tracks message throughput, consumer latency, and failure rates — essential for monitoring
/// a 140+ emails/second pipeline and catching degradation before it cascades.
/// </summary>
public sealed partial class ConsumeObserver : IConsumeObserver
{
    private static readonly Meter Meter = new("EaaS.Messaging", "1.0.0");

    /// <summary>Counter for total messages consumed (success + failure), partitioned by message type.</summary>
    private static readonly Counter<long> MessagesConsumed =
        Meter.CreateCounter<long>("eaas.messaging.consumed.total", "messages",
            "Total messages consumed across all consumer types");

    /// <summary>Counter for failed consume attempts, partitioned by message type and exception type.</summary>
    private static readonly Counter<long> MessagesFailed =
        Meter.CreateCounter<long>("eaas.messaging.consumed.failed", "messages",
            "Messages that failed during consumption");

    /// <summary>Histogram tracking consumer processing duration to detect latency regressions.</summary>
    private static readonly Histogram<double> ConsumeDuration =
        Meter.CreateHistogram<double>("eaas.messaging.consumed.duration", "ms",
            "Time taken to consume a message in milliseconds");

    /// <summary>
    /// Tracks consume start timestamps by MessageId. Using ConcurrentDictionary because
    /// MassTransit ConsumeContext headers are read-only, so we cannot stash timing data there.
    /// Entries are removed in PostConsume/ConsumeFault to prevent memory leaks.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, long> _startTimestamps = new();

    private readonly ILogger<ConsumeObserver> _logger;

    public ConsumeObserver(ILogger<ConsumeObserver> logger)
    {
        _logger = logger;
    }

    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        if (context.MessageId.HasValue)
            _startTimestamps[context.MessageId.Value] = Stopwatch.GetTimestamp();
        LogConsumeStarted(_logger, typeof(T).Name, context.MessageId);
        return Task.CompletedTask;
    }

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        var messageType = typeof(T).Name;
        var durationMs = GetElapsedMs(context.MessageId);

        MessagesConsumed.Add(1, new KeyValuePair<string, object?>("message_type", messageType));
        ConsumeDuration.Record(durationMs, new KeyValuePair<string, object?>("message_type", messageType));

        LogConsumeCompleted(_logger, messageType, context.MessageId, durationMs);
        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        var messageType = typeof(T).Name;
        var durationMs = GetElapsedMs(context.MessageId);
        var exceptionType = exception.GetType().Name;

        MessagesConsumed.Add(1, new KeyValuePair<string, object?>("message_type", messageType));
        MessagesFailed.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("exception_type", exceptionType));
        ConsumeDuration.Record(durationMs, new KeyValuePair<string, object?>("message_type", messageType));

        LogConsumeFailed(_logger, messageType, context.MessageId, durationMs, exceptionType, exception);
        return Task.CompletedTask;
    }

    private double GetElapsedMs(Guid? messageId)
    {
        if (messageId.HasValue && _startTimestamps.TryRemove(messageId.Value, out var startTimestamp))
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            return elapsed.TotalMilliseconds;
        }

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Consuming {MessageType} MessageId={MessageId}")]
    private static partial void LogConsumeStarted(ILogger logger, string messageType, Guid? messageId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Consumed {MessageType} MessageId={MessageId} in {DurationMs:F1}ms")]
    private static partial void LogConsumeCompleted(ILogger logger, string messageType, Guid? messageId, double durationMs);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Consume failed {MessageType} MessageId={MessageId} after {DurationMs:F1}ms ExceptionType={ExceptionType}")]
    private static partial void LogConsumeFailed(ILogger logger, string messageType, Guid? messageId, double durationMs, string exceptionType, Exception ex);
}
