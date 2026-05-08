namespace Synapse.Core.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Username,
    string Password
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthCookieResponse(UserDto User);

public record UserDto(
    int Id,
    string Email,
    string Username,
    string Language,
    string Role
);
