using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.ClassRegistration;

namespace Services.ClassRegistrationService;

public interface IClassRegistrationService
{
    Task<PagedResult<ClassRegistrationViewModel>> GetMyRegistrationsAsync(
        string memberAccountId,
        bool includeCanceled, int page = 1, int pageSize = 20, DateTime? from = null, DateTime? to = null);

    Task<PagedResult<ClassRegistrationViewModel>> GetRegistrationsForStaffAsync(
        string? memberAccountId,
        bool includeCanceled,
        int page,
        int pageSize);

    Task<ClassRegistrationViewModel> RegisterAsync(
        string memberAccountId,
        int sessionId,
        string registeredVia,
        string registeredByAccountId);

    Task<ClassRegistrationViewModel> CancelRegistrationAsync(
        int registrationId,
        string? reason,
        string actorAccountId,
        bool isStaff);

    /// <summary>Đăng ký cả lớp: giữ chỗ mọi buổi còn ở tương lai của lớp.</summary>
    Task<ClassEnrollmentViewModel> RegisterClassAsync(
        string memberAccountId,
        int classId,
        string registeredVia,
        string registeredByAccountId);

    /// <summary>Hủy đăng ký cả lớp: nhả chỗ ở mọi buổi chưa bắt đầu.</summary>
    Task<ClassEnrollmentViewModel> CancelClassRegistrationAsync(
        string memberAccountId,
        int classId,
        string? reason,
        string actorAccountId,
        bool isStaff);

    Task<PagedResult<ClassEnrollmentViewModel>> GetMyClassEnrollmentsAsync(
        string memberAccountId,
        bool includeCanceled,
        int page,
        int pageSize);
}
