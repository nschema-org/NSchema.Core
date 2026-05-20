using Microsoft.Extensions.DependencyInjection;
using NSchema.Diffing;

namespace NSchema.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNSchema(this IServiceCollection services, Action<NSchemaBuilder> configure)
    {
        var builder = new NSchemaBuilder();
        configure(builder);

        services.AddSingleton(
            builder.Extractor
            ?? throw new InvalidOperationException("No schema extractor configured. Call UsePostgres() or UseExtractor()."));

        services.AddSingleton(
            builder.Executor
            ?? throw new InvalidOperationException("No instruction executor configured. Call UsePostgres() or UseExecutor()."));

        services.AddSingleton<ISchemaDiffer, DatabaseModelDiffer>();

        services.AddSingleton(
            builder.Model
            ?? throw new InvalidOperationException("No desired model configured. Call WithModel()."));

        services.AddSingleton(builder.ExecutionOptions);
        services.AddSingleton<ISchemaMigrator, SchemaMigrator>();

        return services;
    }

    public static IServiceCollection AddNSchemaHostedService(this IServiceCollection services)
    {
        services.AddHostedService<SchemaMigratorHostedService>();
        return services;
    }
}
