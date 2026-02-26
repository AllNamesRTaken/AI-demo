using System;

namespace AvaloniaApp.Services;

public interface IIdempotencyKeyService
{
    Guid GenerateKey();
}

public sealed class IdempotencyKeyService : IIdempotencyKeyService
{
    public Guid GenerateKey() => Guid.NewGuid();
}
