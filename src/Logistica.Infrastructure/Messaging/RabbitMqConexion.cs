using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Logistica.Infrastructure.Messaging;

/// <summary>
/// Conexión compartida (Singleton) a RabbitMQ. Se abre de forma perezosa y se vuelve a abrir
/// si se cayó: así el servicio arranca aunque el broker todavía no esté listo, y se recupera
/// solo si RabbitMQ se reinicia (mientras tanto, los eventos esperan en el Outbox).
/// AutomaticRecoveryEnabled queda en false a propósito: la reconexión la manejamos nosotros, y
/// la recuperación automática de RabbitMQ.Client 6.x tiene un bug conocido de concurrencia al
/// hacer Dispose() de canales/conexiones rápidamente (como en tests).
/// </summary>
public sealed class RabbitMqConexion : IDisposable
{
	private readonly ConnectionFactory _factory;
	private readonly ILogger<RabbitMqConexion> _logger;
	private readonly object _lock = new();
	private IConnection? _connection;

	public RabbitMqConexion(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConexion> logger)
	{
		var opciones = options.Value;
		_logger = logger;
		_factory = new ConnectionFactory
		{
			HostName = opciones.HostName,
			Port = opciones.Port,
			UserName = opciones.UserName,
			Password = opciones.Password,
			AutomaticRecoveryEnabled = false,
			DispatchConsumersAsync = true,
			ClientProvidedName = opciones.Cola,
		};
	}

	public IConnection ObtenerConexion()
	{
		lock (_lock)
		{
			if (_connection is { IsOpen: true })
				return _connection;

			try
			{
				_connection?.Dispose();
			}
			catch
			{
				// La conexión anterior ya estaba rota.
			}

			_connection = _factory.CreateConnection();
			_logger.LogInformation("[RabbitMQ] Conectado a {HostName}:{Port}.", _factory.HostName, _factory.Port);
			return _connection;
		}
	}

	public void Dispose()
	{
		try
		{
			_connection?.Close();
			_connection?.Dispose();
		}
		catch
		{
			// Solo puede fallar durante el cierre ordenado; no debe tumbar el shutdown de la app.
		}
	}
}
