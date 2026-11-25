using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderAccumulator.Infrastructure.Fix;
using OrderProcessing.Domain;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.Configure<FixAcceptorOptions>(builder.Configuration.GetSection("FixAcceptor"));
builder.Services.AddSingleton<OrderProcessor>();
builder.Services.AddSingleton<FixApplication>();
builder.Services.AddHostedService<FixAcceptorService>();

await builder.Build().RunAsync();
