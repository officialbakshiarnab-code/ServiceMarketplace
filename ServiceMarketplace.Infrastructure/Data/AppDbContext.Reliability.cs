using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ServiceMarketplace.Infrastructure.Data;

public partial class AppDbContext
{
    private bool _atomicWorkflow;
    private readonly List<(string Operation, Guid EntityId, Func<Task> Action)> _afterCommit = [];

    /// <summary>Only for related multi-save workflows. Nested services join the owning boundary.</summary>
    public async Task<T> ExecuteAtomicAsync<T>(Func<Task<T>> action)
    {
        if (_atomicWorkflow) return await action();
        if (Database.CurrentTransaction != null)
            throw new InvalidOperationException("Use the owning workflow boundary to defer publication until commit.");

        // InMemory is retained for fast behavioral tests; it cannot prove rollback or locking.
        await using var transaction = Database.IsRelational() ? await Database.BeginTransactionAsync() : null;
        _atomicWorkflow = true;
        T result;
        try
        {
            result = await action();
            if (transaction != null) await transaction.CommitAsync();
        }
        catch
        {
            _afterCommit.Clear();
            if (transaction != null) await transaction.RollbackAsync();
            ChangeTracker.Clear();
            throw;
        }
        finally { _atomicWorkflow = false; }

        if (transaction != null) await transaction.DisposeAsync();
        var publications = _afterCommit.ToArray();
        _afterCommit.Clear();
        foreach (var publication in publications)
            await PublishAfterCommitAsync(publication.Operation, publication.EntityId, publication.Action);
        return result;
    }

    public Task ExecuteAtomicAsync(Func<Task> action) => ExecuteAtomicAsync(async () =>
    {
        await action();
        return true;
    });

    /// <summary>External/best-effort work only. Required audit/conversation/inbox writes stay transactional.</summary>
    public async Task PublishAfterCommitAsync(string operation, Guid entityId, Func<Task> action)
    {
        if (_atomicWorkflow)
        {
            _afterCommit.Add((operation, entityId, action));
            return;
        }
        if (Database.IsRelational() && Database.CurrentTransaction != null)
            throw new InvalidOperationException("External publication cannot run inside an unmanaged transaction.");
        try { await action(); }
        catch (Exception ex)
        {
            this.GetService<ILoggerFactory>().CreateLogger("Marketplace.PostCommit").LogWarning(
                "Post-commit effect failed. Operation: {Operation}, EntityId: {EntityId}, FailureType: {FailureType}",
                operation, entityId, ex.GetType().Name);
            ChangeTracker.Clear();
        }
    }
}
