using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Logistica.Application.Abstractions;
using Logistica.Infrastructure.Inbox;
using Logistica.Infrastructure.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Logistica.Infrastructure.Messaging;

/// <summary>
/// Consumidor genérico de eventos de integración. Declara UNA cola durable para este
/// microservicio, la enlaza al exchange topic con la routing key de cada
/// IManejadorEventoIntegracion registrado y despacha cada mensaje a su manejador.
///
/// - Inbox idempotente: cada mensaje se registra en EventosProcesados por su MessageId (el Id
///   de la fila del Outbox del productor); si RabbitMQ lo reentrega, se reconoce y se ignora.
/// - Dead-letter queue: un mensaje que no cumple el contrato (JSON inválido, sin manejador) o
///   que vuelve a fallar tras un reintento va a "{cola}.dlq" en vez de perderse o de quedar
///   reintentándose para siempre.
/// - Si RabbitMQ se cae, el bucle reconecta solo y vuelve a consumir.
/// </summary>
public sealed class ConsumidorEventosIntegracion : BackgroundService
{
	private static readonly TimeSpan EsperaReconexion = TimeSpan.FromSeconds(5);

	private readonly RabbitMqOptions _options;
	private readonly RabbitMqConexion _conexion;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<ConsumidorEventosIntegracion> _logger;

	public ConsumidorEventosIntegracion(
		IOptions<RabbitMqOptions> options,
		RabbitMqConexion conexion,
		IServiceScopeFactory scopeFactory,
		ILogger<ConsumidorEventosIntegracion> logger)
	{
		_options = options.Value;
		_conexion = conexion;
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Habilitado)
			return;

		var routingKeys = ObtenerRoutingKeys();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using var channel = _conexion.ObtenerConexion().CreateModel();
				DeclararTopologia(channel, routingKeys);

				var cierre = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				channel.ModelShutdown += (_, _) => cierre.TrySetResult();

				var consumer = new AsyncEventingBasicConsumer(channel);
				consumer.Received += (_, ea) => ProcesarAsync(channel, ea, stoppingToken);
				channel.BasicConsume(_options.Cola, autoAck: false, consumer);

				_logger.LogInformation(
					"[RabbitMQ] Consumiendo cola '{Cola}' <- {RoutingKeys}.", _options.Cola, string.Join(", ", routingKeys));

				await Task.WhenAny(cierre.Task, Task.Delay(Timeout.Infinite, stoppingToken));
				if (!stoppingToken.IsCancellationRequested)
					_logger.LogWarning("[RabbitMQ] Se perdió la conexión del consumidor; reconectando...");
			}
			catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
			{
				_logger.LogWarning("[RabbitMQ] Broker no disponible ({Mensaje}); reintento en {Segundos}s.", ex.Message, EsperaReconexion.TotalSeconds);
			}

			try
			{
				await Task.Delay(EsperaReconexion, stoppingToken);
			}
			catch (TaskCanceledException)
			{
				// Apagado normal del servicio.
			}
		}
	}

	private IReadOnlyList<string> ObtenerRoutingKeys()
	{
		using var scope = _scopeFactory.CreateScope();
		return scope.ServiceProvider.GetServices<IManejadorEventoIntegracion>()
			.Select(m => m.RoutingKey)
			.Distinct()
			.ToList();
	}

	private void DeclararTopologia(IModel channel, IReadOnlyList<string> routingKeys)
	{
		var deadLetterExchange = $"{_options.Exchange}.dlx";
		var deadLetterQueue = $"{_options.Cola}.dlq";

		channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
		channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
		channel.ExchangeDeclare(deadLetterExchange, ExchangeType.Direct, durable: true);

		channel.QueueDeclare(deadLetterQueue, durable: true, exclusive: false, autoDelete: false);
		channel.QueueBind(deadLetterQueue, deadLetterExchange, routingKey: _options.Cola);

		channel.QueueDeclare(_options.Cola, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
		{
			["x-dead-letter-exchange"] = deadLetterExchange,
			["x-dead-letter-routing-key"] = _options.Cola,
		});

		foreach (var routingKey in routingKeys)
			channel.QueueBind(_options.Cola, _options.Exchange, routingKey);
	}

	private async Task ProcesarAsync(IModel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
	{
		var json = Encoding.UTF8.GetString(ea.Body.Span);
		var messageId = string.IsNullOrWhiteSpace(ea.BasicProperties?.MessageId)
			? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ea.RoutingKey + json)))
			: ea.BasicProperties.MessageId;

		try
		{
			using var scope = _scopeFactory.CreateScope();
			var context = scope.ServiceProvider.GetRequiredService<LogisticaDbContext>();

			if (await context.EventosProcesados.AnyAsync(e => e.MessageId == messageId, stoppingToken))
			{
				_logger.LogInformation("[Inbox] Mensaje {MessageId} ({RoutingKey}) ya procesado; se ignora.", messageId, ea.RoutingKey);
				channel.BasicAck(ea.DeliveryTag, multiple: false);
				return;
			}

			var manejador = scope.ServiceProvider.GetServices<IManejadorEventoIntegracion>()
				.FirstOrDefault(m => m.RoutingKey == ea.RoutingKey);
			if (manejador is null)
			{
				_logger.LogError("[RabbitMQ] Sin manejador para '{RoutingKey}'; el mensaje va a la DLQ.", ea.RoutingKey);
				channel.BasicReject(ea.DeliveryTag, requeue: false);
				return;
			}

			await manejador.ManejarAsync(json, stoppingToken);

			context.EventosProcesados.Add(new EventoProcesado
			{
				MessageId = messageId,
				RoutingKey = ea.RoutingKey,
				Payload = json,
				RecibidoEn = DateTime.UtcNow,
			});
			await context.SaveChangesAsync(stoppingToken);

			channel.BasicAck(ea.DeliveryTag, multiple: false);
		}
		catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
		{
			_logger.LogError(ex, "[RabbitMQ] Mensaje {MessageId} ({RoutingKey}) no cumple el contrato; va a la DLQ.", messageId, ea.RoutingKey);
			channel.BasicReject(ea.DeliveryTag, requeue: false);
		}
		catch (Exception ex)
		{
			// Primer fallo: se reintenta una vez (puede ser transitorio, p. ej. la base no
			// respondió). Si ya era una reentrega, va a la DLQ para no bloquear la cola.
			_logger.LogError(ex, "[RabbitMQ] Error procesando {MessageId} ({RoutingKey}); redelivered={Redelivered}.",
				messageId, ea.RoutingKey, ea.Redelivered);
			if (ea.Redelivered)
			{
				channel.BasicReject(ea.DeliveryTag, requeue: false);
			}
			else
			{
				await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
				channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
			}
		}
	}
}
