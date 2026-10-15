using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.Presentation.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AssignmentPRN.Tests;

public sealed class VoiceRoomHttpTests
{
    [Fact]
    public async Task Real_handlers_bind_json_uploads_and_enforce_csrf_and_audio_ownership()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var seed = await VoiceRoomTests.SeedAsync(clock);
        (await seed.Users.SingleAsync(x => x.UserId == 2)).PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123");
        await seed.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(AssignmentPRN.Presentation.Pages.VoiceRoom.IndexModel).Assembly.GetName().Name,
            EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews(); builder.Services.AddRazorPages();
        builder.Services.AddSignalR(); builder.Services.AddDistributedMemoryCache(); builder.Services.AddSession();
        builder.Services.AddBusiness("Server=unused;Database=unused;User=unused;Password=unused",
            Path.Combine(Path.GetTempPath(), "aives-voice-http-" + Guid.NewGuid().ToString("N")));
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<AivesDbContext>();
        builder.Services.RemoveAll<DbContextOptions<AivesDbContext>>();
        builder.Services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AivesDbContext>>();
        builder.Services.AddDbContext<AivesDbContext>(o => o.UseSqlite(seed.Database.GetDbConnection()));
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddScoped<IExamRealtimeNotifier, SignalRExamNotifier>();
        builder.Services.AddScoped<IVoiceRoomNotifier, SignalRVoiceRoomNotifier>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IDocumentActor, AssignmentPRN.Presentation.Security.SessionDocumentActor>();
        builder.Services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentNotifier>();
        await using var app = builder.Build();
        app.UseRouting(); app.UseSession(); app.MapRazorPages(); app.MapDefaultControllerRoute();
        app.MapHub<VoiceRoomHub>("/hubs/voice-room");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/VoiceRoom/1")).StatusCode);
            var login = await client.GetStringAsync("/Account/Login");
            var loginFields = new Dictionary<string, string> { ["Input.Email"] = "student@example.test", ["Input.Password"] = "Test@123",
                ["__RequestVerificationToken"] = Token(login) };
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent(loginFields))).StatusCode);
            var page = await client.GetStringAsync("/VoiceRoom/1");
            Assert.Contains("voice-record", page);
            Assert.Contains("handler=Upload", page);
            var oldRoom = await client.GetAsync("/ExamRoom/Index/1");
            Assert.Equal(HttpStatusCode.Redirect, oldRoom.StatusCode);
            Assert.Contains("VoiceRoom", oldRoom.Headers.Location!.OriginalString);
            var oldResult = await client.GetAsync("/ExamRoom/Result/1");
            Assert.Equal(HttpStatusCode.Redirect, oldResult.StatusCode);
            Assert.Contains("VoiceRoom", oldResult.Headers.Location!.OriginalString);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/VoiceRoom/1?handler=Sync", (object?)null)).StatusCode);
            client.DefaultRequestHeaders.Add("RequestVerificationToken", Token(page));
            var firstResponse = await client.PostAsJsonAsync("/VoiceRoom/1?handler=Sync", (object?)null);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            var first = (await firstResponse.Content.ReadFromJsonAsync<VoiceRoomState>())!;
            var id = first.Question!.ExamQuestionId;
            var draftResponse = await client.PostAsJsonAsync("/VoiceRoom/1?handler=Sync", new VoiceAnswerRequest(id, "xin chào"));
            Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
            Assert.Equal("xin chào", (await draftResponse.Content.ReadFromJsonAsync<VoiceRoomState>())!.Question!.Transcript);
            byte[] wav = new byte[44]; "RIFF"u8.CopyTo(wav); "WAVE"u8.CopyTo(wav.AsSpan(8));
            using var data = new MultipartFormDataContent();
            data.Add(new StringContent(id.ToString()), "questionId"); data.Add(new StringContent("1"), "durationSeconds");
            var audio = new ByteArrayContent(wav); audio.Headers.ContentType = new("audio/wav"); data.Add(audio, "audio", "untrusted-name.wav");
            var uploaded = await client.PostAsync("/VoiceRoom/1?handler=Upload", data);
            Assert.True(uploaded.IsSuccessStatusCode, await uploaded.Content.ReadAsStringAsync());
            var download = await client.GetAsync($"/VoiceRoom/1?handler=Audio&questionId={id}");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal(wav, await download.Content.ReadAsByteArrayAsync());
            Assert.True(download.Headers.CacheControl!.NoStore);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/VoiceRoom/999?handler=Sync", (object?)null)).StatusCode);
            using var socket = new ClientWebSocket();
            socket.Options.Cookies = handler.CookieContainer;
            await socket.ConnectAsync(new Uri(address.Replace("http://", "ws://") + "/hubs/voice-room"), default);
            await Send(socket, "{\"protocol\":\"json\",\"version\":1}");
            Assert.Contains("{}", await Receive(socket));
            await Send(socket, "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Join\",\"arguments\":[999]}");
            Assert.Contains("error", await Receive(socket));
            await Send(socket, "{\"type\":1,\"invocationId\":\"2\",\"target\":\"Join\",\"arguments\":[1]}");
            Assert.DoesNotContain("error", await Receive(socket));
            var submitted = await client.PostAsJsonAsync("/VoiceRoom/1?handler=Sync", new VoiceAnswerRequest(id, "final", true));
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
            var next = (await submitted.Content.ReadFromJsonAsync<VoiceRoomState>())!;
            Assert.Equal(2, next.Question!.OrderNo);
            Assert.Equal("final", Assert.Single(next.SavedAnswers!).Transcript);
            var notification = await Receive(socket);
            Assert.Contains("VoiceRoomChanged", notification);
            Assert.DoesNotContain("final", notification);
            var retried = await client.PostAsJsonAsync("/VoiceRoom/1?handler=Sync", new VoiceAnswerRequest(id, "late duplicate", true));
            Assert.Equal(next.Question.Deadline, (await retried.Content.ReadFromJsonAsync<VoiceRoomState>())!.Question!.Deadline);
            using var anonymous = new HttpClient { BaseAddress = client.BaseAddress };
            Assert.Equal(HttpStatusCode.Forbidden, (await anonymous.GetAsync($"/VoiceRoom/1?handler=Audio&questionId={id}")).StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Missing antiforgery token");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Task Send(ClientWebSocket socket, string message) =>
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message + "\u001e")), WebSocketMessageType.Text, true, default);

    private static async Task<string> Receive(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[8192];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
