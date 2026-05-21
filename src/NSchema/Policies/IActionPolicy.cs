using NSchema.Migration;

namespace NSchema.Policies;

public interface IActionPolicy
{
    IEnumerable<PolicyError> Validate(SchemaPlan plan);
}
