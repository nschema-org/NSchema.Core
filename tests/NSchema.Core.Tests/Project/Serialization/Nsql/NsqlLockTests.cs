using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Config;
using NSchema.Project.Nsql.Syntax.Lock;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// The lockfile grammar: <c>nschema.lock</c> parses to typed <see cref="LockStatement"/>s, and neither the
/// configuration nor the project grammar mixes into it. Translation into the plugin domain is the CLI's.
/// </summary>
public sealed class NsqlLockTests
{
    private static IReadOnlyList<LockStatement> Read(string source)
    {
        var result = NsqlReader.ReadLock(source);
        result.IsSuccess.ShouldBeTrue();
        return result.Value.Statements;
    }

    [Fact]
    public void ReadLock_Statement_ParsesSourceAndVersion()
    {
        var statement = Read("LOCK ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );")
            .ShouldHaveSingleItem();

        statement.Attributes.Select(a => a.Key).ShouldBe(["source", "version"]);
        statement.Attributes[0].Value.ShouldBeOfType<StringValue>().Value.ShouldBe("NSchema.Postgres");
        statement.Attributes[1].Value.ShouldBeOfType<StringValue>().Value.ShouldBe("5.0.0-alpha.2");
    }

    [Fact]
    public void ReadLock_MultipleStatements_KeepDeclarationOrder()
    {
        var statements = Read(
            """
            LOCK ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );
            LOCK ( source = 'NSchema.Aws',      version = '5.0.0-alpha.2' );
            """);

        statements.Select(s => s.Attributes[0].Value).OfType<StringValue>().Select(v => v.Value)
            .ShouldBe(["NSchema.Postgres", "NSchema.Aws"]);
    }

    [Fact]
    public void ReadLock_KeywordIsCaseInsensitive()
        => Read("lock ( source = 'NSchema.Sqlite', version = '5.0.0-alpha.2' );").ShouldHaveSingleItem();

    [Fact]
    public void ReadLock_WithLabel_IsAnError()
        => NsqlReader.ReadLock("LOCK pg ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("takes no label");

    [Fact]
    public void ReadLock_ConfigStatement_IsAnError()
        => NsqlReader.ReadLock("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.0' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("holds only LOCK statements");

    [Fact]
    public void ReadLock_ProjectStatement_IsAnError()
        => NsqlReader.ReadLock("CREATE SCHEMA app;")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("holds only LOCK statements");

    [Fact]
    public void ReadLock_DuplicateAttribute_IsAnError()
        => NsqlReader.ReadLock("LOCK ( source = 'a', SOURCE = 'b' );")
            .Errors.ShouldHaveSingleItem().Message.ShouldContain("more than once");
}
