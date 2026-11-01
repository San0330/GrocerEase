using GrocerEase.Api.Errors;
using GrocerEase.Api.MiddleWare;
using GrocerEase.Api.OpenApi;
using GrocerEase.Api.RateLimiting;
using GrocerEase.Application;
using GrocerEase.Infrastructure;
using GrocerEase.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddAuthRateLimiting(builder.Configuration);

// The challenge for a missing or invalid token is written by the JwtBearer handler, so it needs its own hook.
builder.Services.Configure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options => options.Events = new JwtBearerEvents { OnChallenge = UnauthorizedResponse.WriteAsync });

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = MalformedRequestResponse.Create);
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

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Public so WebApplicationFactory can host the app in integration tests.
public partial class Program;
