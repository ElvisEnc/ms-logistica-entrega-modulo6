using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Logistica.Application.PaquetesRecibidos;
using Logistica.Infrastructure;
using Logistica.Infrastructure.Persistence;
using Logistica.WebApi.Middleware;
using Logistica.WebApi.ServiceDiscovery;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
		options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
	});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddLogisticaInfrastructure(builder.Configuration);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
	typeof(Program).Assembly,
	typeof(ProcesarPaquetesListosCommand).Assembly,
	typeof(LogisticaDbContext).Assembly));

// Seguridad (Modulo 5): el token lo emite el API Gateway (POST /auth/login) y aqui se vuelve
// a validar con el mismo secreto, issuer y audience, para que nadie se salte el Gateway
// llamando directo a este puerto. El rol viaja en el claim "rol".
var jwt = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwt["Secret"]
	?? throw new InvalidOperationException("Falta configurar Jwt:Secret (variable de entorno Jwt__Secret).");
builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			ValidIssuer = jwt["Issuer"],
			ValidAudience = jwt["Audience"],
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
			RoleClaimType = "rol",
			NameClaimType = "name",
			ClockSkew = TimeSpan.FromSeconds(30),
		};
	});
builder.Services.AddAuthorization();

// Service Discovery: se registra en Consul con health check a GET /health.
builder.Services.AddHealthChecks().AddDbContextCheck<LogisticaDbContext>();
builder.Services.RegisterServiceToServiceDiscovery(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// §12.12 del maestro: migraciones automaticas en Development. En Docker (Production) se
// activan con Database:MigrarAlArrancar.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Database:MigrarAlArrancar", false))
{
	using var scope = app.Services.CreateScope();
	var db = scope.ServiceProvider.GetRequiredService<LogisticaDbContext>();
	db.Database.Migrate();
}

var carpetaEvidencias = Path.Combine(app.Environment.ContentRootPath, "evidencias");
Directory.CreateDirectory(carpetaEvidencias);
app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(carpetaEvidencias),
	RequestPath = "/evidencias"
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program { }
