using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using DataAccess.Entities;
using Services.Interfaces;
using SportsCenterManagement.DTOs.CounterRegistration;

namespace Services.Implementations;

public class CounterRegistrationService : ICounterRegistrationService
{
	private readonly SportsCenterManagementContext _context;
	private readonly IEmailService _emailService;
	private readonly IAuditLogService _auditLogService;

	public CounterRegistrationService(SportsCenterManagementContext context, IEmailService emailService, IAuditLogService auditLogService)
	{
		_context = context;
		_emailService = emailService;
		_auditLogService = auditLogService;
	}

	public async Task<object> RegisterMemberAtCounterAsync(CounterRegisterMemberDto dto, string accountId, string ipAddress)
	{
		if (await _context.Accounts.AnyAsync(account => account.Email.ToLower() == dto.Email.ToLower()))
			throw new InvalidOperationException("Email này đã được sử dụng trong hệ thống.");

		var role = await _context.Roles.FirstOrDefaultAsync(item => item.Name == "Member")
			?? throw new KeyNotFoundException("Role Member không tồn tại.");
		var defaultPassword = $"Sports@{RandomNumberGenerator.GetInt32(1000, 9999)}";
		var account = new Account
		{
			Id = Guid.NewGuid().ToString(), Email = dto.Email.ToLower(), Phone = dto.PhoneNumber,
			PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword), RoleId = role.Id,
			Status = "Active", FailedLoginCount = 0, IsLocked = false
		};
		_context.Accounts.Add(account);
		_context.Members.Add(new Member
		{
			AccountId = account.Id, FullName = dto.FullName,
			MemberCode = $"M{RandomNumberGenerator.GetInt32(100000, 999999)}",
			DateOfBirth = DateOnly.FromDateTime(dto.DateOfBirth)
		});
		await _context.SaveChangesAsync();
		await _auditLogService.LogAsync(accountId, "REGISTER_MEMBER_AT_COUNTER", "Member", account.Id, $"Đăng ký tài khoản mới cho {account.Email}", ipAddress);
		_ = _emailService.SendDefaultPasswordEmailAsync(account.Email, dto.FullName, defaultPassword);
		return new { AccountId = account.Id, FullName = dto.FullName, Email = account.Email, TemporaryPassword = defaultPassword };
	}
}
