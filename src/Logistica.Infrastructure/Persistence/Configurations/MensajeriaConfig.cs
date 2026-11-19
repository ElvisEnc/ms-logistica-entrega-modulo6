using Logistica.Infrastructure.Inbox;
using Logistica.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Logistica.Infrastructure.Persistence.Configurations;

// Transactional Outbox: la fila se inserta en el mismo SaveChanges que los
// agregados (UnitOfWork despacha las políticas antes de guardar, §12.6).
public sealed class OutboxMessageConfig : IEntityTypeConfiguration<OutboxMessage>
{
	public void Configure(EntityTypeBuilder<OutboxMessage> builder)
	{
		builder.ToTable("outbox_messages");
		builder.HasKey(m => m.Id);
		builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
		builder.Property(m => m.Tipo).HasColumnName("tipo").HasMaxLength(200).IsRequired();
		builder.Property(m => m.RoutingKey).HasColumnName("routing_key").HasMaxLength(200).IsRequired();
		builder.Property(m => m.Payload).HasColumnName("payload").IsRequired();
		builder.Property(m => m.OcurridoEn).HasColumnName("ocurrido_en").IsRequired();
		builder.Property(m => m.ProcesadoEn).HasColumnName("procesado_en");
		builder.HasIndex(m => m.ProcesadoEn);
	}
}

// Inbox del consumidor: un MessageId procesado no se vuelve a aplicar (§10.5).
public sealed class EventoProcesadoConfig : IEntityTypeConfiguration<EventoProcesado>
{
	public void Configure(EntityTypeBuilder<EventoProcesado> builder)
	{
		builder.ToTable("eventos_procesados");
		builder.HasKey(e => e.MessageId);
		builder.Property(e => e.MessageId).HasColumnName("message_id").HasMaxLength(100);
		builder.Property(e => e.RoutingKey).HasColumnName("routing_key").HasMaxLength(200).IsRequired();
		builder.Property(e => e.Payload).HasColumnName("payload").IsRequired();
		builder.Property(e => e.RecibidoEn).HasColumnName("recibido_en").IsRequired();
	}
}
