using AiDemo.Application.Interfaces;
using AiDemo.Domain.Exceptions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace AiDemo.Application.Commands.DeleteItem;

public sealed class DeleteItemHandler : ICommandHandler<DeleteItemCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteItemHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async ValueTask<Unit> Handle(DeleteItemCommand command, CancellationToken ct)
    {
        var item = await _context.Items.FindAsync([command.Id], ct)
            ?? throw new DomainException($"Item with ID {command.Id} not found");

        _context.Items.Remove(item);
        await _context.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
