using DM.EntityFrameworkCore.Diagnostics.Internal;
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
    /// <remarks>
    /// Measured on 8.1.4.80: `IDENTITY` on `INT`, `BIGINT`, `INTEGER`, `NUMBER` and `DECIMAL` was accepted,
    /// while `TINYINT` and `SMALLINT` answered `非法IDENTITY列类型`, so the server's identity column is an
    /// integer of at least 32 bits.
    /// </remarks>
    private static readonly HashSet<Type> IdentityTypes = [typeof(int), typeof(long)];

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
        ValidateGeneratedConcurrencyTokens(model);
        WarnOnDecimalDefaults(model, logger);
    }

    /// <summary>
    /// Whether the column type a property maps to is one an identity accepts.
    /// </summary>
    /// <remarks>
    /// The column type is what the store creates, so it is what the attribute has to fit, and it differs
    /// from the CLR type when a converter sits between them: an <see cref="int" /> key converted to a string
    /// is an <c>NVARCHAR2</c> column, and an identity on one answered `非法IDENTITY列类型`, measured on
    /// 8.1.4.80 through the store generated cases of the specification suite.
    /// </remarks>
    private static bool IsIdentityColumnType(IProperty property)
    {
        var name = property.GetColumnType().Split('(', 2)[0].Trim();

        return name is "INT" or "INTEGER" or "BIGINT" or "NUMBER" or "DECIMAL";
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

                if (!IsIdentityColumnType(property))
                {
                    throw new InvalidOperationException(
                        $"The property '{property.DeclaringType.DisplayName()}.{property.Name}' is configured as an "
                        + $"identity column, but its column is {property.GetColumnType()}. Dameng generates identity "
                        + "values for an INT or BIGINT column only.");
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
