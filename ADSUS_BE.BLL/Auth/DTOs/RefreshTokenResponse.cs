namespace ADSUS_BE.BLL.Auth.DTOs;

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);
