using AiDemo.Application.Interfaces;
using AiDemo.Contracts.DTOs;
using AiDemo.Domain.Exceptions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AiDemo.Application.Commands.UpdateItem;

public sealed class UpdateItemHandler : ICommandHandler<UpdateItemCommand, ItemDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateItemHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async ValueTask<ItemDto> Handle(UpdateItemCommand command, CancellationToken ct)
    {
        var item = await _context.Items.FindAsync([command.Id], ct)
            ?? throw new DomainException($"Item with ID {command.Id} not found");

        item.Name = command.Name;
        item.Description = command.Description;
        item.UpdatedAt = DateTime.UtcNow;

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
