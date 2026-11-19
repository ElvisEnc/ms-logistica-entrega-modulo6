namespace Logistica.Infrastructure.Inbox;

/// <summary>
/// Fila de la tabla EventosProcesados (patrón Inbox, complemento del Outbox del productor):
/// cada mensaje recibido de RabbitMQ se registra por su MessageId. RabbitMQ garantiza entrega
/// "al menos una vez", así que si el mismo mensaje llega de nuevo se reconoce y se descarta.
/// Guarda además el payload, que sirve como historial consultable de lo recibido.
/// </summary>
public class EventoProcesado
{
	public string MessageId { get; set; } = string.Empty;
	public string RoutingKey { get; set; } = string.Empty;
	public string Payload { get; set; } = string.Empty;
	public DateTime RecibidoEn { get; set; }
}
