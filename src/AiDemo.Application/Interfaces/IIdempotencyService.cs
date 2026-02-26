namespace AiDemo.Application.Interfaces;

public interface IIdempotencyService
{
    Task<bool> IsProcessedAsync(Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<T?> GetCachedResultAsync<T>(Guid idempotencyKey, CancellationToken cancellationToken = default) where T : class;
    Task StoreResultAsync<T>(Guid idempotencyKey, T result, CancellationToken cancellationToken = default) where T : class;
}
