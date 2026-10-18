using GrocerEase.Application;
using GrocerEase.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
