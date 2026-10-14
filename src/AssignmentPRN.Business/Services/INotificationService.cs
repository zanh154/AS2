using AssignmentPRN.DataAccess.Entities;

namespace AssignmentPRN.Business.Services;

public interface INotificationService
{
    /// <summary>
    /// Lấy danh sách thông báo của người dùng hiện tại (có khả năng lọc trạng thái Đã/Chưa đọc).
    /// </summary>
    /// <param name="userId">ID người dùng (đượcInject từ session tại controller/page).</param>
    /// <param name="isReadFilter">Nullable: null = tất cả, true = đã đọc, false = chưa đọc.</param>
    Task<List<Notification>> ListAsync(int userId, bool? isReadFilter = null);

    /// <summary>
    /// Đếm số thông báo chưa đọc của người dùng.
    /// </summary>
    Task<int> CountUnreadAsync(int userId);

    /// <summary>
    /// Đánh dấu một thông báo là đã đọc.
    /// </summary>
    Task MarkAsReadAsync(int notificationId, int userId);

    /// <summary>
    /// Đánh dấu tất cả thông báo của người dùng là đã đọc.
    /// </summary>
    Task MarkAllReadAsync(int userId);

    /// <summary>
    /// Tạo một thông báo mới.
    /// </summary>
    Task CreateAsync(int userId, string type, string message, int? courseId = null, int? examId = null);
}