using AssignmentPRN.DataAccess.Entities;

namespace AssignmentPRN.DataAccess.Repositories;

public interface INotificationRepository
{
    Task<List<Notification>> ListByUserIdAsync(int userId, bool? isReadFilter = null);

    Task<int> CountUnreadByUserIdAsync(int userId);

    Task<bool> ExistsAsync(int notificationId, int userId);

    Task AddAsync(Notification notification);

    Task UpdateAsync(Notification notification);

    Task DeleteAsync(int notificationId, int userId);

    Task MarkAsReadAsync(int notificationId, int userId);

    Task MarkAllReadAsync(int userId);
}