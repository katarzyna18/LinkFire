using Linkfire.MusicLibrary.Api;
using Linkfire.MusicLibrary.Application;
using Linkfire.MusicLibrary.Infrastructure;
using Linkfire.MusicLibrary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// The service has no authentication and is meant to be explored, so the API explorer is always available.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Music Albums Library v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

await ApplyMigrationsAsync(app);

await app.RunAsync();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MusicLibraryDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Exposes the entry point to the API test project (WebApplicationFactory<Program>).
public partial class Program;
