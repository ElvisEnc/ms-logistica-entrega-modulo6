using System.Net;
using System.Net.Sockets;
using Consul;

namespace Logistica.WebApi.ServiceDiscovery;

/// <summary>
/// Registra esta instancia en Consul (Service Discovery) al arrancar y la da de baja al
/// apagarse (IHostedService.StartAsync/StopAsync). Consul, además, comprueba periódicamente
/// GET /health para saber si la instancia sigue respondiendo; si deja de hacerlo, el Gateway
/// deja de enrutarle tráfico automáticamente, sin tocar ninguna configuración estática.
///
/// ServiceDiscovery:Host vacío = se usa la IP del contenedor en la red de Docker, que es la
/// que alcanzan el Gateway y el health check; así cada réplica se registra por separado.
/// </summary>
public sealed class ServiceRegistration : IHostedService
{
	private readonly IConsulClient _consulClient;
	private readonly IConfiguration _configuration;
	private readonly ILogger<ServiceRegistration> _logger;
	private string? _registrationId;

	public ServiceRegistration(IConsulClient consulClient, IConfiguration configuration, ILogger<ServiceRegistration> logger)
	{
		_consulClient = consulClient;
		_configuration = configuration;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		string? serviceAddress = _configuration.GetValue<string>("ServiceDiscovery:Host");
		int? servicePort = _configuration.GetValue<int?>("ServiceDiscovery:Port");
		string? serviceName = _configuration.GetValue<string>("ServiceDiscovery:ServiceId");

		if (servicePort is null || string.IsNullOrWhiteSpace(serviceName))
		{
			_logger.LogWarning("[ServiceDiscovery] Falta configuración ServiceDiscovery:Port/ServiceId; no se registra en Consul.");
			return;
		}

		if (string.IsNullOrWhiteSpace(serviceAddress))
			serviceAddress = ObtenerIpLocal();

		// Fuera de Docker (dotnet run) el servicio corre en el host y Consul en un contenedor:
		// "localhost" ahí sería el propio contenedor, así que el health check usa
		// host.docker.internal, que es cómo Docker Desktop deja que un contenedor alcance al host.
		string healthCheckAddress = serviceAddress.Replace("localhost", "host.docker.internal");

		var registration = new AgentServiceRegistration
		{
			ID = $"{serviceName}-{Dns.GetHostName()}-{servicePort}",
			Name = serviceName,
			Address = serviceAddress,
			Port = servicePort.Value,
			Tags = new[] { "nurtricenter", "dotnet", "logistica" },
			Check = new AgentServiceCheck
			{
				HTTP = $"http://{healthCheckAddress}:{servicePort}/health",
				Interval = TimeSpan.FromSeconds(10),
				Timeout = TimeSpan.FromSeconds(5),
				DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
				Method = "GET",
			},
		};

		_registrationId = registration.ID;

		// Consul puede arrancar después que este servicio: se reintenta sin bloquear el arranque.
		_ = Task.Run(() => RegistrarConReintentoAsync(registration), CancellationToken.None);
		await Task.CompletedTask;
	}

	private async Task RegistrarConReintentoAsync(AgentServiceRegistration registration)
	{
		for (var intento = 1; intento <= 30; intento++)
		{
			try
			{
				await _consulClient.Agent.ServiceRegister(registration);
				_logger.LogInformation(
					"[ServiceDiscovery] Registrado en Consul como '{ServiceId}' ({Address}:{Port}).",
					registration.Name, registration.Address, registration.Port);
				return;
			}
			catch (Exception ex)
			{
				_logger.LogWarning("[ServiceDiscovery] Consul no disponible ({Mensaje}); reintento {Intento}/30 en 5s.", ex.Message, intento);
				await Task.Delay(TimeSpan.FromSeconds(5));
			}
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(_registrationId))
			return;

		try
		{
			await _consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "[ServiceDiscovery] No se pudo dar de baja del registro en Consul durante el apagado.");
		}
	}

	private static string ObtenerIpLocal() =>
		Dns.GetHostAddresses(Dns.GetHostName())
			.First(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
			.ToString();
}
