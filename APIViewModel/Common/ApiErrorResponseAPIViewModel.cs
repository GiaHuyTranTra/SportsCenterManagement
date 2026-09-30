namespace APIViewModel.Common;

public class ApiErrorResponseAPIViewModel
{
    public bool Success { get; set; } = false;

    public ApiErrorAPIViewModel Error { get; set; } = null!;

    public string TraceId { get; set; } = null!;
}
