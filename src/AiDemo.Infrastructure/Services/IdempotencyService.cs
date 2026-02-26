using AiDemo.Application.Interfaces;
using AiDemo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AiDemo.Infrastructure.Services;

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public IdempotencyService(ApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> IsProcessedAsync(Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeService.UtcNow;
        
        return await _context.IdempotencyRecords
            .AnyAsync(r => r.Key == idempotencyKey && r.ExpiresAt > now, cancellationToken);
    }

    public async Task<T?> GetCachedResultAsync<T>(Guid idempotencyKey, CancellationToken cancellationToken = default) 
        where T : class
    {
        var now = _dateTimeService.UtcNow;
        
        var record = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey && r.ExpiresAt > now, cancellationToken);

        if (record == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(record.Result);
    }

    public async Task StoreResultAsync<T>(Guid idempotencyKey, T result, CancellationToken cancellationToken = default) 
        where T : class
    {
        var now = _dateTimeService.UtcNow;
        
        var record = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = idempotencyKey,
            Result = JsonSerializer.Serialize(result),
            CreatedAt = now,
            ExpiresAt = now.AddHours(24) // Keep for 24 hours
        };

        _context.IdempotencyRecords.Add(record);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
