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
}
