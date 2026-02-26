using AiDemo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiDemo.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Item> Items { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
