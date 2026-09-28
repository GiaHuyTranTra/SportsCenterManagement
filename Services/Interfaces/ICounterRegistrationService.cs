using SportsCenterManagement.DTOs.CounterRegistration;

namespace Services.Interfaces;

public interface ICounterRegistrationService
{
	Task<object> RegisterMemberAtCounterAsync(CounterRegisterMemberDto dto, string accountId, string ipAddress);
}
