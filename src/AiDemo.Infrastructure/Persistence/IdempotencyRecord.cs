namespace AiDemo.Infrastructure.Persistence;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public required Guid Key { get; set; }
    public required string Result { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
