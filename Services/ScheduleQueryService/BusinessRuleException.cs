namespace Services.ScheduleQueryService;

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message, object? details = null, int? statusCode = null) : base(message)
    {
        Code = code;
        Details = details;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public object? Details { get; }

    public int? StatusCode { get; }
}
