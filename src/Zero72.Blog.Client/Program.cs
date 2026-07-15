using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Zero72.Blog.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configuredApiBaseUrl = builder.Configuration["ApiBaseUrl"];
var hostBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseAddress = string.IsNullOrWhiteSpace(configuredApiBaseUrl)
    ? hostBaseAddress
    : Uri.TryCreate(configuredApiBaseUrl, UriKind.Absolute, out var absoluteApiBaseAddress)
        ? absoluteApiBaseAddress
        : new Uri(hostBaseAddress, configuredApiBaseUrl);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseAddress });

await builder.Build().RunAsync();
