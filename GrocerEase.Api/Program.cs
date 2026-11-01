using GrocerEase.Api;
using GrocerEase.Api.Authentication;
using GrocerEase.Api.Errors;
using GrocerEase.Api.MiddleWare;
using GrocerEase.Api.OpenApi;
using GrocerEase.Api.RateLimiting;
using GrocerEase.Application;
using GrocerEase.Infrastructure;
using GrocerEase.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddAuthRateLimiting(builder.Configuration);

builder.Services.AddJwtAuthentication();

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = MalformedRequestResponse.Create;

        // Leave a 4xx such as 415 as a bare status so UseStatusCodePages answers it, instead of MVC attaching its own
        // ProblemDetails with a different shape.
        options.SuppressMapClientErrors = true;
    });
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer<ErrorResponseExamplesTransformer>();
});

var app = builder.Build();

// Off unless asked for: a production database is migrated by a deliberate step, not by whichever instance boots first.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.ApplyMigrationsAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

// Gives every 4xx or 5xx that has no body (unknown route, wrong method, wrong content type) the API's error shape.
app.UseStatusCodePages(StatusCodeProblem.WriteAsync);

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthEndpoints();

app.Run();

// Public so WebApplicationFactory can host the app in integration tests.
public partial class Program;
