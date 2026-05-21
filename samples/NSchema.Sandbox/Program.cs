using Microsoft.Extensions.Logging;
using NSchema;
using NSchema.Migration;
using NSchema.Postgres;
using NSchema.Sandbox;

string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__sandbox")
                          ?? throw new InvalidOperationException("Connection string not found in environment variables.");

await new NSchemaBuilder()
    .ConfigureLogging(b => b.SetMinimumLevel(LogLevel.Debug))
    .UseTarget(Database.GetTarget())
    .UsePostgresSource(connectionString)
    .ConfigureOptions(o => o.DestructiveActionPolicy = DestructiveActionPolicy.Warn)
    .Build()
    .MigrateAsync();
