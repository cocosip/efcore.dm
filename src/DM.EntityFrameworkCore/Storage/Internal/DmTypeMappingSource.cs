using System.Data;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Maps CLR types to Dameng store types and back.
/// </summary>
/// <remarks>
/// Facets are applied by building the store type string here rather than by cloning a base
/// mapping, so that every mapping the provider hands out is self-describing. A facet that is
/// not configured produces the unsuffixed type, which is what a plain property should get.
/// <para>
/// The reverse direction exists for reverse engineering: a column type read from the catalog has
/// to resolve to a CLR type, and the facets written in that name have to survive the round trip.
/// </para>
/// </remarks>
internal sealed class DmTypeMappingSource : RelationalTypeMappingSource
{
    /// <summary>
    /// The width of the text a <see cref="Guid" /> is stored as, in both directions.
    /// </summary>
    private const int GuidSize = 36;

    private static readonly RelationalTypeMapping BoolMapping = new BoolTypeMapping(DmStoreTypes.Bit, DbType.Boolean);
    private static readonly RelationalTypeMapping ByteMapping = new ByteTypeMapping(DmStoreTypes.SmallInt, DbType.Byte);
    private static readonly RelationalTypeMapping SByteMapping = new SByteTypeMapping(DmStoreTypes.TinyInt, DbType.SByte);
    private static readonly RelationalTypeMapping ShortMapping = new ShortTypeMapping(DmStoreTypes.SmallInt, DbType.Int16);
    private static readonly RelationalTypeMapping IntMapping = new IntTypeMapping(DmStoreTypes.Int, DbType.Int32);
    private static readonly RelationalTypeMapping LongMapping = new LongTypeMapping(DmStoreTypes.BigInt, DbType.Int64);

    private static readonly RelationalTypeMapping FloatMapping = new FloatTypeMapping(DmStoreTypes.Real, DbType.Single);
    private static readonly RelationalTypeMapping DoubleMapping = new DoubleTypeMapping(DmStoreTypes.Double, DbType.Double);
    private static readonly RelationalTypeMapping GuidMapping = new GuidTypeMapping(
        DmStoreTypes.Char + DmTypeSizes.FormatLength(GuidSize),
        DbType.Guid);

    private static readonly RelationalTypeMapping DateOnlyMapping = new DateOnlyTypeMapping(DmStoreTypes.Date, DbType.Date);
    private static readonly RelationalTypeMapping TimeSpanMapping = new DmTimeSpanTypeMapping(
        DmStoreTypes.IntervalDayToSecond);
    private static readonly RelationalTypeMapping DateTimeOffsetMapping = new DateTimeOffsetTypeMapping(
        DmStoreTypes.DateTimeWithTimeZone,
        DbType.DateTimeOffset);

