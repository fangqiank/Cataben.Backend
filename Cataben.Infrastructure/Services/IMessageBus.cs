namespace Cataben.Infrastructure.Services
{
    public interface IMessageBus
    {
        Task PublishAsync<T>(string subject, T message);

        Task<TResponse> RequestAsync<TResponse>(
            string subject,
            string correlationId,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        Task SubscribeAsync<T>(string subject, Func<T, Task> handler);

        Task ReplyAsync<T>(string subject, T message);
    }
}
