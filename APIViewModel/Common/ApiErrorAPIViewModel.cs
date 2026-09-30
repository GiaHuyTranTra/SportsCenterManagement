namespace APIViewModel.Common;

public class ApiErrorAPIViewModel
{
    public string Code { get; set; } = null!;

    public string Message { get; set; } = null!;

    public object? Details { get; set; }
}
