using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using ExamSessionStatus = AssignmentPRN.DataAccess.Enums.ExamSessionStatus;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;
using AssignmentPRN.Presentation.Hubs;
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
public class RazorModuleTwoHttpTests
{
    [Fact]
    public async Task Razor_pages_preserve_login_forms_ownership_and_antiforgery()
    {
        var file = Path.Combine(Path.GetTempPath(), "asm2-http-" + Guid.NewGuid() + ".db");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(AssignmentPRN.Presentation.Pages.Courses.IndexModel).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews();
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
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IDocumentActor, AssignmentPRN.Presentation.Security.SessionDocumentActor>();
        builder.Services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentNotifier>();
        await using var app = builder.Build();
        app.UseRouting(); app.UseSession(); app.MapRazorPages(); app.MapDefaultControllerRoute(); app.MapHub<ExamUpdatesHub>("/hubs/exam-updates"); app.MapHub<DocumentProcessingHub>("/hubs/document-processing");
        try {
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
                await db.Database.EnsureCreatedAsync();
                db.Roles.AddRange(new Role { RoleId = 1, RoleName = "Lecturer" }, new Role { RoleId = 2, RoleName = "Student" });
                foreach (var id in new[] { 1, 2, 3 }) db.Users.Add(new User { UserId = id, RoleId = id == 3 ? 2 : 1,
                    FullName = "Test " + id, Email = "test" + id + "@example.test", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"), Status = "Active" });
                db.Courses.AddRange(new Course { CourseId = 1, LecturerId = 1, CourseCode = "TEST1", CourseName = "Test course", IsActive = true },
                    new Course { CourseId = 2, LecturerId = 2, CourseCode = "TEST2", CourseName = "Other course", IsActive = true });
                db.AcademicClasses.Add(new AcademicClass { ClassId = 1, CourseId = 1, LecturerId = 1, ClassCode = "CLASS1", ClassName = "Test class", IsActive = true });
                db.ClassStudents.Add(new ClassStudent { ClassId = 1, StudentId = 3, JoinedAt = DateTime.Now });
                db.ExamSessions.AddRange(new ExamSession { ExamId = 1, CourseId = 1, LecturerId = 1, ExamName = "Own exam", StartTime = DateTime.Now.AddHours(1), EndTime = DateTime.Now.AddHours(2), TimePerStudent = 20, MainQuestionCount = 1, Status = ExamSessionStatus.Scheduled },
                    new ExamSession { ExamId = 2, CourseId = 2, LecturerId = 2, ExamName = "Other exam", StartTime = DateTime.Now.AddHours(1), EndTime = DateTime.Now.AddHours(2), TimePerStudent = 20, MainQuestionCount = 1, Status = ExamSessionStatus.Scheduled });
                db.ExamCandidates.Add(new ExamCandidate { CandidateId = 1, ExamId = 1, StudentId = 3, Status = CandidateStatus.Waiting, ScheduledTime = DateTime.Now.AddHours(1) });
                db.Questions.Add(new Question { QuestionId = 1, CourseId = 1, CreatedBy = 1, QuestionText = "Test question", Status = AssignmentPRN.DataAccess.Enums.QuestionStatus.Approved,
                    QuestionType = AssignmentPRN.DataAccess.Enums.QuestionType.Main, Options = [new QuestionOption { OptionText = "Yes", IsCorrect = true }, new QuestionOption { OptionText = "No", IsCorrect = false }] });
                db.Documents.AddRange(new Document { DocumentId = 1, CourseId = 1, UploadedById = 1, FileName = "own.docx", StoragePath = "own.docx", ContentType = "application/docx", Status = DocumentStatus.Completed }, new Document { DocumentId = 2, CourseId = 2, UploadedById = 2, FileName = "other.docx", StoragePath = "other.docx", ContentType = "application/docx", Status = DocumentStatus.Completed });
                await db.SaveChangesAsync();
            }
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Courses")).StatusCode);
            var login = await client.GetStringAsync("/Account/Login");
            Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Account/Login", login, new() { ["Input.Email"] = "test1@example.test", ["Input.Password"] = "Test@123" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Rubrics")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Documents")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Documents/Questions/Review/999?documentId=1")).StatusCode);
            var index = await client.GetStringAsync("/ExamSessions");
            Assert.Contains("Own exam", index); Assert.DoesNotContain("Other exam", index);
            Assert.Contains("Own exam", await client.GetStringAsync("/ExamSessions?handler=Snapshot"));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/ExamSessions/Details?id=2")).StatusCode);
            var details = await client.GetStringAsync("/ExamSessions/Details?id=1");
            Assert.Contains("handler=ChangeStatus", details);
            Assert.Contains("handler=AddStudent", details);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/ExamSessions/Details?handler=ChangeStatus&id=1", new FormUrlEncodedContent(new Dictionary<string,string> { ["status"] = "Cancelled" }))).StatusCode);
            var create = await client.GetStringAsync("/Courses/Create");
            Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Courses/Create", create, new() { ["CourseCode"] = "NEW", ["CourseName"] = "New course", ["LecturerId"] = "2", ["IsActive"] = "true" })).StatusCode);
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
                Assert.Equal(1, (await db.Courses.SingleAsync(x => x.CourseCode == "NEW")).LecturerId);
            }
            using var socket = new ClientWebSocket();
            socket.Options.Cookies = handler.CookieContainer;
            var negotiation = JsonDocument.Parse(await (await client.PostAsync("/hubs/exam-updates/negotiate?negotiateVersion=1", null)).Content.ReadAsStringAsync());
            await socket.ConnectAsync(new Uri(address.Replace("http:", "ws:") + "/hubs/exam-updates?id=" + Uri.EscapeDataString(negotiation.RootElement.GetProperty("connectionToken").GetString()!)), CancellationToken.None);
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.Contains("{}", await Receive(socket));
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":1,\"invocationId\":\"1\",\"target\":\"JoinExam\",\"arguments\":[2]}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.Contains("error", await Receive(socket));
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":1,\"invocationId\":\"2\",\"target\":\"JoinExam\",\"arguments\":[1]}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.DoesNotContain("error", await Receive(socket));
            var settings = await client.GetStringAsync("/ExamSessions/Settings?id=1");
            Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/ExamSessions/Settings", settings, new() { ["Id"] = "1", ["SecondsPerQuestion"] = "60", ["MaxFollowUpsPerQuestion"] = "2" })).StatusCode);
            var signal = await Receive(socket);
            Assert.Contains("ExamChanged", signal); Assert.Contains("settings", signal);
            Assert.DoesNotContain("StudentIds", signal);
            using var docSocket = new ClientWebSocket();
            docSocket.Options.Cookies = handler.CookieContainer;
            using var docNegotiation = JsonDocument.Parse(await (await client.PostAsync("/hubs/document-processing/negotiate?negotiateVersion=1", null)).Content.ReadAsStringAsync());
            await docSocket.ConnectAsync(new Uri(address.Replace("http:", "ws:") + "/hubs/document-processing?id=" + Uri.EscapeDataString(docNegotiation.RootElement.GetProperty("connectionToken").GetString()!)), CancellationToken.None);
            await docSocket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.Contains("{}", await Receive(docSocket));
            await docSocket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":1,\"invocationId\":\"1\",\"target\":\"JoinCourseGroup\",\"arguments\":[2]}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.Contains("error", await Receive(docSocket));
            await docSocket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":1,\"invocationId\":\"2\",\"target\":\"JoinCourseGroup\",\"arguments\":[1]}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.DoesNotContain("error", await Receive(docSocket));
            await docSocket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":1,\"invocationId\":\"3\",\"target\":\"JoinDocumentGroup\",\"arguments\":[2]}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.Contains("error", await Receive(docSocket));
            await docSocket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":1,\"invocationId\":\"4\",\"target\":\"JoinDocumentGroup\",\"arguments\":[1]}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.DoesNotContain("error", await Receive(docSocket));
            await app.Services.GetRequiredService<IDocumentProgressNotifier>().ChangedAsync(new(1, 1, "Completed"));
            Assert.Contains("DocumentProgress", await Receive(docSocket));
            var newExam = await client.GetStringAsync("/ExamSessions/Create");
            Assert.Contains("handler=ClassRoster", newExam);
            var roster = await client.GetStringAsync("/ExamSessions/Create?handler=ClassRoster&classId=1");
            Assert.Contains("test3@example.test", roster);
            var result = await Post(client, "/ExamSessions/Create", newExam, new() { ["CourseId"] = "1", ["ClassId"] = "1", ["LecturerId"] = "1", ["ExamName"] = "Created via page", ["StartTime"] = DateTime.Now.AddDays(1).Date.AddHours(10).ToString("yyyy-MM-ddTHH:mm"), ["TimePerStudent"] = "20", ["MainQuestionCount"] = "1", ["MaxFollowUpCount"] = "0" });
            Assert.True(result.StatusCode == HttpStatusCode.Redirect, await result.Content.ReadAsStringAsync());
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
                Assert.True(await db.ExamSessions.AnyAsync(x => x.ExamName == "Created via page"));
            }
            // The results screens moved off MVC; the old URLs are now Razor Pages and
            // still have to render, still have to check ownership, and still have to
            // keep students out.
            var scoreSheet = await client.GetStringAsync("/ExamResults/Index/1");
            Assert.Contains("Bảng điểm", scoreSheet);
            Assert.Contains("Own exam", scoreSheet);
            Assert.Contains("/ExamResults/Candidate/1", scoreSheet);
            var paper = await client.GetStringAsync("/ExamResults/Candidate/1");
            Assert.Contains("Test 3", paper);
            // Tiêu đề và breadcrumb phải tới được layout. Đặt chúng trong partial thì mất,
            // vì <partial> nhận bản sao ViewData — lỗi thấy được khi chạy thật 15/10/2026:
            // tab trình duyệt chỉ còn " · AIVES".
            foreach (var page in new[] { scoreSheet, paper })
            {
                Assert.DoesNotContain("<title> · AIVES</title>", page);
                Assert.Contains("breadcrumb-trail", page);
            }
            // Razor Pages drops the Index segment, so the way back is /ExamResults/1;
            // both spellings have to reach the same screen.
            Assert.Contains("href=\"/ExamResults/1\"", paper);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ExamResults/1")).StatusCode);
            // Someone else's session is turned away rather than rendered.
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/ExamResults/Index/2")).StatusCode);

            using var student = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            var studentLogin = await student.GetStringAsync("/Account/Login");
            await Post(student, "/Account/Login", studentLogin, new() { ["Input.Email"] = "test3@example.test", ["Input.Password"] = "Test@123" });
            Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/ExamSessions")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/ExamResults/Index/1")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/ExamResults/Candidate/1")).StatusCode);
            // A student's own report is for students; the lecturer reads the paper instead.
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/MyResults/1")).StatusCode);
            // The slot has not been sat yet, so the student is sent back with a message
            // rather than shown an empty report.
            Assert.Equal(HttpStatusCode.Redirect, (await student.GetAsync("/MyResults/1")).StatusCode);
        } finally {
            await app.StopAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(file)) File.Delete(file);
        }
    }
    private static async Task<string> Receive(ClientWebSocket socket) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var bytes = new byte[8192]; var result = await socket.ReceiveAsync(bytes, timeout.Token);
        return Encoding.UTF8.GetString(bytes, 0, result.Count);
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string url, string html, Dictionary<string,string> fields) {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Missing antiforgery token");
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        return client.PostAsync(url, new FormUrlEncodedContent(fields));
    }
}
