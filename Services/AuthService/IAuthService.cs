using APIViewModel.Auth;

namespace Services.AuthService;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    AccountLocked,
    AccountInactive,
    ConcurrentPasswordChange,
    ConcurrencyConflict
}

public enum RequestPasswordChangeOtpResult
{
    Success,
    AccountNotFound,
    AccountInactive,
    AccountLocked,
    CooldownActive,
    ConcurrentRequest,
    DeliveryFailed,
    ConcurrencyConflict
}

public enum ChangePasswordWithOtpResult
{
    Success,
    AccountNotFound,
    AccountInactive,
    AccountLocked,
    IncorrectCurrentPassword,
    OtpNotFound,
    OtpExpired,
    InvalidOtp,
    AttemptsExceeded,
    PasswordUnchanged,
    ConcurrentPasswordChange,
    ConcurrencyConflict
}

public interface IAuthService
{
    Task<LoginResponseAPIViewModel?> LoginCenterManagerAsync(LoginRequestAPIViewModel request);

    Task<LoginResponseAPIViewModel?> LoginCoachAsync(LoginRequestAPIViewModel request);

    Task<LoginResponseAPIViewModel?> LoginMemberAsync(LoginRequestAPIViewModel request);

    Task<LoginResponseAPIViewModel?> LoginReceptionistAsync(LoginRequestAPIViewModel request);

    Task<(LoginResult Result, LoginResponseAPIViewModel? Account)> LoginAsync(
        LoginRequestAPIViewModel request);

    Task<LoginResponseAPIViewModel?> GetSessionAccountAsync(string accountId);

    Task<(RequestPasswordChangeOtpResult Result, int RetryAfterSeconds)>
        RequestChangePasswordOtpAsync(string accountId);

    Task<ChangePasswordWithOtpResult> ChangePasswordWithOtpAsync(
        string accountId,
        ChangePasswordWithOtpRequestAPIViewModel request);
}