    /// <summary>
    /// Resolves a catalog column type to the CLR type the model should expose. A store type that
    /// is not listed here is not mapped, so reverse engineering surfaces it rather than silently
    /// choosing a type that loses data.
    /// </summary>
    private static readonly Dictionary<string, Type> ClrTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [DmStoreTypes.Bit] = typeof(bool),
        [DmStoreTypes.TinyInt] = typeof(sbyte),
        [DmStoreTypes.SmallInt] = typeof(short),
        [DmStoreTypes.Int] = typeof(int),
        ["INTEGER"] = typeof(int),
        [DmStoreTypes.BigInt] = typeof(long),
        [DmStoreTypes.Decimal] = typeof(decimal),
        ["NUMERIC"] = typeof(decimal),
        ["NUMBER"] = typeof(decimal),
        [DmStoreTypes.Real] = typeof(float),
        // Dameng's FLOAT is FLOAT(53), a double-precision type, and REAL is the single-precision one.
        // Reading FLOAT back as float would truncate a column the previous provider wrote for a
        // double property, and would shorten one written by hand.
        [DmStoreTypes.Float] = typeof(double),
        [DmStoreTypes.Double] = typeof(double),
        ["DOUBLE PRECISION"] = typeof(double),
        [DmStoreTypes.Char] = typeof(string),
        [DmStoreTypes.NChar] = typeof(string),
        [DmStoreTypes.VarChar] = typeof(string),
        [DmStoreTypes.VarChar2] = typeof(string),
        [DmStoreTypes.NVarChar] = typeof(string),
        [DmStoreTypes.NVarChar2] = typeof(string),
        [DmStoreTypes.Text] = typeof(string),
        [DmStoreTypes.Clob] = typeof(string),
        [DmStoreTypes.NClob] = typeof(string),
        // A document is a JSON type on the server and a string in the model, which is what a reverse
        // engineered column reads as.
        [DmStoreTypes.Json] = typeof(string),
        [DmStoreTypes.Binary] = typeof(byte[]),
        [DmStoreTypes.VarBinary] = typeof(byte[]),
        [DmStoreTypes.Blob] = typeof(byte[]),
        [DmStoreTypes.Date] = typeof(DateTime),
        [DmStoreTypes.Time] = typeof(TimeOnly),
        [DmStoreTypes.Timestamp] = typeof(DateTime),
        ["DATETIME"] = typeof(DateTime),
        [DmStoreTypes.TimestampWithTimeZone] = typeof(DateTimeOffset),
        [DmStoreTypes.DateTimeWithTimeZone] = typeof(DateTimeOffset),
        [DmStoreTypes.IntervalDayToSecond] = typeof(TimeSpan)
    };

    public DmTypeMappingSource(
        TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        if (mappingInfo.StoreTypeName is null)
        {
            return Build(
                mappingInfo.ClrType,
                storeType: null,
                mappingInfo.Size,
                mappingInfo.Precision,
                mappingInfo.Scale,
                // A .NET string is UTF-16, so the type a string property gets by default is the one that
                // stores any character: Dameng's ansi `VARCHAR` is written in the database character set,
                // and a gb18030 database has no encoding for characters outside it. `IsUnicode(false)`
                // chooses the ansi type, which is what the reference provider does and what its own
                // migrations show for a column it declared `unicode: false`.
                mappingInfo.IsUnicode ?? true,
                mappingInfo.IsFixedLength ?? false,
                mappingInfo.IsKeyOrIndex);
        }

        var unicode = mappingInfo.IsUnicode;
        var size = mappingInfo.Size;
        var precision = mappingInfo.Precision;
        var scale = mappingInfo.Scale;

        var baseName = ParseStoreTypeName(mappingInfo.StoreTypeName, ref unicode, ref size, ref precision, ref scale);

        var clrType = mappingInfo.ClrType;

        if (clrType is null)
        {
            // A Guid is a fixed width text of 36 characters, which is the shape the reference
            // provider writes, so a column of exactly that shape reads back as a Guid rather than as
            // a string. A wider or narrower character column stays a string.
            clrType = baseName == DmStoreTypes.Char && size == GuidSize
                ? typeof(Guid)
                : baseName is not null && ClrTypes.TryGetValue(baseName, out var resolved)
                    ? resolved
                    : null;
        }

        return Build(
            clrType,
            mappingInfo.StoreTypeName,
            size,
            precision,
            scale,
            unicode ?? true,
            mappingInfo.IsFixedLength ?? false,
            mappingInfo.IsKeyOrIndex);
    }

    private static RelationalTypeMapping? Build(
        Type? clrType,
        string? storeType,
        int? size,
        int? precision,
        int? scale,
        bool unicode,
        bool fixedLength,
        bool keyOrIndex)
    {
        if (clrType is null)
        {
            return null;
        }

        if (clrType == typeof(string))
        {
            return String(storeType, size, unicode, fixedLength, keyOrIndex);
        }

        if (clrType == typeof(decimal))
        {
            return Decimal(storeType, precision, scale);
        }

        if (clrType == typeof(byte[]))
        {
            return Binary(storeType, size, keyOrIndex);
        }

        // The framework asks for the column a JSON entity is stored in by this placeholder type, and
        // refuses the model when the provider answers nothing.
        if (clrType == typeof(JsonTypePlaceholder))
        {
            return new DmJsonTypeMapping(storeType ?? DmStoreTypes.Json);
        }

        if (clrType == typeof(DateTime))
        {
            return Temporal(storeType, size ?? precision);
        }

        // A store type carried by the model is honoured verbatim for the remaining mappings, so
        // that a column declared with a name this provider does not generate still round trips.
        return clrType switch
        {
            var t when t == typeof(bool) => Retype(storeType, () => BoolMapping, s => new BoolTypeMapping(s, DbType.Boolean)),
            var t when t == typeof(byte) => Retype(storeType, () => ByteMapping, s => new ByteTypeMapping(s, DbType.Byte)),
            var t when t == typeof(sbyte) => Retype(storeType, () => SByteMapping, s => new SByteTypeMapping(s, DbType.SByte)),
            var t when t == typeof(short) => Retype(storeType, () => ShortMapping, s => new ShortTypeMapping(s, DbType.Int16)),
            var t when t == typeof(int) => Retype(storeType, () => IntMapping, s => new IntTypeMapping(s, DbType.Int32)),
            var t when t == typeof(long) => Retype(storeType, () => LongMapping, s => new LongTypeMapping(s, DbType.Int64)),
            var t when t == typeof(float) => Retype(storeType, () => FloatMapping, s => new FloatTypeMapping(s, DbType.Single)),
            var t when t == typeof(double) => Retype(storeType, () => DoubleMapping, s => new DoubleTypeMapping(s, DbType.Double)),
            var t when t == typeof(Guid) => Retype(storeType, () => GuidMapping, s => new GuidTypeMapping(s, DbType.Guid)),
            var t when t == typeof(DateOnly) => Retype(storeType, () => DateOnlyMapping, s => new DateOnlyTypeMapping(s, DbType.Date)),
            var t when t == typeof(TimeOnly) => TimeOnly(storeType, precision),
            var t when t == typeof(TimeSpan) => Retype(storeType, () => TimeSpanMapping, s => new DmTimeSpanTypeMapping(s)),
            var t when t == typeof(DateTimeOffset) => Retype(storeType, () => DateTimeOffsetMapping, s => new DateTimeOffsetTypeMapping(s, DbType.DateTimeOffset)),
            _ => null
        };
    }

    private static RelationalTypeMapping Retype(
        string? storeType,
        Func<RelationalTypeMapping> fallback,
        Func<string, RelationalTypeMapping> create)
        => storeType is null ? fallback() : create(storeType);

    private static RelationalTypeMapping String(
        string? storeType,
        int? size,
        bool unicode,
        bool fixedLength,
        bool keyOrIndex)
    {
        if (storeType is null && size is null)
        {
            // A key or an index has to name a bounded type, because Dameng indexes no large object, so
            // the length the model leaves out is the one the framework's own providers use for a key. A
            // fixed length column has no unbounded form either, and a fixed length defaults to one
            // character.
            size = (fixedLength, keyOrIndex) switch
            {
                (true, _) => 1,
                (_, true) => unicode ? DmTypeSizes.DefaultUnicodeKeyLength : DmTypeSizes.DefaultAnsiKeyLength,
                _ => null
            };
        }

        // A string the model gives no length, or one longer than a bounded character column holds, is a
        // character large object. Naming a bounded type without a length is not a type the server accepts
        // in a table definition, and a length past the page size is rejected when the table is created.
        if (storeType is null && (size is null || size > DmTypeSizes.MaxCharacterLength))
        {
            return new StringTypeMapping(
                unicode ? DmStoreTypes.NClob : DmStoreTypes.Clob,
                unicode ? DbType.String : DbType.AnsiString,
                unicode,
                size: null);
        }

        // The variable length names are the ones the reference provider writes and the ones its own
        // migrations show. Dameng documents VARCHAR2 as usable the same way as VARCHAR, and its catalog
        // reports the type of a column it created that way, so the names a model produces and the names a
        // scaffolded model reads are the same and a migration diff between them is empty.
        var storeTypeBase = (unicode, fixedLength) switch
        {
            (true, true) => DmStoreTypes.NChar,
            (true, false) => DmStoreTypes.NVarChar2,
            (false, true) => DmStoreTypes.Char,
            _ => DmStoreTypes.VarChar2
        };

        if (storeType is null)
        {
            // A length without a unit counts bytes for the ANSI types, while the length a model declares
            // counts characters, so the ANSI types name the unit and the unicode ones, which count
            // characters already, do not.
            storeType = size is null
                ? storeTypeBase
                : storeTypeBase + DmTypeSizes.FormatLength(size.Value, unicode ? null : "CHAR");
        }

        var dbType = (unicode, fixedLength) switch
        {
            (true, true) => DbType.StringFixedLength,
            (true, false) => DbType.String,
            (false, true) => DbType.AnsiStringFixedLength,
            _ => DbType.AnsiString
        };

        // EF Core 10 removed the fixed-length constructor parameter: fixed length is expressed
        // by the store type name itself.
        return new StringTypeMapping(storeType, dbType, unicode, size);
    }

    private static RelationalTypeMapping Decimal(string? storeType, int? precision, int? scale)
    {
        if (storeType is null)
        {
            var resolvedPrecision = precision ?? DmTypeSizes.DefaultDecimalPrecision;
            var resolvedScale = scale ?? (precision is null ? DmTypeSizes.DefaultDecimalScale : 0);

            return new DecimalTypeMapping(
                DmStoreTypes.Decimal + DmTypeSizes.FormatDecimal(resolvedPrecision, resolvedScale),
                DbType.Decimal,
                resolvedPrecision,
                resolvedScale);
        }

        return new DecimalTypeMapping(storeType, DbType.Decimal, precision, scale);
    }

    /// <summary>
    /// A binary column the model leaves unbounded is a large object, which Dameng does not index, so a key
    /// or an index names the bound the framework's own providers use instead: without it the server refuses
    /// to create the table at all.
    /// </summary>
    private static RelationalTypeMapping Binary(string? storeType, int? size, bool keyOrIndex)
    {
        if (storeType is not null)
        {
            return new DmByteArrayTypeMapping(storeType, DbType.Binary, size);
        }

        if (size is null && keyOrIndex)
        {
            size = DmTypeSizes.DefaultBinaryKeyLength;
        }

        return size is null
            ? new DmByteArrayTypeMapping(DmStoreTypes.Blob, DbType.Binary)
            : new DmByteArrayTypeMapping(
                DmStoreTypes.VarBinary + DmTypeSizes.FormatLength(size.Value),
                DbType.Binary,
                size);
    }

    private static RelationalTypeMapping TimeOnly(string? storeType, int? precision)
        => storeType is not null
            ? new TimeOnlyTypeMapping(storeType)
            : new TimeOnlyTypeMapping(
                DmStoreTypes.Time
                    + DmTypeSizes.FormatPrecision(precision ?? DmTypeSizes.DefaultTimePrecision));

    private static RelationalTypeMapping Temporal(string? storeType, int? precision)
    {
        if (storeType is null)
        {
            storeType = precision is null
                ? DmStoreTypes.Timestamp
                : DmStoreTypes.Timestamp + DmTypeSizes.FormatPrecision(precision.Value);
        }

        return new DateTimeTypeMapping(storeType, DbType.DateTime);
    }
}
