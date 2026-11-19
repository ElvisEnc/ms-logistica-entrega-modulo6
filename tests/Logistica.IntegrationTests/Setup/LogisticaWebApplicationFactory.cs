using Logistica.Application.Abstractions;
using Logistica.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Logistica.IntegrationTests.Setup;

public sealed class LogisticaWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
		.Build();

	// IT-04: publicador capturador accesible desde los tests para afirmar eventos.
	public CapturingIntegrationEventPublisher Publisher =>
		Services.GetRequiredService<CapturingIntegrationEventPublisher>();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		// Modulo 5: sin broker ni Consul en los tests; los eventos se capturan en memoria.
		builder.UseSetting("RabbitMq:Habilitado", "false");
		builder.UseSetting("ServiceDiscovery:ServiceId", "");

		builder.ConfigureServices(services =>
		{
			services.RemoveAll<DbContextOptions<LogisticaDbContext>>();
			services.AddDbContext<LogisticaDbContext>(options =>
				options.UseNpgsql(_postgres.GetConnectionString()));

			// IT-04: sustituir LoggingIntegrationEventPublisher por el publicador capturador.
			// Se registra como Singleton para que la misma instancia sea accesible desde Services
			// y desde todos los scopes de request.
			services.RemoveAll<IIntegrationEventPublisher>();
			services.AddSingleton<CapturingIntegrationEventPublisher>();
			services.AddSingleton<IIntegrationEventPublisher>(sp =>
				sp.GetRequiredService<CapturingIntegrationEventPublisher>());
		});
	}

	// Modulo 5: los endpoints exigen el JWT del API Gateway; el cliente de pruebas lleva uno de admin.
	protected override void ConfigureClient(HttpClient client)
	{
		base.ConfigureClient(client);
		client.DefaultRequestHeaders.Authorization =
			new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenDePrueba.Generar("admin"));
	}

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();

		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<LogisticaDbContext>();
		await db.Database.MigrateAsync();
	}

	async Task IAsyncLifetime.DisposeAsync()
	{
		await _postgres.DisposeAsync();
	}
}
