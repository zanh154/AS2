using System.Net;
using System.Text.RegularExpressions;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using ExamSessionStatus = AssignmentPRN.DataAccess.Enums.ExamSessionStatus;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;
using AssignmentPRN.Presentation.Hubs;
using AssignmentPRN.Presentation.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;
namespace AssignmentPRN.Tests;

/// <summary>
/// The screens that used to be MVC controllers, driven over real HTTP now that the app
/// runs on Razor Pages alone: every page renders, keeps its old address, and its POST
/// handlers still check antiforgery and redirect where the controller actions did.
/// </summary>
public class RazorPagesOnlyHttpTests
{
    [Fact]
    public async Task Former_mvc_screens_render_and_post_as_razor_pages()
    {
        var file = Path.Combine(Path.GetTempPath(), "asm2-pages-" + Guid.NewGuid() + ".db");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(AssignmentPRN.Presentation.Pages.IndexModel).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorPages();
        builder.Services.AddSignalR();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession();
        builder.Services.AddBusiness("Server=unused;Database=unused;User=unused;Password=unused", Path.GetTempPath());
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<AivesDbContext>();
        builder.Services.RemoveAll<DbContextOptions<AivesDbContext>>();
        builder.Services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AivesDbContext>>();
        builder.Services.AddDbContext<AivesDbContext>(o => o.UseSqlite("Data Source=" + file));
        builder.Services.AddScoped<IExamRealtimeNotifier, SignalRExamNotifier>();
        builder.Services.AddScoped<IVoiceRoomNotifier, SignalRVoiceRoomNotifier>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IDocumentActor, AssignmentPRN.Presentation.Security.SessionDocumentActor>();
        builder.Services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentNotifier>();
        await using var app = builder.Build();
        app.UseRouting(); app.UseSession(); app.MapRazorPages(); app.MapHub<ExamUpdatesHub>("/hubs/exam-updates");
        try {
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
                await db.Database.EnsureCreatedAsync();
                db.Roles.AddRange(new Role { RoleId = 1, RoleName = "Lecturer" }, new Role { RoleId = 2, RoleName = "Student" }, new Role { RoleId = 3, RoleName = "Admin" });
                foreach (var (id, role) in new[] { (1, 1), (3, 2), (4, 2), (5, 3) }) db.Users.Add(new User { UserId = id, RoleId = role,
                    FullName = "Test " + id, Email = "test" + id + "@example.test", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"), Status = "Active" });
                db.Courses.Add(new Course { CourseId = 1, LecturerId = 1, CourseCode = "TEST1", CourseName = "Test course", IsActive = true });
                db.AcademicClasses.Add(new AcademicClass { ClassId = 1, CourseId = 1, LecturerId = 1, ClassCode = "CLASS1", ClassName = "Test class", IsActive = true });
                db.ClassStudents.Add(new ClassStudent { ClassId = 1, StudentId = 3, JoinedAt = DateTime.Now });
                db.ExamSessions.Add(new ExamSession { ExamId = 1, CourseId = 1, LecturerId = 1, ExamName = "Own exam", StartTime = DateTime.Now.AddHours(1), EndTime = DateTime.Now.AddHours(2), TimePerStudent = 20, MainQuestionCount = 1, Status = ExamSessionStatus.Scheduled });
                db.ExamCandidates.Add(new ExamCandidate { CandidateId = 1, ExamId = 1, StudentId = 3, Status = CandidateStatus.Waiting, ScheduledTime = DateTime.Now.AddHours(1) });
                db.CourseMaterials.Add(new CourseMaterial { MaterialId = 1, CourseId = 1, FileName = "slides.pdf", FilePath = "missing/slides.pdf",
                    FileType = AssignmentPRN.DataAccess.Enums.MaterialFileType.PDF, UploadedBy = 1, UploadedAt = DateTime.Now });
                db.Questions.Add(new Question { QuestionId = 1, CourseId = 1, CreatedBy = 1, QuestionText = "Test question", Status = AssignmentPRN.DataAccess.Enums.QuestionStatus.Approved,
                    QuestionType = AssignmentPRN.DataAccess.Enums.QuestionType.Main, Options = [new QuestionOption { OptionText = "Yes", IsCorrect = true }, new QuestionOption { OptionText = "No", IsCorrect = false }] });
                await db.SaveChangesAsync();
            }
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

            using (var lecturer = await SignIn(address, "test1@example.test"))
            {
                Assert.Equal("/Lecturer", await RedirectOf(lecturer, "/"));
                foreach (var path in new[] { "/Lecturer", "/Classes", "/Classes/Create", "/Classes/Edit/1", "/CourseMaterials",
                             "/CourseMaterials/Upload", "/CourseMaterials/Edit/1", "/Questions", "/Questions/Create", "/Questions/Edit/1",
                             "/Questions/Import", "/ExamStudents", "/StudentSchedule", "/StudentSchedule?q=Test" })
                    Assert.True((await lecturer.GetAsync(path)).StatusCode == HttpStatusCode.OK, path);

                // The title and breadcrumb come from the page, so the layout must see them.
                var roster = WebUtility.HtmlDecode(await lecturer.GetStringAsync("/Classes/Roster/1"));
                Assert.Contains("<title>Sinh viên lớp CLASS1 · AIVES</title>", roster);
                Assert.Contains("href=\"/Classes/Edit/1\"", roster);
                Assert.Contains("Test 3", roster);
                Assert.Equal("/Classes/Roster/1", await RedirectOf(lecturer, "/Classes/Details/1"));

                // Courses and ExamSessions moved to Razor Pages earlier; their trail must reach the layout too.
                var course = WebUtility.HtmlDecode(await lecturer.GetStringAsync("/Courses/Create"));
                Assert.Contains("<title>Thêm môn học · AIVES</title>", course);
                Assert.Contains("breadcrumb-trail", course);
                var session = WebUtility.HtmlDecode(await lecturer.GetStringAsync("/ExamSessions/Details/1"));
                Assert.Contains("<title>Own exam · AIVES</title>", session);
                Assert.Contains("href=\"/ExamSessions\"", session);
                Assert.Contains("<title>Phát đề · Own exam · AIVES</title>", WebUtility.HtmlDecode(await lecturer.GetStringAsync("/ExamSessions/Questions/1")));

                Assert.Contains("question_text", await lecturer.GetStringAsync("/Questions/Template"));
                Assert.Contains("Test question", await lecturer.GetStringAsync("/Questions?handler=Snapshot"));
                using (var partial = new HttpRequestMessage(HttpMethod.Get, "/ExamStudents"))
                {
                    partial.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    var body = await (await lecturer.SendAsync(partial)).Content.ReadAsStringAsync();
                    Assert.DoesNotContain("<html", body);
                }

                // A handler without the antiforgery token is refused, as the controller actions were.
                Assert.Equal(HttpStatusCode.BadRequest, (await lecturer.PostAsync("/Classes/Roster/1?handler=RemoveStudent&studentId=3",
                    new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);

                var created = await Post(lecturer, "/Classes/Create", "/Classes/Edit", new() {
                    ["ClassId"] = "0", ["ClassCode"] = "CLASS2", ["ClassName"] = "Second class", ["CourseId"] = "1", ["LecturerId"] = "1", ["IsActive"] = "true" });
                Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
                Assert.StartsWith("/Classes/Roster/", created.Headers.Location!.OriginalString);

                var removed = await Post(lecturer, "/Classes/Roster/1", "/Classes/Roster/1?handler=RemoveStudent", new() { ["studentId"] = "3" });
                Assert.Equal("/Classes/Roster/1", removed.Headers.Location!.OriginalString);
                Assert.Contains("Đã xoá sinh viên khỏi lớp.", WebUtility.HtmlDecode(await lecturer.GetStringAsync("/Classes/Roster/1")));
                var added = await Post(lecturer, "/Classes/Roster/1", "/Classes/Roster/1?handler=AddStudent", new() { ["studentId"] = "3" });
                Assert.Equal("/Classes/Roster/1", added.Headers.Location!.OriginalString);

                var renamed = await Post(lecturer, "/CourseMaterials/Edit/1", "/CourseMaterials/Edit/1", new() {
                    ["MaterialId"] = "1", ["CourseId"] = "1", ["FileName"] = "slides-v2.pdf" });
                Assert.Equal(HttpStatusCode.Redirect, renamed.StatusCode);
                Assert.Contains("slides-v2.pdf", await lecturer.GetStringAsync("/CourseMaterials"));

                var archived = await Post(lecturer, "/Questions", "/Questions?handler=Archive&id=1", new());
                Assert.Equal("/Questions", archived.Headers.Location!.OriginalString);
            }

            using (var admin = await SignIn(address, "test5@example.test"))
            {
                Assert.Equal("/Admin", await RedirectOf(admin, "/"));
                Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Classes")).StatusCode);
            }

            using (var student = await SignIn(address, "test3@example.test"))
            {
                Assert.Equal("/Student", await RedirectOf(student, "/"));
                Assert.Equal(HttpStatusCode.OK, (await student.GetAsync("/Student")).StatusCode);
                Assert.Contains("Own exam", await student.GetStringAsync("/StudentSchedule/Mine"));
                Assert.Equal("/StudentSchedule/Mine", await RedirectOf(student, "/ExamRoom/Enter/1"));
                Assert.StartsWith("/Account/AccessDenied", await RedirectOf(student, "/Classes"));
                Assert.StartsWith("/Account/AccessDenied", await RedirectOf(student, "/Questions/Template"));
            }
        } finally {
            await app.StopAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private static async Task<HttpClient> SignIn(string address, string email)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
        var login = await Post(client, "/Account/Login", "/Account/Login", new() { ["Input.Email"] = email, ["Input.Password"] = "Test@123" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    private static async Task<string?> RedirectOf(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location?.OriginalString;
    }

    /// <summary>Reads the antiforgery token off <paramref name="formPage"/> and posts the fields to <paramref name="target"/>.</summary>
    private static async Task<HttpResponseMessage> Post(HttpClient client, string formPage, string target, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync(formPage);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(
            Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        return await client.PostAsync(target, new FormUrlEncodedContent(fields));
    }
}
