using DM.EntityFrameworkCore.Dialect;
using DM.EntityFrameworkCore.Diagnostics.Internal;
using DM.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

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
    private readonly ITypeMappingSource _typeMappingSource;

    public DmModelValidator(
        ModelValidatorDependencies dependencies,
        RelationalModelValidatorDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
        => _typeMappingSource = relationalDependencies.TypeMappingSource;

    public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        base.Validate(model, logger);

        ValidateIdentityColumns(model);
        ValidateGeneratedConcurrencyTokens(model);
        WarnOnDecimalDefaults(model, logger);
    }


    /// <summary>
    /// Refuses a version column, which the store cannot fill.
    /// </summary>
    /// <remarks>
    /// Measured on 8.1.4.80: `ROWVERSION` is not a type (<c>非法的基类名[ROWVERSION]</c>), `ORA_ROWSCN` does
    /// not exist, and `ROWID` does not change when a row is updated. A property the model marks as generated
    /// on every write therefore describes a token the store never changes: the concurrency check compares a
    /// value that no write replaced, so it never reports a conflict. A computed column is the one shape that
    /// is generated, because the model then names the expression the store evaluates.
    /// </remarks>
    private static void ValidateGeneratedConcurrencyTokens(IModel model)
    {
        foreach (var property in model.GetEntityTypes().SelectMany(entityType => entityType.GetDeclaredProperties()))
        {
            if (property.ValueGenerated != ValueGenerated.OnAddOrUpdate
                || !property.IsConcurrencyToken
                || property.GetComputedColumnSql() is not null)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"The property '{property.DeclaringType.DisplayName()}.{property.Name}' is a version column, but "
                + "Dameng has no column that generates a new value on every write, so the concurrency check would "
                + "compare a value nothing replaced. Mark a token the application sets as a concurrency token "
                + "instead, or give the column a computed expression the store can evaluate.");
        }
    }

    /// <summary>
    /// A decimal property without a precision takes the provider default, and a value that does not
    /// fit is rounded without a word. The warning names each property so the model can be corrected
    /// before data is lost.
    /// </summary>
    private static void WarnOnDecimalDefaults(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        foreach (var property in model.GetEntityTypes().SelectMany(entityType => entityType.GetDeclaredProperties()))
        {
            // A column type set on the property is used verbatim, so the provider default never
            // applies. The annotation is what tells an explicit setting from the type the mapping
            // infers for every property.
            if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(decimal)
                && property.GetPrecision() is null
                && property.FindAnnotation(RelationalAnnotationNames.ColumnType) is null)
            {
                logger.DecimalTypeDefaultWarning(property);
            }
        }
    }

    /// <summary>
    /// A table carries at most one identity column, and the column has to be an integer that nothing
    /// else fills. Each violation is reported against the property that caused it.
    /// </summary>
    private void ValidateIdentityColumns(IModel model)
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

                // Two shapes are refused. A property whose column is not an integer cannot be generated at
                // all, which is what an explicit request on a character key produces. A narrow integer is
                // refused because the store has no identity of that width, so the model would silently read
                // back a value nothing produced. The column, not the property's CLR type, decides: an enum or
                // a wrapped integer converted onto an INT column is generated, while an integer key converted
                // onto a character column is not.
                if (!DmValueGenerationStrategyConvention.IsIdentityType(property, _typeMappingSource)
                    || DmIdentityTypes.IsNarrowInteger(property.GetColumnType()))
                {
                    throw new InvalidOperationException(
                        $"The property '{property.DeclaringType.DisplayName()}.{property.Name}' is configured as an "
                        + $"identity column, but its column is {property.GetColumnType()} for a "
                        + $"{property.ClrType.ShortDisplayName()} property. Dameng generates identity values for an "
                        + "INT or BIGINT column only.");
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
