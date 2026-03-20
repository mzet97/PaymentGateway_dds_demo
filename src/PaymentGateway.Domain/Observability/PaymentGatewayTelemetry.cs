using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PaymentGateway.Domain.Observability;

public static class PaymentGatewayTelemetry
{
    public const string ActivitySourceName = "PaymentGateway";
    public const string MeterName = "PaymentGateway";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> OperationCounter =
        Meter.CreateCounter<long>("paymentgateway.operations.count");

    private static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>("paymentgateway.operations.duration", "ms");

    private static readonly Counter<long> PaymentLifecycleCounter =
        Meter.CreateCounter<long>("paymentgateway.payments.lifecycle.count");

    public static TelemetryOperation StartOperation(
        string category,
        string operation,
        ActivityKind kind = ActivityKind.Internal,
        params (string Key, object? Value)[] tags)
    {
        return new TelemetryOperation(category, operation, kind, tags);
    }

    public static void RecordPaymentLifecycle(
        string transition,
        string status,
        string? method = null,
        Guid? merchantId = null)
    {
        var tags = new TagList
        {
            { "transition", transition },
            { "status", status }
        };

        if (!string.IsNullOrWhiteSpace(method))
            tags.Add("method", method);

        if (merchantId.HasValue && merchantId.Value != Guid.Empty)
            tags.Add("merchant.id", merchantId.Value.ToString());

        PaymentLifecycleCounter.Add(1, tags);
    }

    public sealed class TelemetryOperation : IDisposable
    {
        private readonly string _category;
        private readonly string _operation;
        private readonly (string Key, object? Value)[] _tags;
        private readonly Stopwatch _stopwatch;
        private readonly Activity? _activity;
        private string _result = "success";

        internal TelemetryOperation(
            string category,
            string operation,
            ActivityKind kind,
            params (string Key, object? Value)[] tags)
        {
            _category = category;
            _operation = operation;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
            _activity = ActivitySource.StartActivity($"{category}.{operation}", kind);

            _activity?.SetTag("category", category);
            _activity?.SetTag("operation", operation);

            foreach (var (key, value) in tags)
            {
                _activity?.SetTag(key, value);
            }
        }

        public void SetResult(string result)
        {
            _result = result;
            _activity?.SetTag("result", result);
        }

        public void RecordException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _result = "error";
            _activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _activity?.SetTag("exception.type", exception.GetType().FullName);
            _activity?.SetTag("exception.message", exception.Message);
        }

        public void Dispose()
        {
            _stopwatch.Stop();

            var tags = new TagList
            {
                { "category", _category },
                { "operation", _operation },
                { "result", _result }
            };

            foreach (var (key, value) in _tags)
            {
                if (value != null)
                    tags.Add(key, value);
            }

            OperationCounter.Add(1, tags);
            OperationDuration.Record(_stopwatch.Elapsed.TotalMilliseconds, tags);
            _activity?.Dispose();
        }
    }
}
