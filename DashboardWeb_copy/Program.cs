using DnsBlocker.Core;
using DashboardWeb.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

var dnsServer = new DnsServer();
builder.Services.AddSingleton(dnsServer);
builder.Services.AddSingleton<ILogDataAccess, LogDataAccess>();
builder.Services.AddHostedService<DnsWorkerService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();