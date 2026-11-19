using System.Text.Json;
using System.Text.Json.Serialization;

namespace Logistica.Infrastructure.Messaging;

/// <summary>
/// Formato acordado para los payloads en RabbitMQ: camelCase y enums como string. Al leer
/// no se distingue mayúsculas/minúsculas, así que también se aceptan productores en PascalCase.
/// </summary>
public static class JsonEventos
{
	public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() },
	};
}
