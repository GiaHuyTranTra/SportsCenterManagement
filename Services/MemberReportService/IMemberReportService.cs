using APIViewModel.Report;

namespace Services.MemberReportService;

public interface IMemberReportService
{
    Task<MemberReportAPIViewModel> GetMemberReportAsync(DateOnly? from, DateOnly? to);
}
