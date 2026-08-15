using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Repositories
{
    public class UnitOfWork(AppDbContext context) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => await context.SaveChangesAsync(cancellationToken);

        public async Task ExecuteTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            // The Npgsql connection enables EnableRetryOnFailure, and a retrying execution
            // strategy rejects user-initiated transactions unless the whole transactional block
            // runs inside ExecuteAsync — which is also what makes retries correct: a transient
            // failure re-runs begin + action + commit on a fresh transaction, so the action
            // must be idempotent (row locks + state re-checks at the call sites).
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(
                (context, action),
                static async (ctx, state, ct) =>
                {
                    await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
                    try
                    {
                        await state.action(ct);
                        await transaction.CommitAsync(ct);
                        return true;
                    }
                    catch
                    {
                        await transaction.RollbackAsync(ct);
                        throw;
                    }
                },
                verifySucceeded: null,
                cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            context.Dispose();
        }
    }
}
