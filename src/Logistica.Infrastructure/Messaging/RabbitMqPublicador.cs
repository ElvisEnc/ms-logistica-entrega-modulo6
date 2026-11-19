using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Logistica.Infrastructure.Messaging;

/// <summary>
/// Publica eventos de integración en el exchange tipo "topic" de RabbitMQ. Usa publisher
/// confirms: el método solo retorna cuando el broker confirmó el mensaje, así el Outbox marca
/// la fila como procesada únicamente si el evento quedó realmente en RabbitMQ. Si algo falla,
/// se descarta el canal (el próximo intento reconecta) y la excepción llega al Outbox, que
/// reintenta en el siguiente ciclo.
/// </summary>
public sealed class RabbitMqPublicador : IDisposable
{
	private static readonly TimeSpan TimeoutConfirmacion = TimeSpan.FromSeconds(5);

	private readonly RabbitMqOptions _options;
	private readonly RabbitMqConexion _conexion;
	private readonly object _publishLock = new();
	private IModel? _channel;

	public RabbitMqPublicador(IOptions<RabbitMqOptions> options, RabbitMqConexion conexion)
	{
		_options = options.Value;
		_conexion = conexion;
	}

	public Task PublishRawAsync(string routingKey, string jsonPayload, string messageId, CancellationToken cancellationToken = default)
	{
		var body = Encoding.UTF8.GetBytes(jsonPayload);

		lock (_publishLock)
		{
			try
			{
				var channel = ObtenerCanal();
				var properties = channel.CreateBasicProperties();
				properties.Persistent = true;
				properties.ContentType = "application/json";
				properties.MessageId = messageId;
				properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

				channel.BasicPublish(_options.Exchange, routingKey, properties, body);
				channel.WaitForConfirmsOrDie(TimeoutConfirmacion);
			}
			catch
			{
				DescartarCanal();
				throw;
			}
		}

		return Task.CompletedTask;
	}

	private IModel ObtenerCanal()
	{
		if (_channel is { IsOpen: true })
			return _channel;

		DescartarCanal();
		_channel = _conexion.ObtenerConexion().CreateModel();
		_channel.ConfirmSelect();
		_channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
		return _channel;
	}

	private void DescartarCanal()
	{
		try
		{
			_channel?.Dispose();
		}
		catch
		{
			// El canal ya estaba roto; no hay nada que cerrar.
		}

		_channel = null;
	}

	public void Dispose() => DescartarCanal();
}
