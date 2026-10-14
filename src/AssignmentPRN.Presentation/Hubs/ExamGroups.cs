namespace AssignmentPRN.Presentation.Hubs;
public static class ExamGroups
{
    public const string Admins = "staff:admins";
    public static string User(int id) => "user:" + id;
    public static string Exam(int id) => "exam:" + id;
}
