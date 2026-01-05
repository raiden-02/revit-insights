var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for Vite dev server
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("frontend", p => p
        .WithOrigins(
            "http://localhost:5173",     // Vite dev server
            "http://127.0.0.1:5173",     // Vite dev server (IP)
            "http://[::1]:5173",         // Vite dev server (IPv6)
            "http://localhost:4173",     // Vite preview server
            "http://127.0.0.1:4173"      // Vite preview server (IP)
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
    );
});

var app = builder.Build();

// Security Headers Middleware (won't matter for local development but for learning purposes)
app.Use(async (context, next) =>
{
    // X-Content-Type-Options: nosniff
    // Prevents browsers from MIME-sniffing the response away from the declared Content-Type.
    // Mitigates drive-by download attacks where a malicious file is disguised as a safe type.
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // Referrer-Policy: strict-origin-when-cross-origin
    // Controls how much referrer info is sent with requests:
    // - Same-origin: full URL sent
    // - Cross-origin: only origin (no path/query) sent
    // - HTTPS→HTTP: no referrer sent
    // Prevents leaking sensitive URL paths to external sites.
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // X-Frame-Options: DENY
    // Prevents this page from being embedded in <iframe>, <frame>, <embed>, or <object>.
    // Mitigates clickjacking attacks where attackers overlay invisible frames.
    context.Response.Headers["X-Frame-Options"] = "DENY";

    await next();
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("frontend");
app.MapControllers();
app.Run();
