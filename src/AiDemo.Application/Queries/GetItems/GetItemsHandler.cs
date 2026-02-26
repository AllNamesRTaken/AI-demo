using AiDemo.Application.Interfaces;
using AiDemo.Contracts.DTOs;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AiDemo.Application.Queries.GetItems;

public sealed class GetItemsHandler : IQueryHandler<GetItemsQuery, IEnumerable<ItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetItemsHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IEnumerable<ItemDto>> Handle(GetItemsQuery query, CancellationToken ct)
    {
        var items = await _context.Items
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return items.Select(i => new ItemDto(
            i.Id,
            i.Name,
            i.Description,
            i.CreatedAt,
            i.UpdatedAt,
            i.CreatedByUserId
        ));
    }
}
