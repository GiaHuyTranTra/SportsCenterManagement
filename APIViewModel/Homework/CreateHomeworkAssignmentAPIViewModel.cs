namespace APIViewModel.Homework;

public sealed class CreateHomeworkAssignmentAPIViewModel
{
    public string TargetType { get; set; } = string.Empty;
    public string? MemberId { get; set; }
    public int? ClassId { get; set; }
    public int? SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}
