using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSchema.Migration;
using NSchema.Postgres.Migration;
using NSchema.Postgres.Source;
using NSchema.Source;

namespace NSchema.Postgres;

public static class NSchemaBuilderExtensions
{
    public static NSchemaBuilder UsePostgresSource(this NSchemaBuilder builder, string connectionString)
        => builder.UsePostgresSource(NpgsqlDataSource.Create(connectionString));

    public static NSchemaBuilder UsePostgresSource(this NSchemaBuilder builder, NpgsqlDataSource dataSource)
        => builder.ConfigureServices(services => services
            .AddSingleton(dataSource)
            .AddSingleton<ISourceSchemaProvider, PostgresSourceSchemaProvider>()
            .AddSingleton<ISchemaMigrator, PostgresSchemaMigrator>());
}
