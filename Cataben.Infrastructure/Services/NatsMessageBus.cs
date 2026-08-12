using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Cataben.Infrastructure.Services
{


    public class NatsMessageBus(ILogger<NatsMessageBus> logger) : IMessageBus
    {
        private readonly ConcurrentDictionary<string, List<Func<object, Task>>> _subscriptions = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests = new();
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        private bool _disposed;

        public async Task PublishAsync<T>(string subject, T message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message, _jsonOptions);
                logger.LogDebug("Published message to {Subject}", subject);

                // Simulate NATS publish
                await Task.Delay(10);

                // Process subscribers (simulated)
                if (_subscriptions.TryGetValue(subject, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        try
                        {
                            await handler(message!);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error in subscriber handler for {Subject}", subject);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish message to {Subject}", subject);
                throw;
            }
        }

        public async Task<TResponse> RequestAsync<TResponse>(
            string subject,
            string correlationId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();
            _pendingRequests[correlationId] = tcs;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout);

                // Simulate request/response
                await Task.Delay(100, cts.Token);

                // Simulate response
                var response = JsonSerializer.Serialize(new { success = true, data = "Result" });
                var result = JsonSerializer.Deserialize<TResponse>(response, _jsonOptions);

                if (result == null)
                    throw new InvalidOperationException("Failed to deserialize response");

                return result;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Request to {subject} timed out");
            }
            finally
            {
                _pendingRequests.TryRemove(correlationId, out _);
            }
        }

        public async Task SubscribeAsync<T>(string subject, Func<T, Task> handler)
        {
            try
            {
                _subscriptions.AddOrUpdate(
                    subject,
                    new List<Func<object, Task>> { (obj) => handler((T)obj) },
                    (key, existing) =>
                    {
                        existing.Add((obj) => handler((T)obj));
                        return existing;
                    });

                logger.LogInformation("Subscribed to {Subject}", subject);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to subscribe to {Subject}", subject);
                throw;
            }
        }

        public async Task ReplyAsync<T>(string subject, T message)
        {
            await PublishAsync(subject, message);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _pendingRequests.Clear();
            _subscriptions.Clear();
            _disposed = true;
        }
    }
}
