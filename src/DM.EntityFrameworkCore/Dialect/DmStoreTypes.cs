namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// Dameng store type names, as written in DDL and read back from the catalog.
/// </summary>
/// <remarks>
/// This layer is deliberately free of any EF Core type reference. A CI check fails the
/// build if <c>Microsoft.EntityFrameworkCore</c> appears in a file under <c>Dialect/</c>.
/// The names are the single source of truth for both the type mapping layer and the
/// reverse engineering queries, so they are written once and shared across branches.
/// </remarks>
internal static class DmStoreTypes
{
    public const string Bit = "BIT";
    public const string TinyInt = "TINYINT";
    public const string SmallInt = "SMALLINT";
    public const string Int = "INT";
    public const string BigInt = "BIGINT";
    public const string Decimal = "DECIMAL";
    public const string Real = "REAL";
    public const string Float = "FLOAT";
    public const string Double = "DOUBLE";

    public const string Char = "CHAR";
    public const string NChar = "NCHAR";
    public const string VarChar = "VARCHAR";
    public const string VarChar2 = "VARCHAR2";
    public const string NVarChar = "NVARCHAR";
    public const string NVarChar2 = "NVARCHAR2";
    public const string Text = "TEXT";
    public const string Clob = "CLOB";
    public const string NClob = "NCLOB";
    public const string Json = "JSON";

    public const string Binary = "BINARY";
    public const string VarBinary = "VARBINARY";
    public const string Blob = "BLOB";

    public const string Date = "DATE";
    public const string Time = "TIME";
    public const string Timestamp = "TIMESTAMP";
    public const string TimestampWithTimeZone = "TIMESTAMP WITH TIME ZONE";
    public const string DateTimeWithTimeZone = "DATETIME WITH TIME ZONE";
    public const string IntervalDayToSecond = "INTERVAL DAY TO SECOND";
}
