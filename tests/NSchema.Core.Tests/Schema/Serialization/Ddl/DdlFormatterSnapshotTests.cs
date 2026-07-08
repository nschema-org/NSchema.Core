using NSchema.Schema.Ddl;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// Snapshot coverage for <see cref="DdlFormatter"/> over the template statement family.
/// </summary>
public sealed class DdlFormatterSnapshotTests
{
    [Fact]
    public Task Format_TemplateDocument()
    {
        // A deliberately messy document: a template holding several statement kinds (with comments), an
        // application, and surrounding ordinary statements — the snapshot pins the canonical layout.
        const string source =
            """
            create schema billing;
              create schema ordering;
            --- Transactional outbox, one per subdomain schema.
            template outbox begin
                create enum outbox_status('pending','sent');
              create table outbox(
                  id uuid not null,
                status outbox_status not null,  -- current delivery state
                    payload text not null,
                constraint pk_outbox primary key(id));
              -- covering index for the dispatcher
             create index ix_outbox_status on outbox(status);
                grant select, insert on outbox to svc;
                end;
                apply template outbox in schema billing,   ordering;
            """;

        return Verify(DdlFormatter.Instance.Format(source));
    }
}
