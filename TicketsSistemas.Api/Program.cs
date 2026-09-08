using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TicketsSistemas.Api.Data;
using TicketsSistemas.Api.Models;
using TicketsSistemas.Api.Services;

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

// --- Autenticación por JWT ---
// JWT_SECRET: clave simétrica para firmar/validar los tokens de login.
// Bearer token en vez de cookie de sesión porque frontend (Netlify) y
// backend (Railway) son orígenes distintos — evita meter credenciales en
// CORS (SameSite=None, AllowCredentials) encima del ALLOWED_ORIGINS que ya
// es delicado de mantener.
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException(
        "Falta configurar JWT_SECRET (clave para firmar los tokens de login). " +
        "Configúrala como variable de entorno JWT_SECRET.");

builder.Services.AddSingleton<JwtService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        options.Events = new JwtBearerEvents
        {
            // Revisa Activo en cada request (no solo al hacer login): así
            // desactivar a alguien surte efecto de inmediato, sin esperar a
            // que expire su token.
            OnTokenValidated = async context =>
            {
                var idClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (idClaim is null || !int.TryParse(idClaim, out var colaboradorId))
                {
                    context.Fail("Token inválido.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var colaborador = await db.Colaboradores.FindAsync(colaboradorId);
                if (colaborador is null || !colaborador.Activo)
                {
                    context.Fail("Cuenta desactivada o inexistente.");
                }
            }
        };
    });

// Una sola policy con nombre respaldada por el claim "esAdministrador" del
// JWT — suficiente para un equipo chico, sin tabla de permisos aparte.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrador", policy =>
        policy.RequireClaim(ClaimesColaborador.EsAdministrador, "true"));
});

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

    // Siembra el primer administrador si no existe todavía. Es la única
    // forma de crear un colaborador sin ya ser admin, así que se hace por
    // variable de entorno (mismo patrón que ConnectionStrings__Default /
    // ALLOWED_ORIGINS) en vez de un endpoint abierto — que tendería a
    // quedarse ahí "por si acaso", igual que pasó con la API key compartida
    // que hubo que quitar después.
    var seedEmail = app.Configuration["SEED_ADMIN_EMAIL"]?.Trim().ToLower();
    var seedPassword = app.Configuration["SEED_ADMIN_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(seedEmail) && !string.IsNullOrWhiteSpace(seedPassword))
    {
        var yaExiste = db.Colaboradores.Any(c => c.Email == seedEmail);
        if (!yaExiste)
        {
            db.Colaboradores.Add(new Colaborador
            {
                NombreCompleto = "Administrador",
                Email = seedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword),
                EsAdministrador = true,
            });
            db.SaveChanges();
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Interno");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Endpoint simple para verificar que el servicio está vivo (útil para
// healthchecks de Docker o del balanceador).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
