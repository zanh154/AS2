using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Hubs;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddControllersWithViews();

// The question reviewer and rubric editors are Razor Pages; the rest of the app stays MVC.
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
    if (options.BasePath.StartsWith(publicRoot, StringComparison.OrdinalIgnoreCase))
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

app.MapRazorPages();
app.MapHub<ExamUpdatesHub>("/hubs/exam-updates");
app.MapHub<VoiceRoomHub>("/hubs/voice-room");
app.MapHub<DocumentProcessingHub>("/hubs/document-processing");
app.MapDefaultControllerRoute();

app.Run();
