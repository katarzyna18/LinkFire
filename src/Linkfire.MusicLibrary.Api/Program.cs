using Linkfire.MusicLibrary.Application;
using Linkfire.MusicLibrary.Infrastructure;
using Linkfire.MusicLibrary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Expected failures are returned as ProblemDetails by the controllers; these two cover the rest:
// unhandled exceptions (generic 500, no details) and pipeline-generated statuses such as 404 for unknown routes.
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

// Convenient for local development only. Deployed environments run the EF migration bundle as a
// separate step before the app starts, so several replicas never migrate the same database at once.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<MusicLibraryDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

// Exposes the entry point to the API test project (WebApplicationFactory<Program>).
public partial class Program;
