using AiDemo.Application.Interfaces;
using AiDemo.Contracts.DTOs;
using AiDemo.Domain.Entities;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AiDemo.Application.Commands.CreateItem;

public sealed class CreateItemHandler : ICommandHandler<CreateItemCommand, ItemDto>
{
    private readonly IApplicationDbContext _context;

    public CreateItemHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async ValueTask<ItemDto> Handle(CreateItemCommand command, CancellationToken ct)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Description = command.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = command.UserId
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync(ct);

        return new ItemDto(
            item.Id,
            item.Name,
            item.Description,
            item.CreatedAt,
            item.UpdatedAt,
            item.CreatedByUserId
        );
    }
}
