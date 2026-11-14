namespace DataAccess.Entities;

public sealed class MemberSubscriptionDiscipline
{
    public int SubscriptionId { get; set; }

    public int DisciplineId { get; set; }

    public string DisciplineName { get; set; } = null!;

    public MemberSubscription Subscription { get; set; } = null!;
}
