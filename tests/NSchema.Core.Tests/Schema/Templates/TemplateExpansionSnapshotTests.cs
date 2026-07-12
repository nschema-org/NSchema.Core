using NSchema.Project.Ddl;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;

namespace NSchema.Tests.Schema.Templates;

/// <summary>
/// Snapshot coverage for <see cref="TemplateApplicator"/>: the expanded schema is rendered back to DDL with
/// <see cref="DdlWriter"/>, so the snapshot shows exactly what the rest of the pipeline sees per target schema —
/// placeholder foreign keys re-pointed, and template-declared types and trigger functions qualified per instance.
/// </summary>
public sealed class TemplateExpansionSnapshotTests
{
    [Fact]
    public Task Expand_OutboxTemplate_IntoTwoSchemas()
    {
        const string source =
            """
            CREATE SCHEMA billing;
            CREATE SCHEMA ordering;

            TEMPLATE audit_columns FOR TABLE
            BEGIN
              created_at timestamptz NOT NULL,
              updated_at timestamptz NOT NULL,
              CONSTRAINT chk_audit CHECK (updated_at >= created_at)
            END;

            TEMPLATE outbox
            BEGIN
              CREATE ENUM outbox_status ('pending', 'sent');
              CREATE FUNCTION outbox_notify() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END; $$;
              CREATE TABLE events (
                id uuid NOT NULL,
                status outbox_status NOT NULL,
                payload text NOT NULL,
                INCLUDE audit_columns,
                CONSTRAINT pk_events PRIMARY KEY (id)
              );
              CREATE TABLE event_locks (
                event_id uuid NOT NULL,
                CONSTRAINT fk_event FOREIGN KEY (event_id) REFERENCES events (id),
                CONSTRAINT fk_owner FOREIGN KEY (event_id) REFERENCES public.owners (id)
              );
              CREATE TRIGGER trg_notify AFTER INSERT ON events FOR EACH ROW EXECUTE FUNCTION outbox_notify();
              GRANT SELECT, INSERT ON events TO svc;
            END;

            APPLY TEMPLATE outbox IN SCHEMA billing, ordering;
            """;

        var document = DdlReader.Instance.Read(source).Require();
        var expanded = TemplateApplicator.Apply(new ProjectDefinition(document.Schema, document.Scripts), document.Templates).Require().Schema;

        return Verify(DdlWriter.Instance.Write(expanded));
    }
}
