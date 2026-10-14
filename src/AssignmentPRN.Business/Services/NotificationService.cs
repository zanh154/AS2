using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repo;

    public NotificationService(INotificationRepository repo)
    {
        _repo = repo;
    }

    // --- List --------------------------------------------------------
    public async Task<List<Notification>> ListAsync(int userId, bool? isReadFilter = null)
    {
        if (userId <= 0) return new List<Notification>();
        return await _repo.ListByUserIdAsync(userId, isReadFilter);
    }

    // --- Count unread ------------------------------------------------
    public async Task<int> CountUnreadAsync(int userId)
    {
        if (userId <= 0) return 0;
        return await _repo.CountUnreadByUserIdAsync(userId);
    }

    // --- Mark one as read --------------------------------------------
    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        if (userId <= 0) return;
        await _repo.MarkAsReadAsync(notificationId, userId);
    }

    // --- Mark all as read --------------------------------------------
    public async Task MarkAllReadAsync(int userId)
    {
        if (userId <= 0) return;
        await _repo.MarkAllReadAsync(userId);
    }

    // --- Create ------------------------------------------------------
    public async Task CreateAsync(int userId, string type, string message, int? courseId = null, int? examId = null)
    {
        if (userId <= 0) return;

        var n = new Notification
        {
            UserId = userId,
            CourseId = courseId,
            ExamId = examId,
            Type = type,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(n);
    }
}