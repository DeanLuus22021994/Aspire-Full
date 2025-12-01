using Aspire_Full.Tensor;
using Aspire_Full.WebAssembly.Extensions;
using Aspire_Full.WebAssembly.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.AddServiceDefaults();

builder.RootComponents.Add<Aspire_Full.WebAssembly.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));
builder.Services.AddSingleton<FrontendEnvironmentRegistry>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<RegistryApiClient>();
builder.Services.AddScoped<TensorJobService>();
builder.Services.AddScoped<ITensorExecutionService, TensorExecutionService>();
builder.Services.AddTensorRuntime(builder.Configuration);

await builder.Build().RunAsync();
