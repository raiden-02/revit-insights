using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;

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

// EF Core Sqlite (db file lives next to the exe at runtime)
builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "revitinsights.db")}"));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("frontend");
app.MapControllers();
app.Run();

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }
    public DbSet<ModelSummary> Summaries => Set<ModelSummary>();
}

public class ModelSummary
{
    public int Id { get; set; }
    public string ProjectName { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
    public string RevitVersion { get; set; } = "";
    public List<CategoryStat> Categories { get; set; } = new();
}

public class CategoryStat
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public int ModelSummaryId { get; set; }
}
