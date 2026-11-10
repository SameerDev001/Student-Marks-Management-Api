using MarksApi.Authentication;
using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Exceptions;
using MarksApi.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        IPasswordService passwordService,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Username))
                errors["username"] = new[] { "Username is required." };
            if (string.IsNullOrWhiteSpace(request.Password))
                errors["password"] = new[] { "Password is required." };

            throw new ValidationException(errors);
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        if (user == null || !_passwordService.VerifyPassword(user.PasswordHash, request.Password))
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            throw new UnauthorizedException("Invalid username or password.");
        }

        var (token, expiresAtUtc) = _tokenService.GenerateToken(user);

        _logger.LogInformation("User {Username} logged in successfully with role {Role}", user.Username, user.Role);

        return new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            Role = user.Role
        };
    }
}
