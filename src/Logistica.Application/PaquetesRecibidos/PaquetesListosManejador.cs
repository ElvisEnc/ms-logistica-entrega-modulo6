using System.Text.Json;
using System.Text.Json.Serialization;
using Logistica.Application.Abstractions;
using Logistica.Application.IntegrationEvents;
using MediatR;

namespace Logistica.Application.PaquetesRecibidos;

// paquetes.listos (ms-produccion-alimentos) por RabbitMQ: reemplaza a
// POST /api/integracion/paquetes-listos (SISTEMA.md §10.6) y manda el MISMO comando, que ya
// es idempotente por paqueteId (I12). El payload se lee contra el record del contrato (§11.8).
public sealed class PaquetesListosManejador(ISender sender) : IManejadorEventoIntegracion
{
	private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	public string RoutingKey => "paquetes.listos";

	public Task ManejarAsync(string payloadJson, CancellationToken ct)
	{
		var contrato = JsonSerializer.Deserialize<PaquetesListosParaEntrega>(payloadJson, Opciones)
			?? throw new JsonException("Payload vacio para PaquetesListosParaEntrega.");

		return sender.Send(new ProcesarPaquetesListosCommand(contrato), ct);
	}
}
