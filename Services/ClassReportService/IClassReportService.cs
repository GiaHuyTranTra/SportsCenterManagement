using APIViewModel.Report;

namespace Services.ClassReportService;

public interface IClassReportService
{
    Task<ClassReportAPIViewModel> GetClassReportAsync(DateOnly? from, DateOnly? to,
        int? classId, int? sportId, string? registrationStatus, int page, int pageSize);

    Task<ClassReportAPIViewModel> GetClassReportForExportAsync(DateOnly? from, DateOnly? to,
        int? classId, int? sportId, string? registrationStatus);
}
