namespace DataAccess.Entities;

public partial class ClassChangeEvent
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public int SessionId { get; set; }
    public string EventType { get; set; } = null!;
    public string? ActorAccountId { get; set; }
    public DateTime OccurredAt { get; set; }
}
