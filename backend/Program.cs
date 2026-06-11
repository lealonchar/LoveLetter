using LoveLetter.Hubs;
using LoveLetter.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<GameEngine>();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // In production, replace with your Vercel frontend URL
        var origins = builder.Configuration["AllowedOrigins"]?.Split(',')
            ?? ["http://localhost:5173"];

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

app.UseCors();
app.MapHub<GameHub>("/gamehub");

// Health check for Railway
app.MapGet("/", () => "Love Letter server running.");

app.Run();
