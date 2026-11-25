using System.Net;
using OrderGenerator.Application;
using OrderGenerator.Infrastructure.Fix;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.Configure<FixInitiatorOptions>(builder.Configuration.GetSection("FixInitiator"));
builder.Services.Configure<OrderSubmissionOptions>(builder.Configuration.GetSection("FixInitiator"));
builder.Services.AddSingleton<PendingOrderRegistry>();
builder.Services.AddSingleton<FixApplication>();
builder.Services.AddSingleton<FixInitiatorService>();
builder.Services.AddSingleton<IOrderTransport>(services => services.GetRequiredService<FixInitiatorService>());
builder.Services.AddHostedService(services => services.GetRequiredService<FixInitiatorService>());
builder.Services.AddSingleton<IOrderSubmissionService, OrderSubmissionService>();

var app = builder.Build();
app.Use(async (context, next) =>
{
    bool localDevelopment = app.Environment.IsDevelopment()
        && context.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote)
        && context.Connection.LocalIpAddress is { } local && IPAddress.IsLoopback(local);
    if (!context.Request.IsHttps && !localDevelopment)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    await next();
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
await app.RunAsync();

public partial class Program { }
