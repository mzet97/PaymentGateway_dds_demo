using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Events;

namespace PaymentGateway.Domain.Aggregates;

public abstract class AggregateRoot : Entity
{
    private readonly List<DomainEvent> _domainEvents = new();

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() : base() { }

    protected AggregateRoot(Guid id) : base(id) { }

    protected void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
