using AssignmentPRN.Business.Interfaces;
using DaContracts = AssignmentPRN.DataAccess.Contracts;
using DaEnums = AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Business;

internal static class DataAccessMappings
{
    public static DaEnums.ExamSessionStatus ToDataAccess(this ExamSessionStatus value) =>
        (DaEnums.ExamSessionStatus)(int)value;

    public static ExamSessionStatus ToBusiness(this DaEnums.ExamSessionStatus value) =>
        (ExamSessionStatus)(int)value;

    public static DaEnums.CandidateStatus ToDataAccess(this CandidateStatus value) =>
        (DaEnums.CandidateStatus)(int)value;

    public static CandidateStatus ToBusiness(this DaEnums.CandidateStatus value) =>
        (CandidateStatus)(int)value;

    public static DaEnums.QuestionDifficulty ToDataAccess(this QuestionDifficulty value) =>
        (DaEnums.QuestionDifficulty)(int)value;

    public static QuestionDifficulty ToBusiness(this DaEnums.QuestionDifficulty value) =>
        (QuestionDifficulty)(int)value;

    public static DaEnums.BloomLevel ToDataAccess(this BloomLevel value) =>
        (DaEnums.BloomLevel)(int)value;

    public static BloomLevel ToBusiness(this DaEnums.BloomLevel value) =>
        (BloomLevel)(int)value;

    public static DaEnums.QuestionType ToDataAccess(this QuestionType value) =>
        (DaEnums.QuestionType)(int)value;

    public static QuestionType ToBusiness(this DaEnums.QuestionType value) =>
        (QuestionType)(int)value;

    public static DaEnums.QuestionStatus ToDataAccess(this QuestionStatus value) =>
        (DaEnums.QuestionStatus)(int)value;

    public static QuestionStatus ToBusiness(this DaEnums.QuestionStatus value) =>
        (QuestionStatus)(int)value;

    public static DaEnums.MaterialFileType ToDataAccess(this MaterialFileType value) =>
        (DaEnums.MaterialFileType)(int)value;

    public static MaterialFileType ToBusiness(this DaEnums.MaterialFileType value) =>
        (MaterialFileType)(int)value;

    public static DaEnums.QuestionSource ToDataAccess(this QuestionSource value) =>
        (DaEnums.QuestionSource)(int)value;

    public static QuestionSource ToBusiness(this DaEnums.QuestionSource value) =>
        (QuestionSource)(int)value;

    public static DaContracts.ExamSessionUpdateInput ToDataAccess(this ExamSessionUpdateInput value) => new()
    {
        ExamId = value.ExamId,
        CourseId = value.CourseId,
        ExamName = value.ExamName,
        Description = value.Description,
        StartTime = value.StartTime,
        TimePerStudent = value.TimePerStudent,
        MainQuestionCount = value.MainQuestionCount,
        MaxFollowUpCount = value.MaxFollowUpCount
    };

    public static DaContracts.ExamStudentSearch ToDataAccess(this ExamStudentSearch value) => new()
    {
        Query = value.Query,
        ExamId = value.ExamId,
        From = value.From,
        To = value.To,
        Status = value.Status?.ToDataAccess(),
        Page = value.Page
    };

    public static ExamStudentSearchResult ToBusiness(this DaContracts.ExamStudentSearchResult value) => new()
    {
        Items = value.Items.Select(item => new ExamStudentRow(
            item.CandidateId,
            item.ExamId,
            item.StudentName,
            item.Email,
            item.ExamName,
            item.CourseName,
            item.ScheduledTime,
            item.Status.ToBusiness())).ToList(),
        Sessions = value.Sessions
            .Select(item => new ExamStudentSessionOption(item.ExamId, item.ExamName))
            .ToList(),
        Total = value.Total,
        Page = value.Page
    };

    public static DaContracts.QuestionPickRequest ToDataAccess(this QuestionPickRequest value) => new()
    {
        CourseId = value.CourseId,
        Count = value.Count,
        MaterialIds = value.MaterialIds,
        Difficulties = value.Difficulties.Select(item => item.ToDataAccess()).ToList(),
        TakenQuestionIds = value.TakenQuestionIds
    };

    public static DaContracts.ExamRoomQuestion ToDataAccess(this ExamRoomQuestion value) => new()
    {
        ExamQuestionId = value.ExamQuestionId,
        OrderNo = value.OrderNo,
        QuestionText = value.QuestionText,
        Difficulty = value.Difficulty.ToDataAccess(),
        Options = value.Options.Select(item => new DaContracts.ExamRoomOption
        {
            OptionId = item.OptionId,
            Text = item.Text
        }).ToList(),
        SelectedOptionId = value.SelectedOptionId,
        ParentExamQuestionId = value.ParentExamQuestionId
    };

    public static ExamRoomQuestion ToBusiness(this DaContracts.ExamRoomQuestion value) => new()
    {
        ExamQuestionId = value.ExamQuestionId,
        OrderNo = value.OrderNo,
        QuestionText = value.QuestionText,
        Difficulty = value.Difficulty.ToBusiness(),
        Options = value.Options.Select(item => new ExamRoomOption
        {
            OptionId = item.OptionId,
            Text = item.Text
        }).ToList(),
        SelectedOptionId = value.SelectedOptionId,
        ParentExamQuestionId = value.ParentExamQuestionId
    };

    public static ExamResultQuestion ToBusiness(this DaContracts.ExamResultQuestion value) => new()
    {
        ExamQuestionId = value.ExamQuestionId,
        OrderNo = value.OrderNo,
        QuestionText = value.QuestionText,
        Difficulty = value.Difficulty.ToBusiness(),
        Options = value.Options.Select(item => new ExamResultOption
        {
            OptionId = item.OptionId,
            Text = item.Text,
            IsCorrect = item.IsCorrect
        }).ToList(),
        SelectedOptionId = value.SelectedOptionId,
        MaterialId = value.MaterialId,
        ParentExamQuestionId = value.ParentExamQuestionId,
        ExpectedAnswer = value.ExpectedAnswer
    };

    public static ExamPaperItem ToBusiness(this DaContracts.ExamPaperItem value) => new()
    {
        CandidateId = value.CandidateId,
        StudentName = value.StudentName,
        StudentEmail = value.StudentEmail,
        ScheduledTime = value.ScheduledTime,
        HasStarted = value.HasStarted,
        Questions = value.Questions.Select(item => new ExamPaperQuestion
        {
            ExamQuestionId = item.ExamQuestionId,
            QuestionId = item.QuestionId,
            OrderNo = item.OrderNo,
            QuestionText = item.QuestionText,
            Difficulty = item.Difficulty.ToBusiness(),
            MaterialName = item.MaterialName,
            IsCompleted = item.IsCompleted
        }).ToList()
    };
}
