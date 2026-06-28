using NSchema.Diagnostics;
using NSchema.Operations;
using NSchema.Operations.Doctor;
using NSchema.Operations.Drift;
using NSchema.Operations.Import;
using NSchema.Operations.Plan;
using NSchema.Operations.Refresh;
using NSchema.Operations.Validate;

namespace NSchema;

/// <summary>
/// Aggregates the individual <see cref="IOperation{TArgs,TResult}"/> services behind the single <see cref="INSchemaOperations"/> surface.
/// </summary>
internal sealed class NSchemaOperations(
    IOperation<PlanArguments, Result<PlanResult>> plan,
    IOperation<PlanResult, Result> apply,
    IOperation<RefreshArguments, Result> refresh,
    IOperation<ValidateArguments, Result> validate,
    IOperation<DriftArguments, Result<DriftResult>> drift,
    IOperation<ImportArguments, Result> import,
    IOperation<DoctorArguments, Result> doctor
) : INSchemaOperations
{
    public Task<Result<PlanResult>> Plan(PlanArguments args, CancellationToken cancellationToken = default) =>
        plan.Execute(args, cancellationToken);

    public Task<Result> Apply(PlanResult plan, CancellationToken cancellationToken = default) =>
        apply.Execute(plan, cancellationToken);

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
