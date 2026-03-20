using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace PaymentGateway.Infrastructure.Observability;

internal sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity == null)
            return;

        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));

        if (activity.ParentSpanId != default)
        {
            logEvent.AddOrUpdateProperty(
                propertyFactory.CreateProperty("ParentSpanId", activity.ParentSpanId.ToString()));
        }
    }
}
