using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSchema.Comparison;
using NSchema.Migration;
using NSchema.Postgres;
using NSchema.Postgres.Migration;
using NSchema.Postgres.Source;
using NSchema.Sandbox;
using NSchema.Source;

string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__sandbox")
                          ?? throw new InvalidOperationException("Connection string not found in environment variables.");
var dataSource = NpgsqlDataSource.Create(connectionString);

var services = new ServiceCollection()
    .AddLogging(b => b.AddConsole()
        .AddSimpleConsole(o => o.SingleLine = true)
        .SetMinimumLevel(LogLevel.Debug)
    )
    .AddSingleton(dataSource)
    .AddSingleton<ISchemaComparer, DefaultSchemaComparer>()
    .AddSingleton<ISourceSchemaProvider, PostgresSourceSchemaProvider>()
    .AddSingleton<ISchemaMigrator, PostgresSchemaMigrator>()
    .BuildServiceProvider();

var target = Database.GetTarget();
string[] touchedSchemas = target.Schemas.Select(s => s.Name).ToArray();

var sourceProvider = services.GetRequiredService<ISourceSchemaProvider>();
var source = await sourceProvider.GetSchema(touchedSchemas);

var comparer = services.GetRequiredService<ISchemaComparer>();
var plan = comparer.Compare(source, target);

var options = new MigrationOptions(DestructiveActionPolicy.Warn);
var migrator = services.GetRequiredService<ISchemaMigrator>();
await migrator.Migrate(plan, options);
