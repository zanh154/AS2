using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business;

public class ExamSessionService(
    IExamSessionRepository examSessionRepository,
    ICatalogRepository catalogRepository) : IExamSessionService
{
    private const string LecturerRole = "Lecturer";
    private const string StudentRole = "Student";
    private const int SearchResultLimit = 25;

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
                if (await examSessionRepository.GetDetailAsync(examId, cancellationToken) is null)
                {
                    throw new BusinessValidationException("Không tìm thấy lịch thi.");
                }

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
            slots = ExamSchedulePlanner.Generate(request.StartTime, studentIds, request.TimePerStudent);
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
            Status = ExamSessionStatus.Scheduled,
            Candidates = slots.Select(slot => new ExamCandidateInput
            {
                StudentId = slot.StudentId,
                ScheduledTime = slot.StartTime,
                Status = CandidateStatus.Waiting
            }).ToList()
        };
    }

    private static ExamSessionListItemResponse MapListItem(ExamSessionListItem item) => new()
    {
        ExamId = item.ExamId,
        ExamName = item.ExamName,
        StartTime = item.StartTime,
        EndTime = item.EndTime,
        TimePerStudent = item.TimePerStudent,
        Status = item.Status,
        LecturerId = item.LecturerId,
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
        Status = detail.Status,
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
            Status = item.Status
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
            ExamName = item.ExamName,
            CourseCode = item.CourseCode,
            CourseName = item.CourseName,
            LecturerId = item.LecturerId,
            LecturerName = item.LecturerName,
            ScheduledTime = item.ScheduledTime,
            EndTime = item.ScheduledTime.AddMinutes(item.TimePerStudent),
            SessionStatus = item.SessionStatus,
            CandidateStatus = item.CandidateStatus
        }).ToList()
    };
}
