namespace Logistica.Application.Abstractions;

// Reaccion de BC6 a un evento de integracion de otro microservicio, recibido por RabbitMQ.
// El consumidor generico (Infrastructure) enlaza la cola a cada RoutingKey registrada,
// garantiza que cada mensaje se procese una sola vez (inbox) y despacha el payload JSON.
public interface IManejadorEventoIntegracion
{
	public string RoutingKey { get; }

	public Task ManejarAsync(string payloadJson, CancellationToken ct);
}
