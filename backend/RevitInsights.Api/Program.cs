var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for Vite dev server
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("frontend", p => p
        .WithOrigins(
            "http://localhost:5173",
            "http://127.0.0.1:5173",
            "http://[::1]:5173"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
    );
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("frontend");
app.MapControllers();
app.Run();
