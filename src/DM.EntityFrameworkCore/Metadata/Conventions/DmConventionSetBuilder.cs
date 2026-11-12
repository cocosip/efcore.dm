using Microsoft.EntityFrameworkCore.Metadata.Conventions;
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

    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();

        // Dameng limits an identifier to 128 characters; the convention records the limit so the
        // framework truncates a generated table, column, key or index name that would exceed it.
        conventionSet.ModelInitializedConventions.Add(
            new RelationalMaxIdentifierLengthConvention(128, Dependencies, RelationalDependencies));

        conventionSet.ModelFinalizingConventions.Add(new DmValueGenerationStrategyConvention(Dependencies.TypeMappingSource));

        return conventionSet;
    }
}
