using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);

    var defaultScope = builder.Configuration.GetSection("ApiApp:DefaultScope").Value;
    options.ProviderOptions.DefaultAccessTokenScopes.Add(defaultScope);
});

builder.Services.AddHttpClient<WeatherForecastHttpClient>(client =>
    {
        var baseUrl = builder.Configuration.GetSection("ApiApp:BaseUrl").Value;
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddHttpMessageHandler(sp =>
    {
        var baseUrl = builder.Configuration.GetSection("ApiApp:BaseUrl").Value;

        return sp
            .GetRequiredService<AuthorizationMessageHandler>()
            // Add the endpoints to attach this token to in this ConfigureHandler
            .ConfigureHandler(authorizedUrls: new[] { baseUrl });
    });

await builder.Build().RunAsync();
