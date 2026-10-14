using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.BusinessRules;
using AssignmentPRN.Presentation.ViewModels;

namespace AssignmentPRN.Presentation.ViewModels;

/// <summary>Shared dashboard numbers, so Admin and Lecturer stay in step.</summary>
internal static class DashboardBuilder
{
    private const int UpcomingWindowDays = 7;

    public static async Task<StaffDashboardViewModel> BuildStaffAsync(
        IExamSessionService service,
        CancellationToken cancellationToken,
        int? lecturerId = null)
    {
        var response = await service.ListAsync(cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return new StaffDashboardViewModel { LoadError = response.Error };
        }

        var visibleSessions = lecturerId.HasValue
            ? response.Data.Where(item => item.LecturerId == lecturerId.Value).ToList()
            : response.Data;

        var now = DateTime.Now;
        var horizon = now.AddDays(UpcomingWindowDays);
        var sessions = response.Data.Where(x => !lecturerId.HasValue || x.LecturerId == lecturerId).ToList();
        var upcoming = sessions
            .Where(session => session.Status == ExamSessionStatus.Scheduled)
            .Where(session => session.StartTime >= now && session.StartTime <= horizon)
            .OrderBy(session => session.StartTime)
            .ToList();

        return new StaffDashboardViewModel
        {
            SessionCount = sessions.Count,
            CandidateCount = sessions.Sum(session => session.CandidateCount),
            UpcomingCount = upcoming.Count,
            NextSession = upcoming.FirstOrDefault()
        };
    }

    public static async Task<StudentDashboardViewModel> BuildStudentAsync(
        IExamSessionService service,
        int userId,
        CancellationToken cancellationToken)
    {
        var response = await service.GetStudentScheduleAsync(userId, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return new StudentDashboardViewModel { LoadError = response.Error };
        }

        var now = DateTime.Now;
        var upcoming = response.Data.Items
            .Where(item => item.ScheduledTime >= now)
            .OrderBy(item => item.ScheduledTime)
            .ToList();

        return new StudentDashboardViewModel
        {
            ExamCount = response.Data.Items.Count,
            UpcomingCount = upcoming.Count,
            NextExam = upcoming.FirstOrDefault()
        };
    }
}
