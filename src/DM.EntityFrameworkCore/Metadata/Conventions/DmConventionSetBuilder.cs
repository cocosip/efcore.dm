using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace DM.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Supplies the relational conventions for the Dameng provider. Without a provider
/// convention set builder EF Core falls back to the core conventions, and the relational
/// model is built without the table mappings the query pipeline needs.
/// </summary>
internal sealed class DmConventionSetBuilder : RelationalConventionSetBuilder
{
    public DmConventionSetBuilder(
        ProviderConventionSetBuilderDependencies dependencies,
        RelationalConventionSetBuilderDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }
}
