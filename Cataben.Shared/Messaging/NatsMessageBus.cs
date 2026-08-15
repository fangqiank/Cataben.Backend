using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Cataben.Shared.Messaging;

/// <summary>
/// NATS.Net-backed <see cref="IMessageBus"/>. Core NATS for fire-and-forget subjects such as
/// health checks; JetStream durable consumers for both the code.execute task-dispatch subject and
/// code.result.* so submission results survive Worker/API restart and are redelivered on failure.
/// </summary>
public class NatsMessageBus(INatsConnection connection, ILogger<NatsMessageBus> logger) : IMessageBus
{
    private readonly INatsJSContext _js = new NatsJSContext(connection);
    private readonly ConcurrentDictionary<string, INatsJSStream> _streams = new();
    private readonly ConcurrentDictionary<string, string> _subjectToStream = new();

    /// <summary>
    /// Max redeliveries before a JetStream message that keeps failing is treated as poison
    /// and ACK-terminated (dropped) instead of NAKed forever. Self-heals the consumer so a
    /// single bad message can't block the whole queue. Transient / process-crash failures
    /// are still redelivered (at-least-once) up to this many times.
    /// </summary>
    private const int MaxRedeliveries = 5;

    // ─── Core NATS ──────────────────────────────────────────────────────────────

    public async Task PublishAsync<T>(string subject, T message, CancellationToken ct = default)
    {
        await connection.PublishAsync(subject, message, cancellationToken: ct);
    }

    public async Task SubscribeAsync<T>(
        string subject,
        string? queueGroup,
        Func<T, CancellationToken, Task> handler,
        CancellationToken ct)
    {
        logger.LogInformation("Subscribed (core) to {Subject} [queue={Queue}]", subject, queueGroup);

        await foreach (var msg in connection.SubscribeAsync<T>(subject, queueGroup, cancellationToken: ct))
        {
            try
            {
                await handler(msg.Data!, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Core subscriber handler error on {Subject}", subject);
            }
        }
    }

    // ─── JetStream (durable) ─────────────────────────────────────────────────────

    public async Task PublishDurableAsync<T>(string subject, T message, CancellationToken ct = default)
    {
        var ack = await _js.PublishAsync(subject, message, cancellationToken: ct);
        logger.LogDebug("JetStream publish {Subject} -> stream {Stream}, seq {Seq}", subject, ack.Stream, ack.Seq);
    }

    public async Task EnsureStreamAsync(string name, string[] subjects, CancellationToken ct = default)
    {
        if (_streams.ContainsKey(name)) return;

        INatsJSStream stream;
        try
        {
            stream = await _js.CreateStreamAsync(new StreamConfig(name, subjects), ct);
            logger.LogInformation("Created JetStream stream {Name} [{Subjects}]", name, string.Join(",", subjects));
        }
        catch (NatsJSApiException)
        {
            // Stream already exists (concurrent creator / restart) — just fetch it.
            stream = await _js.GetStreamAsync(name, cancellationToken: ct);
        }

        _streams[name] = stream;
        foreach (var s in subjects)
            _subjectToStream[s] = name;
    }

    public async Task SubscribeDurableAsync<T>(
        string subject,
        string durable,
        Func<T, CancellationToken, Task> handler,
        CancellationToken ct)
    {
        if (!_subjectToStream.TryGetValue(subject, out var streamName))
            throw new InvalidOperationException(
                $"No JetStream stream provisioned for subject '{subject}'. Call EnsureStreamAsync at startup.");

        var stream = _streams[streamName];
        // Shared durable name across all worker replicas → JetStream fans pull requests
        // out between them (horizontal scaling / load balancing).
        var consumer = await stream.CreateOrUpdateConsumerAsync(
            new ConsumerConfig(durable) { FilterSubject = subject }, ct);
        logger.LogInformation("Subscribed (durable {Durable}) to {Subject}", durable, subject);

        // Best-effort ACK-terminate: a failure here (e.g. transient NATS disconnect) only means
        // NATS will redeliver, which is acceptable and logged. It must NOT throw out of the loop
        // and kill the consumer.
        async Task DropAsync(INatsJSMsg<T> m)
        {
            try { await m.AckTerminateAsync(cancellationToken: ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Ack-terminate failed; NATS may redeliver"); }
        }

        await foreach (var msg in consumer.ConsumeAsync<T>().WithCancellation(ct))
        {
            var seq = msg.Metadata?.Sequence.Stream.ToString() ?? "?";
            var deliveries = (int)(msg.Metadata?.NumDelivered ?? 0);

            // Undecodable / empty payload (msg.Data is null): ACK-terminate to drop it. NAKing
            // would redeliver the same undecodable bytes forever — a poison loop with nothing to
            // actually process. (Bug this fixed: ExecutionWorker dereferenced the null Data, its
            // catch re-threw on the null message, and the NAK redelivered ~1M+ times because the
            // consumer had max_deliver=-1.)
            if (msg.Data is null)
            {
                logger.LogWarning(
                    "Durable {Subject}: null payload (seq {Seq}, {Size}B, {Deliveries}x); ACK-terminate to drop",
                    subject, seq, msg.Size, deliveries);
                await DropAsync(msg);
                continue;
            }

            try
            {
                await handler(msg.Data, ct);
                await msg.AckAsync(cancellationToken: ct);           // success → remove from stream's pending
            }
            catch (Exception ex)
            {
                if (deliveries >= MaxRedeliveries)
                {
                    // Self-heal: a deterministically-failing message that keeps returning would
                    // otherwise NAK-loop forever. After enough retries, ACK-terminate so the
                    // consumer advances past the poison instead of stalling the whole queue.
                    logger.LogError(ex,
                        "Durable {Subject}: poison message seq {Seq} failed {Deliveries}x; ACK-terminate to advance",
                        subject, seq, deliveries);
                    await DropAsync(msg);
                }
                else
                {
                    logger.LogError(ex,
                        "Durable handler error on {Subject} (seq {Seq}, {Deliveries}/{Max}); NAK for redelivery",
                        subject, seq, deliveries, MaxRedeliveries);
                    await msg.NakAsync(cancellationToken: ct);       // transient failure → JetStream redelivers (at-least-once)
                }
            }
        }
    }
}
