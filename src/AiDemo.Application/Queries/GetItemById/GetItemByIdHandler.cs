using AiDemo.Application.Interfaces;
using AiDemo.Contracts.DTOs;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AiDemo.Application.Queries.GetItemById;

public sealed class GetItemByIdHandler : IQueryHandler<GetItemByIdQuery, ItemDto?>
{
    private readonly IApplicationDbContext _context;

    public GetItemByIdHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async ValueTask<ItemDto?> Handle(GetItemByIdQuery query, CancellationToken ct)
    {
        var item = await _context.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == query.Id, ct);

        return item == null ? null : new ItemDto(
            item.Id,
            item.Name,
            item.Description,
            item.CreatedAt,
            item.UpdatedAt,
            item.CreatedByUserId
        );
    }
}
