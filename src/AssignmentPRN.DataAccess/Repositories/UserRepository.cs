using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public class UserRepository(AivesDbContext context) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return context.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.Email.ToLower() == normalizedEmail);
    }

    public Task<User?> GetByIdAsync(int userId)
    {
        return context.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.UserId == userId);
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return context.Users
            .AnyAsync(user => user.Email.ToLower() == normalizedEmail);
    }

    public Task<Role?> GetRoleByNameAsync(string roleName)
    {
        return context.Roles
            .FirstOrDefaultAsync(role => role.RoleName == roleName);
    }

    public async Task<User> CreateAsync(User user)
    {
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        return user;
    }
}
