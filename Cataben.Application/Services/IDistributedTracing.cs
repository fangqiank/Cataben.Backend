using System.Diagnostics;

namespace Cataben.Application.Services
{
    public interface IDistributedTracing
    {
        Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
        void AddEvent(string name, Dictionary<string, object>? attributes = null);
        void SetTag(string key, object value);
        void RecordException(Exception ex);
    }
}
