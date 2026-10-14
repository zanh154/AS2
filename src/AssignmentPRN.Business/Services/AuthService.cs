using AssignmentPRN.Business.BusinessRules;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.DataAccess.Entities;
using System.ComponentModel.DataAnnotations;

namespace AssignmentPRN.Business.Services;

public class AuthService(IUserRepository userRepository) : IAuthService
{
    private const string StudentRoleName = "Student";
    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<AuthenticatedUserResponse?> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (!EmailValidator.IsValid(normalizedEmail))
        {
            return null;
        }

        var user = await userRepository.GetByEmailAsync(normalizedEmail);

        if (user is null || user.Status != "Active")
        {
            return null;
        }

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? ToResponse(user) : null;
    }

    public async Task<AuthenticatedUserResponse?> GetUserByIdAsync(int userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        return user is null || user.Status != "Active" ? null : ToResponse(user);
    }

    public async Task<(bool Success, string? Error, AuthenticatedUserResponse? User)> RegisterAsync(
        string fullName,
        string email,
        string password)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (false, "Full name is required.", null);
        }

        var normalizedFullName = fullName.Trim();
        if (normalizedFullName.Length is < 2 or > 100)
        {
            return (false, "Full name must be between 2 and 100 characters.", null);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "Email is required.", null);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (!EmailValidator.IsValid(normalizedEmail))
        {
            return (false, "Please enter a valid email address.", null);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Password is required.", null);
        }

        if (!MeetsPasswordRequirements(password))
        {
            return (
                false,
                "Password must be 8 to 50 characters and include uppercase, lowercase, digit, and special characters.",
                null);
        }

        if (await userRepository.EmailExistsAsync(normalizedEmail))
        {
            return (false, "An account with this email already exists.", null);
        }

        var studentRole = await userRepository.GetRoleByNameAsync(StudentRoleName);
        if (studentRole is null)
        {
            return (false, "The Student role is not configured.", null);
        }

        var user = new User
        {
            FullName = normalizedFullName,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RoleId = studentRole.RoleId,
            Role = studentRole,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var createdUser = await userRepository.CreateAsync(user);
        return (true, null, ToResponse(createdUser));
    }

    private static bool MeetsPasswordRequirements(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 50)
        {
            return false;
        }

        var hasUppercase = false;
        var hasLowercase = false;
        var hasDigit = false;
        var hasSpecialCharacter = false;

        foreach (var character in password)
        {
            hasUppercase |= char.IsUpper(character);
            hasLowercase |= char.IsLower(character);
            hasDigit |= char.IsDigit(character);
            hasSpecialCharacter |= !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character);
        }

        return hasUppercase && hasLowercase && hasDigit && hasSpecialCharacter;
    }

    private static AuthenticatedUserResponse ToResponse(User user) => new()
    {
        UserId = user.UserId,
        FullName = user.FullName,
        Email = user.Email,
        RoleName = user.Role?.RoleName ?? string.Empty
    };
}
