using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

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
    ?? throw new InvalidOperationException("Setting 'MaterialStorage:RootPath' is not configured.");
storageRoot = Path.GetFullPath(storageRoot, builder.Environment.ContentRootPath);

builder.Services.AddBusiness(connectionString, storageRoot);

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
app.MapDefaultControllerRoute();

app.Run();
