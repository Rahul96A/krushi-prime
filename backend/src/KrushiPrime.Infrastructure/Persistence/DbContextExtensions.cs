using Microsoft.EntityFrameworkCore;

namespace KrushiPrime.Infrastructure.Persistence;

public static class DbContextExtensions
{
    /// <summary>
    /// Runs work atomically and is compatible with Azure SQL's retrying execution strategy
    /// (transient faults / serverless resume are retried as one unit).
    /// </summary>
    public static Task<T> InTransactionAsync<T>(this AppDbContext db, Func<Task<T>> work, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await work();
                await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                db.ChangeTracker.Clear(); // discard partial state so a retry starts clean
                throw;
            }
        });
    }
}
