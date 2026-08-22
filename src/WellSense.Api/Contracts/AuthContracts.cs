namespace WellSense.Api.Contracts;

public record RegisterRequest(string Email, string Password);
public record RegisterResponse(Guid UserId, string Email, string Message);

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record VerifyEmailRequest(string Token);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record RefreshResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);

public record AuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid UserId,
    string Email);

public record DeviceLinkCodeResponse(string Code, DateTimeOffset ExpiresAt);

public record RedeemDeviceLinkCodeRequest(string Code, string? DeviceModel, string? OsVersion, string? AppVersion);

public record MobileAuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid UserId,
    string Email,
    Guid DeviceId);
