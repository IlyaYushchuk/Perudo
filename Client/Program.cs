using Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;

using PerudoGame.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) })
	.AddBlazoredLocalStorage();

builder.Services.AddScoped<GameClientLogic>(sp =>
{
	var localStorage = sp.GetRequiredService<ILocalStorageService>();
	return new GameClientLogic("https://localhost:7195/gamehub", sp.GetRequiredService<HttpClient>(), localStorage);
});

await builder.Build().RunAsync();
