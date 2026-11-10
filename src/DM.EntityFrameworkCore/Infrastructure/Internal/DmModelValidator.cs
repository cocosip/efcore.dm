using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DM.EntityFrameworkCore.Infrastructure.Internal;

/// <summary>
/// Rejects a model that describes a column Dameng cannot create.
/// </summary>
/// <remarks>
/// The conventions produce a valid model for the usual cases, so these checks catch what a model can
/// still contradict on its own: a strategy the store cannot honour, or two columns competing for the
/// one identity attribute a table is allowed.
/// <para>
/// The errors are raised while the model is built rather than while a migration runs, because the
/// migration would report the same problem as a failed statement against the server.
/// </para>
/// </remarks>
internal sealed class DmModelValidator : RelationalModelValidator
{
    /// <summary>
    /// The types a Dameng identity column accepts.
    /// </summary>
    private static readonly HashSet<Type> IdentityTypes = [typeof(byte), typeof(sbyte), typeof(short), typeof(int), typeof(long)];

    public DmModelValidator(
        ModelValidatorDependencies dependencies,
        RelationalModelValidatorDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        base.Validate(model, logger);

        ValidateIdentityColumns(model);
    }

    /// <summary>
    /// A table carries at most one identity column, and the column has to be an integer that nothing
    /// else fills. Each violation is reported against the property that caused it.
    /// </summary>
    private static void ValidateIdentityColumns(IModel model)
    {
        // Grouped from the metadata rather than from the relational model, which does not exist yet
        // while a model is being validated.
        var mapped = model.GetEntityTypes()
            .Where(entityType => entityType.GetTableName() is not null)
            .GroupBy(entityType => (Schema: entityType.GetSchema(), Table: entityType.GetTableName()));

        foreach (var table in mapped)
        {
            IProperty? identity = null;

            foreach (var property in table.SelectMany(entityType => entityType.GetDeclaredProperties()))
            {
                if (property.GetDmValueGenerationStrategy() != DmValueGenerationStrategy.IdentityColumn)
                {
                    continue;
                }

                if (!IdentityTypes.Contains(Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType))
                {
                    throw new InvalidOperationException(
                        $"The property '{property.DeclaringType.DisplayName()}.{property.Name}' is configured as an "
                        + $"identity column, but its type is {property.ClrType.ShortDisplayName()}. Dameng generates "
                        + "identity values for an integer column only.");
                }

                if (property.GetDefaultValueSql() is not null)
                {
                    throw new InvalidOperationException(
                        $"The property '{property.DeclaringType.DisplayName()}.{property.Name}' is configured as an "
                        + "identity column and also has a default expression. Dameng cannot create a column that "
                        + "generates its value two ways, so remove one of them.");
                }

                if (identity is not null)
                {
                    throw new InvalidOperationException(
                        $"The table '{table.Key.Table}' has a generated value configured for both "
                        + $"'{identity.DeclaringType.DisplayName()}.{identity.Name}' and "
                        + $"'{property.DeclaringType.DisplayName()}.{property.Name}', but Dameng allows one identity "
                        + "column per table.");
                }

                identity = property;
            }
        }
    }
}
