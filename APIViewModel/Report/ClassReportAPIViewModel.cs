namespace APIViewModel.Report;

public class ClassReportAPIViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string TimeZoneId { get; set; } = null!;
    public int? ClassId { get; set; }
    public int? SportId { get; set; }
    public string RegistrationStatus { get; set; } = null!;
    public int TotalClasses { get; set; }
    public int TotalSessions { get; set; }
    public int CanceledSessions { get; set; }
    public int TotalRegistrations { get; set; }
    public int UniqueMembers { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalClasses / (double)PageSize);
    public List<ClassReportRowAPIViewModel> Items { get; set; } = new();
}

public class ClassReportRowAPIViewModel
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public int SportId { get; set; }
    public string SportName { get; set; } = null!;
    public int SessionCount { get; set; }
    public int CanceledSessionCount { get; set; }
    public int RegistrationCount { get; set; }
    public int UniqueMemberCount { get; set; }
}
