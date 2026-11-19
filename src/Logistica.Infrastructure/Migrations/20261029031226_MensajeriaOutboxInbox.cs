using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistica.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class MensajeriaOutboxInbox : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "eventos_procesados",
				columns: table => new
				{
					message_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
					routing_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					payload = table.Column<string>(type: "text", nullable: false),
					recibido_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_eventos_procesados", x => x.message_id);
				});

			migrationBuilder.CreateTable(
				name: "outbox_messages",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					routing_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					payload = table.Column<string>(type: "text", nullable: false),
					ocurrido_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
					procesado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_outbox_messages", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_outbox_messages_procesado_en",
				table: "outbox_messages",
				column: "procesado_en");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "eventos_procesados");

			migrationBuilder.DropTable(
				name: "outbox_messages");
		}
	}
}
