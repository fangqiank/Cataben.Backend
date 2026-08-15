namespace Cataben.Application.Repositories
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs <paramref name="action"/> inside a database transaction. Retried as a whole on
        /// transient failures (the Npgsql connection runs EnableRetryOnFailure), so the action
        /// must be re-runnable: take row locks and re-check state (idempotency) rather than
        /// relying on side effects from a previous attempt. Committing an action that made no
        /// changes is fine (read-only transaction); throw to roll back.
        /// </summary>
        Task ExecuteTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default);

        void Dispose();
    }
}
