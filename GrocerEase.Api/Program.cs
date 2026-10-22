using GrocerEase.Api.MiddleWare;
using GrocerEase.Application;
using GrocerEase.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapControllers();

app.Run();
