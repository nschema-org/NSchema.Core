using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSchema.Migration;
using NSchema.Postgres.Migration;
using NSchema.Postgres.Source;
using NSchema.Source;

namespace NSchema.Postgres;

public static class NSchemaApplicationBuilderExtensions
{
    extension(NSchemaApplicationBuilder builder)
    {
        public NSchemaApplicationBuilder UsePostgresSource(string connectionString)
        {
            builder.Services.AddNpgsqlDataSource(connectionString);
            return builder.AddPostgresCore();
        }

        public NSchemaApplicationBuilder UsePostgresSource(Action<NpgsqlDataSourceBuilder> configure)
        {
            builder.Services.AddNpgsqlDataSource("", configure);
            return builder.AddPostgresCore();
        }

        public NSchemaApplicationBuilder UsePostgresSource(Action<IServiceProvider, NpgsqlDataSourceBuilder> configure)
        {
            builder.Services.AddNpgsqlDataSource("", configure);
            return builder.AddPostgresCore();
        }

        private NSchemaApplicationBuilder AddPostgresCore()
        {
            builder.Services
                .AddSingleton<ISourceSchemaProvider, PostgresSourceSchemaProvider>()
                .AddSingleton<ISchemaMigrator, PostgresSchemaMigrator>();

            return builder;
        }
    }
}
