using System.Diagnostics;

namespace Cataben.Infrastructure.Services
{
    public class OpenTelemetryService : IDistributedTracing
    {
        private readonly ActivitySource _activitySource = new("Cataben");

        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
            => _activitySource.StartActivity(name, kind);

        public void AddEvent(string name, Dictionary<string, object>? attributes = null)
        {
            var current = Activity.Current;
            if (current == null) return;

            current.AddEvent(new ActivityEvent(name));
            if (attributes != null)
            {
                foreach (var kv in attributes)
                    current.SetTag(kv.Key, kv.Value);
            }
        }

        public void SetTag(string key, object value)
        {
            Activity.Current?.SetTag(key, value);
        }

        public void RecordException(Exception ex)
        {
            var current = Activity.Current;
            if (current == null) return;

            current.SetStatus(ActivityStatusCode.Error);
            current.SetTag("exception.type", ex.GetType().FullName);
            current.SetTag("exception.message", ex.Message);
        }
    }
}
