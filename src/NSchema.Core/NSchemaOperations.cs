using NSchema.Diagnostics;
using NSchema.Operations;
using NSchema.Operations.Apply;
using NSchema.Operations.Destroy;
using NSchema.Operations.Doctor;
using NSchema.Operations.Drift;
using NSchema.Operations.Import;
using NSchema.Operations.Plan;
using NSchema.Operations.PlanDestroy;
using NSchema.Operations.Refresh;
using NSchema.Operations.Validate;

namespace NSchema;

/// <summary>
/// Aggregates the individual <see cref="IOperation{TArgs,TResult}"/> services behind the single <see cref="INSchemaOperations"/> surface.
/// </summary>
internal sealed class NSchemaOperations(
    IOperation<PlanArguments, Result<PlanResult>> plan,
    IOperation<PlanDestroyArguments, Result<PlanResult>> planDestroy,
    IOperation<ApplyArguments, Result<IMigrationPlan>> apply,
    IOperation<DestroyArguments, Result<IMigrationPlan>> destroy,
    IOperation<RefreshArguments, Result> refresh,
    IOperation<ValidateArguments, Result> validate,
    IOperation<DriftArguments, Result<DriftResult>> drift,
    IOperation<ImportArguments, Result> import,
    IOperation<DoctorArguments, Result> doctor
) : INSchemaOperations
{
    public Task<Result<PlanResult>> Plan(PlanArguments args, CancellationToken cancellationToken = default) =>
        plan.Execute(args, cancellationToken);

    public Task<Result<PlanResult>> PlanDestroy(PlanDestroyArguments args, CancellationToken cancellationToken = default) =>
        planDestroy.Execute(args, cancellationToken);

    public Task<Result<IMigrationPlan>> BeginApply(ApplyArguments args, CancellationToken cancellationToken = default) =>
        apply.Execute(args, cancellationToken);

    public Task<Result<IMigrationPlan>> BeginDestroy(DestroyArguments args, CancellationToken cancellationToken = default) =>
        destroy.Execute(args, cancellationToken);

    public Task<Result> Refresh(RefreshArguments args, CancellationToken cancellationToken = default) =>
        refresh.Execute(args, cancellationToken);

    public Task<Result> Validate(ValidateArguments args, CancellationToken cancellationToken = default) =>
        validate.Execute(args, cancellationToken);

    public Task<Result<DriftResult>> Drift(DriftArguments args, CancellationToken cancellationToken = default) =>
        drift.Execute(args, cancellationToken);

    public Task<Result> Import(ImportArguments args, CancellationToken cancellationToken = default) =>
        import.Execute(args, cancellationToken);

    public Task<Result> Doctor(DoctorArguments args, CancellationToken cancellationToken = default) =>
        doctor.Execute(args, cancellationToken);
}
