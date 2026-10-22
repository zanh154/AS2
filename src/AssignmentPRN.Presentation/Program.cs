using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Hubs;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// Every screen is a Razor Page; there are no MVC controllers.
builder.Services.AddRazorPages();

// Course materials live in the data access project, deliberately outside wwwroot so the
// static-file middleware cannot serve them: every download goes through the controller,
// which checks the session first. A relative value is resolved against the content root;
// a deployment sets an absolute path, because the project folder only exists in a checkout.
var storageRoot = builder.Configuration["MaterialStorage:RootPath"]
    ?? "../AssignmentPRN.DataAccess/Storage";
storageRoot = Path.GetFullPath(storageRoot, builder.Environment.ContentRootPath);
var publicStorageRoot = Path.GetFullPath(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
if (storageRoot.Equals(publicStorageRoot, StringComparison.OrdinalIgnoreCase)
    || storageRoot.StartsWith(publicStorageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("MaterialStorage:RootPath must be outside wwwroot to protect private recordings.");

builder.Services.AddBusiness(connectionString, storageRoot);
// Allow a configured-size file plus multipart overhead; Business validates the file itself.
var uploadLimit = builder.Configuration.GetValue<long?>("DocumentStorage:MaxFileSizeMB") ?? 50;
if (uploadLimit is < 1 or > 1024) throw new InvalidOperationException("Document size limit must be between 1 and 1024 MB.");
var requestLimit = uploadLimit * 1024L * 1024L + 1024L * 1024L;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = Math.Max(30_000_000L, requestLimit));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = requestLimit);
builder.Services.AddSignalR();
builder.Services.AddScoped<IExamRealtimeNotifier, SignalRExamNotifier>();
builder.Services.AddScoped<IVoiceRoomNotifier, SignalRVoiceRoomNotifier>();
builder.Services.AddSingleton<AssignmentPRN.Business.Services.IGradingNotifier, SignalRGradingNotifier>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IDocumentActor, AssignmentPRN.Presentation.Security.SessionDocumentActor>();
builder.Services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentNotifier>();
builder.Services.PostConfigure<DocumentStorageOptions>(options => {
    options.BasePath = Path.GetFullPath(options.BasePath, builder.Environment.ContentRootPath);
    var publicRoot = Path.GetFullPath(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (options.BasePath.Equals(publicRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || options.BasePath.StartsWith(publicRoot, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("DocumentStorage:BasePath must be outside wwwroot.");
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// A session is only a cached login, not proof that the account is still active. Recheck
// it on every protected request so locking an account takes effect immediately.
app.Use(async (context, next) =>
{
    var userId = context.Session.GetInt32(AssignmentPRN.Presentation.Constants.SessionKeys.UserId);
    if (userId is > 0)
    {
        var auth = context.RequestServices.GetRequiredService<IAuthService>();
        var user = await auth.GetUserByIdAsync(userId.Value);
        var sessionRole = context.Session.GetString(AssignmentPRN.Presentation.Constants.SessionKeys.Role);
        if (user is null || !string.Equals(user.RoleName, sessionRole, StringComparison.OrdinalIgnoreCase))
        {
            context.Session.Clear();
            if (!context.Request.Path.StartsWithSegments("/Account"))
            {
                context.Response.Redirect("/Account/Login");
                return;
            }
        }
    }
    await next();
});

app.MapRazorPages();
app.MapHub<ExamUpdatesHub>("/hubs/exam-updates");
app.MapHub<VoiceRoomHub>("/hubs/voice-room");
app.MapHub<DocumentProcessingHub>("/hubs/document-processing");

app.Run();
