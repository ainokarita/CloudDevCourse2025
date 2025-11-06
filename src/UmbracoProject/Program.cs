using UmbracoProject.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<GiphyService>(client =>
{
    client.BaseAddress = new Uri("https://api.giphy.com/v1/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoSite/1.0");
}).SetHandlerLifetime(TimeSpan.FromMinutes(5));

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseHttpsRedirection();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
