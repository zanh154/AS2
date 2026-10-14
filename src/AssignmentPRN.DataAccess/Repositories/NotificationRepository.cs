using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AivesDbContext _context;

    public NotificationRepository(AivesDbContext context)
    {
        _context = context;
    }

    public async Task<List<Notification>> ListByUserIdAsync(int userId, bool? isReadFilter = null)
    {
        var query = _context.Notifications.Where(n => n.UserId == userId);
        if (isReadFilter.HasValue)
        {
            query = query.Where(n => n.IsRead == isReadFilter.Value);
        }
        return await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
    }

    public async Task<int> CountUnreadByUserIdAsync(int userId)
    {
        return await _context.Notifications.CountAsync(n => n.UserId == userId && n.IsRead == false);
    }

    public async Task<bool> ExistsAsync(int notificationId, int userId)
    {
        return await _context.Notifications.AnyAsync(n => n.NotificationId == notificationId && n.UserId == userId);
    }

    public async Task AddAsync(Notification notification)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Notification notification)
    {
        _context.Entry(notification).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int notificationId, int userId)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);
        if (n != null)
        {
            _context.Notifications.Remove(n);
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);
        if (n != null)
        {
            n.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllReadAsync(int userId)
    {
        var notifs = await _context.Notifications.Where(n => n.UserId == userId && n.IsRead == false).ToListAsync();
        foreach (var item in notifs)
        {
            item.IsRead = true;
        }
        await _context.SaveChangesAsync();
    }
}