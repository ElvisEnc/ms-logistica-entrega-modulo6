using Logistica.Application.Abstractions;
using Logistica.Application.PaquetesRecibidos;
using Logistica.Domain.Repartidores;
using Logistica.Domain.Rutas;
using Logistica.Infrastructure.Messaging;
using Logistica.Infrastructure.Outbox;
using Logistica.Infrastructure.Persistence;
using Logistica.Infrastructure.Persistence.Repositories;
using Logistica.Infrastructure.Storage;
using Joseco.DDD.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logistica.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddLogisticaInfrastructure(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddDbContext<LogisticaDbContext>(options =>
			options.UseNpgsql(configuration.GetConnectionString("Postgres")));

		services.AddScoped<IRutaEntregaRepository, RutaEntregaRepository>();
		services.AddScoped<IRepartidorRepository, RepartidorRepository>();
		services.AddScoped<IPaqueteRecibidoStore, PaqueteRecibidoStore>();
		services.AddScoped<IUnitOfWork, UnitOfWork>();
		services.AddScoped<IEvidenciaStorage, LocalEvidenciaStorage>();

		// SISTEMA.md §10.6 (Modulo 5): RabbitMQ (exchange topic nurtricenter.events) con
		// Transactional Outbox para publicar y un consumidor con inbox idempotente en lugar de
		// POST /api/integracion/paquetes-listos. No hay HTTP entre microservicios.
		services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
		services.AddSingleton<RabbitMqConexion>();
		services.AddSingleton<RabbitMqPublicador>();
		services.AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();
		services.AddHostedService<OutboxPublisherBackgroundService>();
		services.AddScoped<IManejadorEventoIntegracion, PaquetesListosManejador>();
		services.AddHostedService<ConsumidorEventosIntegracion>();

		return services;
	}
}
