using GrocerEase.Application.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

var app = builder.Build();

app.MapControllers();

app.Run();
