using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Maps the column a JSON entity is stored in.
/// </summary>
/// <remarks>
/// Dameng has a <c>JSON</c> type, measured on 8.1.4.80: a table accepts a <c>JSON</c> column, the driver
/// reads and writes it as text, and the server normalizes the document it stores. The framework looks a
/// mapping up by the CLR type it parses the document into, <see cref="JsonElement" />, when it needs the
/// store type of such a column, and refuses the model when the provider answers nothing.
/// <para>
/// The document is not a value a reader produces as a typed value, so the reader path is the string one,
/// turned into the UTF-8 stream the framework parses the document from.
/// </para>
/// </remarks>
internal sealed class DmJsonTypeMapping : JsonTypeMapping
{
    private static readonly MethodInfo CreateUtf8StreamMethod
        = typeof(DmJsonTypeMapping).GetMethod(nameof(CreateUtf8Stream), BindingFlags.NonPublic | BindingFlags.Static)!;

    public DmJsonTypeMapping(string storeType)
        : base(storeType, typeof(JsonElement), System.Data.DbType.String)
    {
    }

    private DmJsonTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new DmJsonTypeMapping(parameters);

    public override MethodInfo GetDataReaderMethod()
        => typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString), [typeof(int)])!;

    public override Expression CustomizeDataReaderExpression(Expression expression)
        => Expression.Call(CreateUtf8StreamMethod, expression);

    /// <summary>
    /// A document as a literal is a string literal: the server accepted one on a <c>JSON</c> column and
    /// answered it back with its whitespace normalized.
    /// </summary>
    protected override string GenerateNonNullSqlLiteral(object value)
        => "'" + (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
            .Replace("'", "''", StringComparison.Ordinal) + "'";

    private static MemoryStream CreateUtf8Stream(string value) => new(Encoding.UTF8.GetBytes(value));
}
