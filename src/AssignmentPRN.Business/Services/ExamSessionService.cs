using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Repositories;

using ExamSessionUpdateInput = AssignmentPRN.Business.Interfaces.ExamSessionUpdateInput;
using ExamStudentSearch = AssignmentPRN.Business.Interfaces.ExamStudentSearch;
using ExamStudentSearchResult = AssignmentPRN.Business.Interfaces.ExamStudentSearchResult;

namespace AssignmentPRN.Business.Services;

public class ExamSessionService(
    IExamSessionRepository examSessionRepository,
    ICatalogRepository catalogRepository,
    IQuestionRepository questionRepository) : IExamSessionService
{
    private const string LecturerRole = "Lecturer";
    private const string StudentRole = "Student";
    private const int SearchResultLimit = 25;

    public Task<ServiceResponse<ExamSessionDetailResponse>> UpdateAsync(ExamSessionUpdateInput request, int? lecturerId, CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var current = await examSessionRepository.GetDetailAsync(request.ExamId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy phiên thi.");
            if (lecturerId.HasValue && current.Lecturer.UserId != lecturerId)
                throw new BusinessValidationException("Bạn không có quyền sửa phiên thi này.");
            EnsureSessionEditable(current);
            request.ExamName = BusinessValidation.RequiredText(request.ExamName, "tên phiên thi", 200);
            request.Description = BusinessValidation.OptionalText(request.Description, "mô tả", 1000);
            BusinessValidation.InRange(request.TimePerStudent, 1, 1440, "Thời lượng");
            BusinessValidation.InRange(request.MainQuestionCount, 1, 50, "Số câu hỏi chính");
            BusinessValidation.InRange(request.MaxFollowUpCount, 0, 50, "Số câu hỏi phụ");
            if (request.StartTime == default) throw new BusinessValidationException("Vui lòng chọn ngày giờ thi.");
            EnsureUpdatedTimingIsValid(current, request);
            var courseChanged = request.CourseId != current.Course.CourseId;
            if ((courseChanged || request.MainQuestionCount != current.MainQuestionCount)
                && (await questionRepository.ListAssignedQuestionIdsAsync(request.ExamId, cancellationToken)).Count > 0)
                throw new BusinessValidationException(
                    "Phiên thi đã phát đề. Hãy huỷ đề đã phát trước khi đổi môn học hoặc số câu hỏi chính. Đã có sinh viên vào thi thì không thể huỷ đề.");
            if (courseChanged)
            {
                if (!await catalogRepository.CourseExistsAsync(request.CourseId, cancellationToken))
                    throw new BusinessValidationException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
                await EnsureCandidatesBelongToCourseAsync(current, request.CourseId, cancellationToken);
            }
            if (courseChanged || request.MainQuestionCount != current.MainQuestionCount)
                await EnsureBankCoversAsync(request.CourseId, SeatsToDeal(current), request.MainQuestionCount, cancellationToken);
            return MapDetail(await examSessionRepository.UpdateAsync(request.ToDataAccess(), cancellationToken));
        }, "Không thể cập nhật phiên thi.");

    public Task<ServiceResponse> ChangeStatusAsync(int examId, ExamSessionStatus status, int? lecturerId, CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var current = await examSessionRepository.GetDetailAsync(examId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy phiên thi.");
            if (lecturerId.HasValue && current.Lecturer.UserId != lecturerId)
                throw new BusinessValidationException("Bạn không có quyền sửa phiên thi này.");
            EnsureStatusChangeAllowed(current, status);
            await examSessionRepository.ChangeStatusAsync(examId, status.ToDataAccess(), cancellationToken);
        }, "Không thể cập nhật trạng thái.");

    public Task<ServiceResponse<ExamStudentSearchResult>> SearchExamStudentsAsync(
        ExamStudentSearch filter, int? lecturerId, CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(async () =>
        {
            if (lecturerId.HasValue) BusinessValidation.PositiveId(lecturerId.Value, "giảng viên");
            if (filter.ExamId.HasValue) BusinessValidation.PositiveId(filter.ExamId.Value, "phiên thi");
            filter.Query = BusinessValidation.OptionalText(filter.Query, "tên hoặc email", 255);
            if (filter.From?.Date > filter.To?.Date)
                throw new BusinessValidationException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
            if (filter.To?.Date == DateTime.MaxValue.Date)
                throw new BusinessValidationException("Ngày kết thúc không hợp lệ.");
            if (filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value))
                throw new BusinessValidationException("Trạng thái không hợp lệ.");
            return (await examSessionRepository.SearchExamStudentsAsync(
                filter.ToDataAccess(), lecturerId, cancellationToken)).ToBusiness();
        }, "Không thể tra cứu sinh viên trong phiên thi.");
    }

    public Task<ServiceResponse> RemoveStudentAsync(int examId, int candidateId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(async () =>
        {
            BusinessValidation.PositiveId(examId, "phiên thi");
            BusinessValidation.PositiveId(candidateId, "sinh viên cần xóa");
            var session = await examSessionRepository.GetDetailAsync(examId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy phiên thi.");
            EnsureStudentCanBeRemoved(session, candidateId);
            await examSessionRepository.RemoveStudentAsync(examId, candidateId, cancellationToken);
        }, "Không thể xóa sinh viên khỏi phiên thi.");
    }

    public Task<ServiceResponse<ExamSessionDetailResponse>> AddStudentAsync(
        int examId, string email, DateTime scheduledTime, CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(async () =>
        {
            BusinessValidation.PositiveId(examId, "phiên thi");
            var normalizedEmail = BusinessValidation.RequiredText(email, "email sinh viên", 255);
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(normalizedEmail))
                throw new BusinessValidationException("Email sinh viên không hợp lệ.");

            var session = await examSessionRepository.GetDetailAsync(examId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy phiên thi.");
            EnsureStudentCanBeAdded(session, scheduledTime);
            var student = (await catalogRepository.FindUsersByEmailsAsync(
                    StudentRole, [normalizedEmail.ToLowerInvariant()], cancellationToken))
                .FirstOrDefault()
                ?? throw new BusinessValidationException("Không tìm thấy sinh viên đang hoạt động với email này.");
            if (session.Candidates.Any(candidate => candidate.StudentId == student.UserId))
                throw new BusinessValidationException("Sinh viên đã có trong phiên thi này.");

            // Same rule as moving the session to another course: whoever sits it studies it.
            var enrolled = await catalogRepository.ListStudentIdsInCourseAsync(session.Course.CourseId, cancellationToken);
            if (!enrolled.Contains(student.UserId))
                throw new BusinessValidationException(
                    $"{student.FullName} không thuộc lớp nào của môn {session.Course.CourseCode}.");

            var config = await questionRepository.GetExamConfigurationAsync(examId, cancellationToken);
            var papers = await questionRepository.ListExamPaperAsync(examId, cancellationToken);
            var taken = await questionRepository.ListAssignedQuestionIdsAsync(examId, cancellationToken);
            var available = await questionRepository.ListPoolIdsAsync(new AssignmentPRN.DataAccess.Contracts.QuestionPickRequest {
                CourseId = config.CourseId, Count = config.Count, MaterialIds = config.MaterialIds,
                Difficulties = config.Difficulties, TakenQuestionIds = taken }, cancellationToken);
            var waitingForPaper = session.Candidates.Count(candidate =>
                candidate.Status.ToBusiness() is not (CandidateStatus.Cancelled or CandidateStatus.Absent)
                && !papers.Any(paper => paper.CandidateId == candidate.CandidateId && paper.Questions.Count > 0));
            EnsureQuestionSupply(waitingForPaper + 1, config.Count, available.Count);

            return MapDetail(await examSessionRepository.AddStudentAsync(
                examId, student.UserId, scheduledTime, cancellationToken));
        }, "Không thể thêm sinh viên vào phiên thi.");
    }

    public Task<ServiceResponse<IReadOnlyList<ExamSessionListItemResponse>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<ExamSessionListItemResponse>>(
            async () =>
            {
                var sessions = await examSessionRepository.ListAsync(cancellationToken);
                return sessions.Select(MapListItem).ToList();
            },
            "Không thể tải danh sách lịch thi.");
    }

    public Task<ServiceResponse<ExamSessionDetailResponse>> GetAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var detail = await examSessionRepository.GetDetailAsync(examId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lịch thi.");
                return MapDetail(detail);
            },
            "Không thể tải chi tiết lịch thi.");
    }

    public Task<ServiceResponse<ExamSessionOptionsResponse>> GetCreationOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var courses = await catalogRepository.ListActiveCoursesAsync(cancellationToken);
                var lecturers = await catalogRepository.ListUsersInRoleAsync(LecturerRole, cancellationToken);
                var classes = await catalogRepository.ListActiveClassesAsync(cancellationToken);

                return new ExamSessionOptionsResponse
                {
                    Courses = courses
                        .Select(item => new LookupOption(item.CourseId, $"{item.CourseCode} · {item.CourseName}"))
                        .ToList(),
                    Lecturers = lecturers
                        .Select(item => new LookupOption(item.UserId, $"{item.FullName} · {item.Email}"))
                        .ToList(),
                    Classes = classes
                        .Select(item => new ClassOptionResponse
                        {
                            StudentCount = item.Students.Count(student =>
                                student.Student.Status == "Active"
                                && student.Student.Role.RoleName == StudentRole),
                            ClassId = item.ClassId,
                            CourseId = item.CourseId,
                            LecturerId = item.LecturerId,
                            Label = $"{item.ClassCode} · {item.Course.CourseCode} · "
                                + $"{item.Students.Count(student => student.Student.Status == "Active" && student.Student.Role.RoleName == StudentRole)} sinh viên"
                        })
                        .ToList()
                };
            },
            "Không thể tải dữ liệu để tạo lịch thi.");
    }

    public Task<ServiceResponse<ClassRosterResponse>> GetClassRosterAsync(
        int classId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var id = BusinessValidation.PositiveId(classId, "lớp học");
                var academicClass = await catalogRepository.GetClassWithStudentsAsync(id, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lớp học đang hoạt động.");

                var students = academicClass.Students
                    .Select(item => item.Student)
                    .Where(student => student.Status == "Active" && student.Role.RoleName == StudentRole)
                    .GroupBy(student => student.UserId)
                    .Select(group => group.First())
                    .OrderBy(student => student.FullName)
                    .ThenBy(student => student.Email)
                    .Select(student => new PersonResponse
                    {
                        UserId = student.UserId,
                        FullName = student.FullName,
                        Email = student.Email
                    })
                    .ToList();

                return new ClassRosterResponse
                {
                    ClassId = academicClass.ClassId,
                    CourseId = academicClass.CourseId,
                    LecturerId = academicClass.LecturerId,
                    ClassCode = academicClass.ClassCode,
                    ClassName = academicClass.ClassName,
                    Students = students
                };
            },
            "Không thể tải danh sách sinh viên của lớp.");
    }

    public Task<ServiceResponse<ExamSessionDetailResponse>> CreateAsync(
        ExamSessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var input = await BuildCreateInputAsync(request, cancellationToken);
                var created = await examSessionRepository.CreateAsync(input, cancellationToken);
                return MapDetail(created);
            },
            "Không thể tạo lịch thi.");
    }

    public Task<ServiceResponse<ExamSessionDetailResponse>> RescheduleAsync(
        ExamSessionRescheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);

                var candidateId = BusinessValidation.PositiveId(request.CandidateId, "lượt thi cần đổi giờ");
                if (request.ScheduledTime == default)
                {
                    throw new BusinessValidationException("Vui lòng chọn giờ bắt đầu mới.");
                }

                if (request.ScheduledTime < DateTime.Now)
                {
                    throw new BusinessValidationException("Giờ bắt đầu mới không được nằm trong quá khứ.");
                }

                var session = await examSessionRepository.GetDetailByCandidateIdAsync(
                        candidateId, cancellationToken)
                    ?? throw new BusinessValidationException($"Không tìm thấy lượt thi #{candidateId}.");
                EnsureCandidateCanBeRescheduled(session, candidateId, request.ScheduledTime);

                var updated = await examSessionRepository.RescheduleAsync(
                    candidateId,
                    request.ScheduledTime,
                    cancellationToken);

                return MapDetail(updated);
            },
            "Không thể đổi khung giờ.");
    }

    public Task<ServiceResponse> DeleteAsync(int examId, CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var session = await examSessionRepository.GetDetailAsync(examId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lịch thi.");
                EnsureSessionEditable(session);

                await examSessionRepository.DeleteAsync(examId, cancellationToken);
            },
            "Không thể xoá lịch thi.");
    }

    public Task<ServiceResponse<IReadOnlyList<PersonResponse>>> SearchStudentsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<PersonResponse>>(
            async () =>
            {
                var term = BusinessValidation.OptionalText(query, "email hoặc tên sinh viên", 255)
                    ?? string.Empty;
                var matches = await catalogRepository.SearchUsersAsync(
                    StudentRole,
                    term,
                    SearchResultLimit,
                    cancellationToken);

                return matches
                    .Select(user => new PersonResponse
                    {
                        UserId = user.UserId,
                        FullName = user.FullName,
                        Email = user.Email
                    })
                    .ToList();
            },
            "Không thể tìm sinh viên.");
    }

    public Task<ServiceResponse<StudentScheduleResponse>> GetStudentScheduleAsync(
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var schedule = await examSessionRepository.GetStudentScheduleAsync(studentUserId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy sinh viên.");

                return MapSchedule(schedule);
            },
            "Không thể tải lịch thi của sinh viên.");
    }

    /// <summary>
    /// Moving a session to another course only makes sense when everyone already queued in it
    /// studies that course; otherwise the roster and the course would disagree.
    /// </summary>
    private async Task EnsureCandidatesBelongToCourseAsync(
        ExamSessionDetail current,
        int courseId,
        CancellationToken cancellationToken)
    {
        var enrolled = (await catalogRepository.ListStudentIdsInCourseAsync(courseId, cancellationToken)).ToHashSet();
        var outsiders = current.Candidates
            .Where(candidate => !enrolled.Contains(candidate.StudentId))
            .Select(candidate => candidate.StudentName)
            .ToList();
        if (outsiders.Count > 0)
        {
            throw new BusinessValidationException(
                $"Không thể đổi sang môn học này: {string.Join(", ", outsiders)} không thuộc lớp nào của môn.");
        }
    }

    private static void EnsureSessionEditable(ExamSessionDetail session)
    {
        if (!ExamSessionRules.CanEdit(session.Status.ToBusiness())
            || session.Candidates.Any(candidate => candidate.Status.ToBusiness() != CandidateStatus.Waiting))
        {
            throw new BusinessValidationException(
                "Chỉ được sửa hoặc xoá phiên nháp/đã xếp lịch khi tất cả sinh viên còn chờ thi.");
        }
    }

    private static void EnsureUpdatedTimingIsValid(
        ExamSessionDetail current,
        ExamSessionUpdateInput request)
    {
        var timingChanged = current.StartTime != request.StartTime
            || current.TimePerStudent != request.TimePerStudent;
        if (!timingChanged)
        {
            return;
        }

        if (request.StartTime < DateTime.Now)
        {
            throw new BusinessValidationException("Giờ bắt đầu không được nằm trong quá khứ.");
        }

        if (current.Candidates.Count == 0)
        {
            throw new BusinessValidationException("Phiên thi phải có sinh viên để xếp lịch.");
        }

        var end = request.StartTime.AddMinutes(
            (double)request.TimePerStudent * current.Candidates.Count);
        if (end.Date != request.StartTime.Date)
        {
            throw new BusinessValidationException("Tổng thời lượng vượt quá ngày thi.");
        }
    }

    private static void EnsureStatusChangeAllowed(
        ExamSessionDetail session,
        ExamSessionStatus target)
    {
        if (!Enum.IsDefined(target) || !ExamSessionRules.CanTransition(session.Status.ToBusiness(), target))
        {
            throw new BusinessValidationException("Không thể chuyển sang trạng thái đã chọn.");
        }

        if (target == ExamSessionStatus.Completed
            && session.Candidates.Any(candidate =>
                candidate.Status.ToBusiness() is CandidateStatus.Waiting or CandidateStatus.InProgress))
        {
            throw new BusinessValidationException(
                "Chưa thể hoàn thành: còn sinh viên chờ thi hoặc đang thi.");
        }

        if (target == ExamSessionStatus.InProgress && DateTime.Now < session.StartTime)
        {
            throw new BusinessValidationException("Chưa đến giờ bắt đầu phiên thi.");
        }

        if (target == ExamSessionStatus.Cancelled)
        {
            var sitting = session.Candidates.Count(candidate =>
                ExamSessionRules.BlocksCancellation(candidate.Status.ToBusiness()));
            if (sitting > 0)
            {
                throw new BusinessValidationException(
                    $"Còn {sitting} sinh viên đang thi, chưa thể huỷ phiên.");
            }
        }
    }

    private static void EnsureRosterCanChange(ExamSessionDetail session)
    {
        if (!ExamSessionRules.CanEdit(session.Status.ToBusiness()) || session.StartTime <= DateTime.Now)
        {
            throw new BusinessValidationException(
                "Chỉ được thay đổi sinh viên khi phiên thi chưa bắt đầu.");
        }
    }

    private static void EnsureStudentCanBeRemoved(ExamSessionDetail session, int candidateId)
    {
        EnsureRosterCanChange(session);
        var candidate = session.Candidates.FirstOrDefault(item => item.CandidateId == candidateId)
            ?? throw new BusinessValidationException("Sinh viên không còn trong phiên thi này.");
        if (candidate.Status.ToBusiness() != CandidateStatus.Waiting)
        {
            throw new BusinessValidationException(
                "Chỉ được xóa sinh viên đang chờ thi và chưa có dữ liệu bài thi.");
        }
    }

    private static void EnsureStudentCanBeAdded(
        ExamSessionDetail session,
        DateTime scheduledTime)
    {
        EnsureRosterCanChange(session);
        if (scheduledTime <= DateTime.Now)
        {
            throw new BusinessValidationException(
                "Không thể thêm sinh viên vào lịch thi trong quá khứ.");
        }

        if (session.TimePerStudent <= 0
            || scheduledTime.Date != session.StartTime.Date
            || scheduledTime.AddMinutes(session.TimePerStudent).Date != session.StartTime.Date)
        {
            throw new BusinessValidationException("Khung giờ phải nằm trọn trong ngày thi.");
        }
    }

    private static void EnsureCandidateCanBeRescheduled(
        ExamSessionDetail session,
        int candidateId,
        DateTime scheduledTime)
    {
        var candidate = session.Candidates.FirstOrDefault(item => item.CandidateId == candidateId)
            ?? throw new BusinessValidationException($"Không tìm thấy lượt thi #{candidateId}.");
        if (!ExamSessionRules.CanEdit(session.Status.ToBusiness())
            || candidate.Status.ToBusiness() != CandidateStatus.Waiting)
        {
            throw new BusinessValidationException(
                "Phiên thi hoặc lượt thi không còn được phép đổi giờ.");
        }

        var endTime = scheduledTime.AddMinutes(session.TimePerStudent);
        if (endTime.Date != scheduledTime.Date)
        {
            throw new BusinessValidationException("Khung giờ không được vượt quá ngày thi.");
        }

        if (scheduledTime.Date != session.StartTime.Date)
        {
            throw new BusinessValidationException(
                $"Khung giờ mới phải nằm trong ngày thi {session.StartTime:dd/MM/yyyy}.");
        }
    }

    /// <summary>
    /// Students of the session who will still draw a paper. A cancelled slot or a no-show
    /// never draws one, so they do not count against the bank.
    /// </summary>
    private static int SeatsToDeal(ExamSessionDetail session) =>
        session.Candidates.Count(candidate =>
            candidate.Status.ToBusiness() is not (CandidateStatus.Cancelled or CandidateStatus.Absent));

    /// <summary>
    /// Refuses a session the course's question bank could not serve to the last student,
    /// instead of letting that student find out when they open their slot.
    /// </summary>
    private async Task EnsureBankCoversAsync(
        int courseId,
        int candidateCount,
        int questionsPerCandidate,
        CancellationToken cancellationToken)
    {
        var available = await questionRepository.CountMainPoolAsync(courseId, cancellationToken);
        EnsureQuestionSupply(candidateCount, questionsPerCandidate, available);
    }

    private const int MinTimePerStudent = 1;

    private const int MaxTimePerStudent = 1440;

    /// <summary>
    /// How many distinct questions a session needs: every student gets their own main
    /// questions and no question is dealt twice inside one session, so the bank must hold
    /// students x main questions. Refuses a session it could not serve to the last student.
    /// </summary>
    internal static int RequiredQuestionCount(int candidateCount, int questionsPerCandidate) =>
        Math.Max(candidateCount, 0) * Math.Max(questionsPerCandidate, 0);

    /// <summary>Refuses a session the bank could not serve to the last student.</summary>
    internal static void EnsureQuestionSupply(int candidateCount, int questionsPerCandidate, int available)
    {
        var required = RequiredQuestionCount(candidateCount, questionsPerCandidate);
        if (available >= required)
        {
            return;
        }

        throw new BusinessValidationException(
            $"Ngân hàng chỉ có {available} câu hỏi chính, không đủ {required} câu khác nhau "
            + $"cho {candidateCount} sinh viên × {questionsPerCandidate} câu. "
            + "Hãy bổ sung câu hỏi hoặc giảm số câu hỏi chính.");
    }

    /// <summary>
    /// Turns "who sits the exam, from when, how long each" into back-to-back slots: each
    /// student starts exactly when the previous one finishes.
    /// </summary>
    internal static IReadOnlyList<ScheduleSlot> GenerateSlots(
        DateTime firstStartTime,
        IEnumerable<int> studentIds,
        int timePerStudentMinutes)
    {
        ArgumentNullException.ThrowIfNull(studentIds);

        if (timePerStudentMinutes is < MinTimePerStudent or > MaxTimePerStudent)
        {
            throw new ArgumentException(
                $"Thời lượng mỗi sinh viên phải từ {MinTimePerStudent} đến {MaxTimePerStudent} phút.");
        }

        var ids = studentIds.ToList();
        if (ids.Count == 0)
        {
            throw new ArgumentException("Phải có ít nhất một sinh viên.");
        }

        var seen = new HashSet<int>();
        var currentStart = firstStartTime;
        var slots = new List<ScheduleSlot>(ids.Count);

        foreach (var studentId in ids)
        {
            if (studentId <= 0)
            {
                throw new ArgumentException("Mã sinh viên không hợp lệ.");
            }

            if (!seen.Add(studentId))
            {
                throw new ArgumentException("Mỗi sinh viên chỉ được xuất hiện một lần.");
            }

            var endTime = currentStart.AddMinutes(timePerStudentMinutes);
            slots.Add(new ScheduleSlot(studentId, currentStart, endTime));
            currentStart = endTime;
        }

        return slots;
    }

    /// <summary>
    /// Validates the request, generates the back-to-back slots and checks every referenced
    /// record exists before the repository is asked to persist anything.
    /// </summary>
    private async Task<ExamSessionAggregateInput> BuildCreateInputAsync(
        ExamSessionCreateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
        var lecturerId = BusinessValidation.PositiveId(request.LecturerId, "giảng viên");
        var classId = BusinessValidation.PositiveId(request.ClassId, "lớp học");
        var examName = BusinessValidation.RequiredText(request.ExamName, "tên lịch thi", 200);
        var description = BusinessValidation.OptionalText(request.Description, "mô tả", 1000);

        if (request.StartTime == default)
        {
            throw new BusinessValidationException("Vui lòng chọn ngày giờ bắt đầu.");
        }

        if (request.StartTime < DateTime.Now)
        {
            throw new BusinessValidationException("Giờ bắt đầu không được nằm trong quá khứ.");
        }

        var mainQuestionCount = BusinessValidation.InRange(request.MainQuestionCount, 1, 50, "Số câu hỏi chính");
        var maxFollowUpCount = BusinessValidation.InRange(request.MaxFollowUpCount, 0, 50, "Số câu hỏi phụ tối đa");

        var academicClass = await catalogRepository.GetClassWithStudentsAsync(classId, cancellationToken)
            ?? throw new BusinessValidationException("Không tìm thấy lớp học đang hoạt động.");

        if (academicClass.CourseId != courseId)
        {
            throw new BusinessValidationException(
                $"Lớp {academicClass.ClassCode} không thuộc môn học đã chọn.");
        }

        if (academicClass.LecturerId != lecturerId)
        {
            throw new BusinessValidationException(
                $"Lớp {academicClass.ClassCode} không thuộc giảng viên đã chọn.");
        }

        var activeStudents = academicClass.Students
            .Select(item => item.Student)
            .Where(student => student.Status == "Active" && student.Role.RoleName == StudentRole)
            .ToList();

        var duplicateStudents = activeStudents
            .GroupBy(student => student.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => group.First().FullName)
            .ToList();
        if (duplicateStudents.Count > 0)
        {
            throw new BusinessValidationException(
                $"Danh sách lớp có sinh viên bị trùng: {string.Join(", ", duplicateStudents)}.");
        }

        var studentIds = activeStudents
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.Email)
            .Select(student => student.UserId)
            .ToList();
        if (studentIds.Count == 0)
        {
            throw new BusinessValidationException($"Lớp {academicClass.ClassCode} chưa có sinh viên đang hoạt động.");
        }

        IReadOnlyList<ScheduleSlot> slots;
        try
        {
            slots = GenerateSlots(request.StartTime, studentIds, request.TimePerStudent);
        }
        catch (ArgumentException exception)
        {
            throw new BusinessValidationException(exception.Message);
        }

        if (slots[^1].EndTime.Date != request.StartTime.Date)
        {
            throw new BusinessValidationException(
                "Tổng thời lượng vượt quá ngày thi. Hãy bắt đầu sớm hơn, giảm thời lượng hoặc bớt sinh viên.");
        }

        if (!await catalogRepository.CourseExistsAsync(courseId, cancellationToken))
        {
            throw new BusinessValidationException("Không tìm thấy môn học.");
        }

        if (!await catalogRepository.UserIsInRoleAsync(lecturerId, LecturerRole, cancellationToken))
        {
            throw new BusinessValidationException("Tài khoản được chọn không phải là giảng viên.");
        }

        await EnsureBankCoversAsync(courseId, studentIds.Count, mainQuestionCount, cancellationToken);

        return new ExamSessionAggregateInput
        {
            CourseId = courseId,
            LecturerId = lecturerId,
            ExamName = examName,
            Description = description,
            StartTime = slots[0].StartTime,
            EndTime = slots[^1].EndTime,
            TimePerStudent = request.TimePerStudent,
            MainQuestionCount = mainQuestionCount,
            MaxFollowUpCount = maxFollowUpCount,
            Status = ExamSessionStatus.Scheduled.ToDataAccess(),
            Candidates = slots.Select(slot => new ExamCandidateInput
            {
                StudentId = slot.StudentId,
                ScheduledTime = slot.StartTime,
                Status = CandidateStatus.Waiting.ToDataAccess()
            }).ToList()
        };
    }

    private static ExamSessionListItemResponse MapListItem(ExamSessionListItem item) => new()
    {
        LecturerId = item.LecturerId,
        ExamId = item.ExamId,
        ExamName = item.ExamName,
        StartTime = item.StartTime,
        EndTime = item.EndTime,
        TimePerStudent = item.TimePerStudent,
        Status = item.Status.ToBusiness(),
        LecturerName = item.LecturerName,
        CourseCode = item.CourseCode,
        CourseName = item.CourseName,
        CandidateCount = item.CandidateCount
    };

    private static ExamSessionDetailResponse MapDetail(ExamSessionDetail detail) => new()
    {
        ExamId = detail.ExamId,
        ExamName = detail.ExamName,
        Description = detail.Description,
        StartTime = detail.StartTime,
        EndTime = detail.EndTime,
        TimePerStudent = detail.TimePerStudent,
        MainQuestionCount = detail.MainQuestionCount,
        MaxFollowUpCount = detail.MaxFollowUpCount,
        Status = detail.Status.ToBusiness(),
        CreatedAt = detail.CreatedAt,
        Lecturer = new PersonResponse
        {
            UserId = detail.Lecturer.UserId,
            FullName = detail.Lecturer.FullName,
            Email = detail.Lecturer.Email
        },
        Course = new CourseResponse
        {
            CourseId = detail.Course.CourseId,
            CourseCode = detail.Course.CourseCode,
            CourseName = detail.Course.CourseName,
            Description = detail.Course.Description
        },
        Candidates = detail.Candidates.Select(item => new ExamCandidateResponse
        {
            CandidateId = item.CandidateId,
            StudentId = item.StudentId,
            StudentName = item.StudentName,
            StudentEmail = item.StudentEmail,
            ScheduledTime = item.ScheduledTime,
            EndTime = item.ScheduledTime?.AddMinutes(detail.TimePerStudent),
            Status = item.Status.ToBusiness()
        }).ToList()
    };

    private static StudentScheduleResponse MapSchedule(StudentSchedule schedule) => new()
    {
        StudentId = schedule.StudentId,
        StudentName = schedule.StudentName,
        StudentEmail = schedule.StudentEmail,
        Items = schedule.Items.Select(item => new StudentScheduleItemResponse
        {
            ExamId = item.ExamId,
            CandidateId = item.CandidateId,
            ExamName = item.ExamName,
            CourseCode = item.CourseCode,
            CourseName = item.CourseName,
            LecturerId = item.LecturerId,
            LecturerName = item.LecturerName,
            ScheduledTime = item.ScheduledTime,
            EndTime = item.ScheduledTime.AddMinutes(item.TimePerStudent),
            SessionStatus = item.SessionStatus.ToBusiness(),
            CandidateStatus = item.CandidateStatus.ToBusiness()
        }).ToList()
    };
}

/// <summary>One student's slot in a session: who sits, from when to when.</summary>
public sealed record ScheduleSlot(int StudentId, DateTime StartTime, DateTime EndTime);
