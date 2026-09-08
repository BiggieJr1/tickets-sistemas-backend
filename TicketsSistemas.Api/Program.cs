using Microsoft.EntityFrameworkCore;
using TicketsSistemas.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// --- Base de datos ---
// Postgres (Supabase). La cadena de conexión NUNCA se hardcodea aquí ni en
// appsettings.json: se configura como variable de entorno
// ConnectionStrings__Default (doble guion bajo = notación de ASP.NET Core
// para "ConnectionStrings:Default"), tomada del proyecto de Supabase.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Falta configurar ConnectionStrings:Default (cadena de conexión de Postgres/Supabase). " +
        "Configúrala como variable de entorno ConnectionStrings__Default.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- CORS ---
// ALLOWED_ORIGINS: lista separada por comas con los dominios del frontend
// permitidos (ej. "https://tu-sitio.netlify.app"). Si no se configura
// (uso interno / desarrollo local) se deja abierto, como antes.
var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Interno", policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// --- API key compartida ---
// Si se configura la variable de entorno API_KEY, todas las rutas /api/*
// exigen el header "X-Api-Key" con ese valor. Pensado para cuando la API
// queda expuesta públicamente (Railway, etc.); si no se configura (uso
// interno en red local) no se exige nada, igual que antes.
var apiKey = builder.Configuration["API_KEY"];

// Permite mandar y recibir los enums (Categoria, Prioridad, Estado)
// como texto ("Red", "Critica") en vez de números en el JSON.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Aplica las migraciones pendientes al arrancar. Se usa Migrate() en vez de
// EnsureCreated() porque EnsureCreated() decide si "ya hay esquema" contando
// cualquier tabla fuera de pg_catalog/information_schema, y en Supabase eso
// incluye las tablas propias de Supabase (auth.*, storage.*, etc.), así que
// nunca llegaba a crear las tablas de esta app.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Interno");

if (!string.IsNullOrEmpty(apiKey))
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var provided = context.Request.Headers["X-Api-Key"].FirstOrDefault();
            if (provided != apiKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("API key inválida o faltante.");
                return;
            }
        }
        await next();
    });
}

app.MapControllers();

// Endpoint simple para verificar que el servicio está vivo (útil para
// healthchecks de Docker o del balanceador).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
