using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.DataAccess.Contracts;

/// <summary>
/// Single commit boundary for repository mutations. Repositories that opt in
/// stage their changes and a caller invokes SaveChanges once per logical
/// operation instead of paying one DB round-trip per mutation.
/// </summary>
public interface ISaasKitUnitOfWork
{
    /// <summary>
    /// Persists all staged changes in a single transaction.
    /// </summary>
    /// <returns>Number of state entries written to the database.</returns>
    int SaveChanges();

    /// <summary>
    /// Persists all staged changes in a single transaction.
    /// </summary>
    /// <returns>Number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
