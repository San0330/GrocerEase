using GrocerEase.Api.MiddleWare;
using GrocerEase.Api.OpenApi;
using GrocerEase.Application;
using GrocerEase.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer<ErrorResponseExamplesTransformer>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Public so WebApplicationFactory can host the app in integration tests.
public partial class Program;
