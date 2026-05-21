using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Sandbox.Schemas;

internal static class EventSourcingSchemaExtensions
{
    extension(SchemaBuilder schema)
    {
        /// <summary>
        /// Append-only event store table. Uses a composite PK (aggregate_id, sequence_number) and a
        /// globally-unique IDENTITY sequence for projection checkpointing.
        /// </summary>
        public SchemaBuilder AddEventsTable(string grantRole)
        {
            var events = schema.Table("events")
                .Comment("Append-only event store.")
                .Grant(grantRole, TablePrivilege.AppendOnly);

            events.Column("aggregate_id", SqlType.TypeId).NotNull().Comment("The aggregate ID this event belongs to.");
            events.Column("sequence_number", SqlType.Int).NotNull().Comment("Ordering of events within a single aggregate.");
            events.Column("global_sequence", SqlType.BigInt).NotNull().Identity(startWith: 0, minValue: 0).Comment("Monotonically increasing sequence across all aggregates. Used by projections for checkpointing.");
            events.Column("timestamp", SqlType.DateTimeOffset).NotNull().Comment("UTC timestamp when the event was recorded.");
            events.Column("event_type", SqlType.Text).NotNull().Comment("Discriminator used for deserialization.");
            events.Column("payload", SqlType.Jsonb).NotNull().Comment("JSON-serialized event data.");
            events.Column("user_id", SqlType.Text).Comment("The ID of the user who triggered this event. NULL for system events.");
            events.Column("user_name", SqlType.Text).Comment("The display name of the user who triggered this event. NULL for system events.");
            events.PrimaryKey("pk_events", ["aggregate_id", "sequence_number"]);
            events.Index("ix_events_global_sequence", ["global_sequence"]).Unique();
            return schema;
        }

        /// <summary>
        /// Transactional outbox table for integration events waiting to be published to the message broker.
        /// </summary>
        public SchemaBuilder AddIntegrationEventsTable(string grantRole)
        {
            var table = schema.Table("integration_events")
                .Comment("Outbox table for integration events waiting to be published to the message broker.")
                .Grant(grantRole, TablePrivilege.All);
            table.Column("id", SqlType.TypeId).NotNull().Comment("Primary key.");
            table.Column("event_type", SqlType.Text).NotNull().Comment("The fully qualified .NET type name of the integration event.");
            table.Column("payload", SqlType.Jsonb).NotNull().Comment("The event payload.");
            table.Column("created_at", SqlType.DateTimeOffset).NotNull().Comment("When the event was written to the outbox.");
            table.Column("published_at", SqlType.DateTimeOffset).Comment("When the event was published to the message broker. NULL if not yet published.");
            table.Column("metadata", SqlType.Jsonb).NotNull().Default("'{}'::jsonb")
                .Comment("Envelope metadata propagated to consumers via message broker headers.");
            table.PrimaryKey("pk_integration_events", ["id"]);
            return schema;
        }

        /// <summary>
        /// Projection checkpoint table. Each row tracks the last processed global_sequence for one projection handler.
        /// </summary>
        public SchemaBuilder AddProjectionsTable(string grantRole)
        {
            var table = schema.Table("projections")
                .Comment("Stores the checkpoint for each projection handler.")
                .Grant(grantRole, TablePrivilege.All);
            table.Column("id", SqlType.TypeId).NotNull().Comment("Projection id; defined by each concrete projection handler.");
            table.Column("last_processed_global_sequence", SqlType.BigInt).NotNull().Default("0")
                .Comment("The global_sequence value of the last projected event.");
            table.Column("last_updated", SqlType.DateTimeOffset).NotNull().Comment("When the checkpoint was last updated.");
            table.PrimaryKey("pk_projections", ["id"]);
            return schema;
        }
    }
}
