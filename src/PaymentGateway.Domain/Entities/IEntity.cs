namespace PaymentGateway.Domain.Entities;

public interface IEntity
{
    Guid Id { get; }
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; }
}
