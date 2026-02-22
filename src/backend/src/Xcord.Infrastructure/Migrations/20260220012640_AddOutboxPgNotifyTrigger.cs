using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxPgNotifyTrigger : Migration
    {
        /// <inheritdoc />
        private const string FunctionName = "notify_outbox_event_inserted";
        private const string TriggerName = "trg_outbox_event_inserted";
        private const string TableName = "outbox_events";
        private const string Channel = "outbox_event_inserted";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                CREATE OR REPLACE FUNCTION {FunctionName}()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    PERFORM pg_notify('{Channel}', '');
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql($"""
                CREATE TRIGGER {TriggerName}
                AFTER INSERT ON {TableName}
                FOR EACH ROW EXECUTE FUNCTION {FunctionName}();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {TriggerName} ON {TableName};");
            migrationBuilder.Sql($"DROP FUNCTION IF EXISTS {FunctionName}();");
        }
    }
}
