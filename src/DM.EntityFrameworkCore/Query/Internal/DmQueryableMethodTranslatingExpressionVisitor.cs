using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the LINQ method chain into relational query roots.
/// </summary>
internal sealed class DmQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
{
    private readonly RelationalQueryCompilationContext _queryCompilationContext;

    public DmQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext)
        : base(dependencies, relationalDependencies, queryCompilationContext)
    {
        _queryCompilationContext = queryCompilationContext;
    }

    private DmQueryableMethodTranslatingExpressionVisitor(
        DmQueryableMethodTranslatingExpressionVisitor parentVisitor)
        : base(parentVisitor)
    {
        _queryCompilationContext = parentVisitor._queryCompilationContext;
    }

    /// <summary>
    /// A subquery is translated by the provider's visitor, not by the base one: the primitive collection
    /// expansion lives in this class, and EF Core's <c>Contains</c> over such a collection is a subquery.
    /// </summary>
    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new DmQueryableMethodTranslatingExpressionVisitor(this);

    /// <summary>
    /// Expands a primitive collection so that the operators over it can be translated. The server has a
    /// <c>JSON_TABLE</c> table function, measured on 8.1.4.80: it turns the JSON array in the column into
    /// one row per element and applies the declared store type to the element, so <c>Contains</c> becomes
    /// a comparison against that row source instead of an untranslatable call.
    /// </summary>
    /// <remarks>
    /// The default implementation answers null, which EF Core reports as
    /// <c>could not be translated</c>. A collection parameter has no property to take the element mapping
    /// from, so it is left to that default.
    /// </remarks>
    protected override ShapedQueryExpression? TranslatePrimitiveCollection(
        SqlExpression sqlExpression,
        IProperty? property,
        string tableAlias)
    {
        var elementType = property?.GetElementType();
        var elementClrType = sqlExpression.Type.IsArray
            ? sqlExpression.Type.GetElementType()!
            : sqlExpression.Type.GetGenericArguments()[0];

        // A mapped collection carries the element mapping in its metadata. A collection parameter does
        // not, so its mapping is taken from the parameter's own mapping or, when that is not inferred,
        // from the CLR element type.
        RelationalTypeMapping? elementMapping = elementType?.GetTypeMapping() as RelationalTypeMapping;
        elementMapping ??= (sqlExpression.TypeMapping as RelationalTypeMapping)?.ElementTypeMapping
            as RelationalTypeMapping;
        elementMapping ??= RelationalDependencies.TypeMappingSource.FindMapping(
            elementClrType,
            RelationalDependencies.Model) as RelationalTypeMapping;

        if (elementMapping is null)
        {
            return null;
        }

        // A collection parameter arrives without a mapping. Give it the collection mapping, which
        // serializes the elements as JSON and carries the element mapping JSON_TABLE reads back.
        if (property is null
            && sqlExpression is SqlParameterExpression { TypeMapping: null } parameterExpression)
        {
            if (RelationalDependencies.TypeMappingSource.FindMapping(
                    sqlExpression.Type,
                    RelationalDependencies.Model,
                    elementMapping) is not RelationalTypeMapping collectionMapping)
            {
                return null;
            }

            sqlExpression = parameterExpression.ApplyTypeMapping(collectionMapping);
        }

        var nullableElementType = elementClrType.IsValueType
            && Nullable.GetUnderlyingType(elementClrType) is null
                ? typeof(Nullable<>).MakeGenericType(elementClrType)
                : elementClrType;

        // The path and the column definition are constants; the only value from outside is the store
        // type, which comes from the element's own mapping and not from user input. The ordinality column
        // is not projected: it gives a paged row source the ordering a page needs, and the generator
        // writes that ordering (measured on 8.1.4.80: `FOR ORDINALITY` parses and answers 1..n in array
        // order).
        var columns = new SqlFragmentExpression(
            $"'$[*]' COLUMNS (\"value\" {elementMapping.StoreType} PATH '$', \"rn\" FOR ORDINALITY)");
        var table = new TableValuedFunctionExpression(tableAlias, "JSON_TABLE", [sqlExpression, columns]);
        var value = new ColumnExpression(
            "value",
            tableAlias,
            Nullable.GetUnderlyingType(elementClrType) ?? elementClrType,
            elementMapping,
            elementType?.IsNullable
                ?? (Nullable.GetUnderlyingType(elementClrType) is not null || !elementClrType.IsValueType));

#pragma warning disable EF1001 // A query root is built from a SelectExpression, as it is for every provider.
        var selectExpression = new SelectExpression(
            [table],
            value,
            [(value, elementMapping.Comparer)],
            _queryCompilationContext.SqlAliasManager);
#pragma warning restore EF1001

        Expression shaperExpression = new ProjectionBindingExpression(
            selectExpression,
            new ProjectionMember(),
            nullableElementType);

        if (shaperExpression.Type != elementClrType)
        {
            shaperExpression = Expression.Convert(shaperExpression, elementClrType);
        }

        return new ShapedQueryExpression(selectExpression, shaperExpression);
    }
}
