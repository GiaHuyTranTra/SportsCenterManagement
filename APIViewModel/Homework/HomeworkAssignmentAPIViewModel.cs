namespace APIViewModel.Homework;

public sealed class HomeworkAssignmentAPIViewModel
{
    public int Id { get; set; }
    public string CoachId { get; set; } = string.Empty;
    public string CoachName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public int? ClassId { get; set; }
    public string? ClassName { get; set; }
    public int? SessionId { get; set; }
    public string? SessionLabel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; }
}
