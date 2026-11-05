using GrocerEase.Api;
using GrocerEase.Api.Authentication;
using GrocerEase.Api.Cors;
using GrocerEase.Api.Errors;
using GrocerEase.Api.MiddleWare;
using GrocerEase.Api.OpenApi;
using GrocerEase.Api.RateLimiting;
using GrocerEase.Application;
using GrocerEase.Infrastructure;
using GrocerEase.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Puts the W3C trace id and span id on every log line written while a request is running, so the lines of one request
// can be found together and matched to its trace.
builder.Logging.Configure(options => options.ActivityTrackingOptions =
    ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddFrontendCors(builder.Configuration);

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

// First, so the status it logs is the one the client receives, including error bodies written by the middleware below.
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseMiddleware<ErrorHandlingMiddleware>();

// Gives every 4xx or 5xx that has no body (unknown route, wrong method, wrong content type) the API's error shape.
app.UseStatusCodePages(StatusCodeProblem.WriteAsync);

app.UseHttpsRedirection();

// Before the rate limiter and auth so a preflight is answered without a token or a rate-limit slot, and so 401 and
// 429 responses still carry the CORS headers that let the frontend read them.
app.UseCors(FrontendCors.PolicyName);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthEndpoints();

app.Run();

// Public so WebApplicationFactory can host the app in integration tests.
public partial class Program;
