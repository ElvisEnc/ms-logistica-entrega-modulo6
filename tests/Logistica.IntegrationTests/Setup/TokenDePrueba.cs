using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Logistica.IntegrationTests.Setup;

/// <summary>
/// JWT firmado igual que los que emite el API Gateway (seccion Jwt de appsettings.json), para
/// probar los endpoints protegidos sin levantar el Gateway.
/// </summary>
public static class TokenDePrueba
{
	public static string Generar(string rol)
	{
		var credenciales = new SigningCredentials(
			new SymmetricSecurityKey(Encoding.UTF8.GetBytes("nurtricenter-secreto-compartido-2026")),
			SecurityAlgorithms.HmacSha256);

		var token = new JwtSecurityToken(
			issuer: "nurtricenter-gateway",
			audience: "nurtricenter-api",
			claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, "tests"), new Claim("rol", rol) },
			expires: DateTime.UtcNow.AddMinutes(30),
			signingCredentials: credenciales);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
