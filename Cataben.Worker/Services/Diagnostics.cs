using System.Diagnostics;

namespace Cataben.Worker.Services
{
    /// <summary>
    /// Activity source + helpers for tracing execution inside the worker. Exported via
    /// OpenTelemetry once the OTel wiring is added back; harmless without it.
    /// </summary>
    public static class Diagnostics
    {
        private const string ActivitySourceName = "Cataben.Worker";

        private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        public static Activity? StartActivity(string name) => ActivitySource.StartActivity(name);
    }

    public static class ActivityExtensions
    {
        public static void RecordException(this Activity? activity, Exception ex)
        {
            if (activity is null) return;

            activity.SetTag("exception.type", ex.GetType().FullName);
            activity.SetTag("exception.message", ex.Message);
            activity.SetTag("exception.stacktrace", ex.StackTrace);
            activity.SetStatus(ActivityStatusCode.Error);
        }
    }
}
