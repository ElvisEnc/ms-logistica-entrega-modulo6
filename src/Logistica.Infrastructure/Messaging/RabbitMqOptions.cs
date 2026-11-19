namespace Logistica.Infrastructure.Messaging;

public class RabbitMqOptions
{
	public string HostName { get; set; } = "localhost";
	public int Port { get; set; } = 5672;
	public string UserName { get; set; } = "guest";
	public string Password { get; set; } = "guest";
	public string Exchange { get; set; } = "nurtricenter.events";

	/// <summary>Cola durable de este microservicio: recibe todas las routing keys que consume.</summary>
	public string Cola { get; set; } = "ms-logistica.eventos";

	/// <summary>Si es false no arrancan ni el relay del Outbox ni el consumidor (tests).</summary>
	public bool Habilitado { get; set; } = true;
}
