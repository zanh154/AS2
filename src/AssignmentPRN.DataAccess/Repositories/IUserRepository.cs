using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;

namespace AssignmentPRN.DataAccess.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByIdAsync(int userId);

    Task<bool> EmailExistsAsync(string email);

    Task<Role?> GetRoleByNameAsync(string roleName);

    Task<User> CreateAsync(User user);
}
