using System.Text.Json;
using Logistica.Application.Abstractions;
using Logistica.Application.IntegrationEvents;
using Logistica.Infrastructure.Outbox;
using Logistica.Infrastructure.Persistence;

namespace Logistica.Infrastructure.Messaging;

// Reemplaza a LoggingIntegrationEventPublisher (SISTEMA.md §10.6). No publica: agrega la fila a
// outbox_messages en el MISMO DbContext. Las politicas de IntegrationEventHandlers corren dentro
// de UnitOfWork.CommitAsync, antes de SaveChangesAsync (§12.6), asi que el evento se guarda
// atomicamente con la ruta. La publicacion real a RabbitMQ la hace OutboxPublisherBackgroundService.
public sealed class OutboxIntegrationEventPublisher(LogisticaDbContext context) : IIntegrationEventPublisher
{
	// Routing keys entidad.participio del exchange nurtricenter.events.
	private static readonly Dictionary<Type, string> RoutingKeys = new()
	{
		[typeof(RutaOptimizadaGeneradaIntegrationEvent)] = "ruta.optimizada",
		[typeof(EntregaConfirmadaIntegrationEvent)] = "entrega.confirmada",
		[typeof(IncidenciaEntregaRegistradaIntegrationEvent)] = "incidencia_entrega.registrada",
	};

	public Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct = default)
		where TIntegrationEvent : notnull
	{
		if (!RoutingKeys.TryGetValue(typeof(TIntegrationEvent), out var routingKey))
			throw new InvalidOperationException($"El evento {typeof(TIntegrationEvent).Name} no tiene routing key asignada.");

		context.OutboxMessages.Add(new OutboxMessage
		{
			Id = Guid.NewGuid(),
			Tipo = typeof(TIntegrationEvent).Name,
			RoutingKey = routingKey,
			Payload = JsonSerializer.Serialize(integrationEvent, JsonEventos.Opciones),
			OcurridoEn = DateTime.UtcNow,
			ProcesadoEn = null,
		});

		return Task.CompletedTask;
	}
}
