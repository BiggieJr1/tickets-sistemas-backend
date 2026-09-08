using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TicketsSistemas.Api.Models;

namespace TicketsSistemas.Api.Services;

// Nombre del claim que dice si el colaborador es administrador. Se usa tanto
// al generar el token (aquí) como al armar la policy de autorización
// (Program.cs) y en el frontend no hace falta: solo importa server-side.
public static class ClaimesColaborador
{
    public const string EsAdministrador = "esAdministrador";
}

public class JwtService
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromHours(12);

    private readonly SymmetricSecurityKey _key;

    public JwtService(IConfiguration configuration)
    {
        var secret = configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException(
                "Falta configurar JWT_SECRET (clave para firmar los tokens de login). " +
                "Configúrala como variable de entorno JWT_SECRET.");

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public (string Token, DateTime Expira) GenerarToken(Colaborador colaborador)
    {
        var expira = DateTime.UtcNow.Add(Vigencia);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, colaborador.Id.ToString()),
            new Claim(ClaimTypes.Email, colaborador.Email),
            new Claim(ClaimesColaborador.EsAdministrador, colaborador.EsAdministrador ? "true" : "false"),
        };

        var credenciales = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: expira, signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
