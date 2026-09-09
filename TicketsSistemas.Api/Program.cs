using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using TicketsSistemas.Api.Data;
using TicketsSistemas.Api.Models;

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

// --- Autenticación con Microsoft Entra ID ---
// Ya no se firman JWT propios: el frontend (MSAL.js) hace login contra
// Microsoft y manda como Bearer un access token emitido por Entra ID para el
// scope de esta API (App Registration "tickets-sistemas-api"). Sigue siendo
// Bearer token (no cookie), así que frontend/backend en orígenes distintos
// (Netlify/Railway) no necesitan tocar CORS con credenciales.
// Config vía variables de entorno AzureAd__TenantId / AzureAd__ClientId
// (ClientId = Application ID de la API registrada en Entra), mismo patrón
// que ALLOWED_ORIGINS.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Microsoft.Identity.Web ya registró sus propios manejadores de
// OnTokenValidated (validan issuer/audience/firma contra Entra ID). Se
// encadena uno más: a partir del correo del token de Microsoft, resuelve el
// Colaborador local y le agrega los claims que sí conoce esta app (Id,
// esAdministrador) — así el resto del backend (ColaboradorActualAsync, la
// policy "Administrador") sigue funcionando igual que con el JWT propio.
// También revisa Activo en cada request: desactivar a alguien surte efecto
// de inmediato, sin esperar a que expire su token de Microsoft. Si el correo
// no corresponde a ningún Colaborador dado de alta, rechaza — a propósito no
// hay auto-alta (ver nota del seed más abajo).
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var validacionPrevia = options.Events!.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        if (validacionPrevia is not null) await validacionPrevia(context);
        if (context.Result is not null) return; // ya falló arriba

        var email = context.Principal?.FindFirstValue(ClaimTypes.Upn)
            ?? context.Principal?.FindFirstValue("preferred_username")
            ?? context.Principal?.FindFirstValue(ClaimTypes.Email);

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var colaborador = email is null
            ? null
            : await db.Colaboradores.FirstOrDefaultAsync(c => c.Email.ToLower() == email.Trim().ToLower());

        if (colaborador is null || !colaborador.Activo)
        {
            context.Fail("Cuenta no registrada o desactivada. Contacta a un administrador.");
            return;
        }

        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, colaborador.Id.ToString()));
        identity.AddClaim(new Claim(ClaimesColaborador.EsAdministrador, colaborador.EsAdministrador ? "true" : "false"));
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
    // que hubo que quitar después. Ya no hace falta una contraseña: quien
    // entra con esa cuenta de Microsoft ya se autenticó con Entra ID.
    var seedEmail = app.Configuration["SEED_ADMIN_EMAIL"]?.Trim().ToLower();
    if (!string.IsNullOrWhiteSpace(seedEmail))
    {
        var yaExiste = db.Colaboradores.Any(c => c.Email == seedEmail);
        if (!yaExiste)
        {
            db.Colaboradores.Add(new Colaborador
            {
                NombreCompleto = "Administrador",
                Email = seedEmail,
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
