namespace Cataben.Shared.Messaging;

/// <summary>
/// Unified message bus used by both Cataben.API and Cataben.Worker. Hides NATS.Net
/// specifics behind two delivery modes:
///
/// - <b>Core</b> (PublishAsync / SubscribeAsync): fire-and-forget, at-most-once. Used
///   for result subjects (code.result.*) where loss is tolerable (client retries / DB).
/// - <b>JetStream</b> (PublishDurableAsync / SubscribeDurableAsync): durable, at-least-once.
///   Used for the critical code.execute task-dispatch subject so Worker crashes and
///   NATS restarts do not drop user submissions. The bus auto-acks on handler success
///   and naks on throw — callers never touch ack semantics. At-least-once means
///   consumers/handlers must be idempotent (the API result consumer dedupes by final state).
/// </summary>
public interface IMessageBus
{
    // --- Core NATS ---

    /// <summary>Fire-and-forget publish (at-most-once).</summary>
    Task PublishAsync<T>(string subject, T message, CancellationToken ct = default);

    /// <summary>Subscribe (Core NATS). Pass a queue group to load-balance across subscribers, or null for broadcast (every subscriber receives). The call blocks, running the handler on a consume loop until <paramref name="ct"/> cancels.</summary>
    Task SubscribeAsync<T>(
        string subject,
        string? queueGroup,
        Func<T, CancellationToken, Task> handler,
        CancellationToken ct);

    // --- JetStream (durable) ---

    /// <summary>Publish into a JetStream-mapped subject (stored, survives restarts).</summary>
    Task PublishDurableAsync<T>(string subject, T message, CancellationToken ct = default);

    /// <summary>Durable pull consumer (at-least-once). Load-balancing across N worker replicas is via the SHARED <paramref name="durable"/> consumer name — all workers pass the same name and JetStream fans out pull requests between them. Auto-ack on handler success, nak on throw (triggers redelivery).</summary>
    Task SubscribeDurableAsync<T>(
        string subject,
        string durable,
        Func<T, CancellationToken, Task> handler,
        CancellationToken ct);

    /// <summary>Idempotently create a JetStream stream covering the given subjects.</summary>
    Task EnsureStreamAsync(string name, string[] subjects, CancellationToken ct = default);
}
