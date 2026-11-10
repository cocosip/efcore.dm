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
    private static readonly RelationalTypeMapping BoolMapping = new BoolTypeMapping(DmStoreTypes.Bit, DbType.Boolean);
    private static readonly RelationalTypeMapping ByteMapping = new ByteTypeMapping(DmStoreTypes.SmallInt, DbType.Byte);
    private static readonly RelationalTypeMapping SByteMapping = new SByteTypeMapping(DmStoreTypes.TinyInt, DbType.SByte);
    private static readonly RelationalTypeMapping ShortMapping = new ShortTypeMapping(DmStoreTypes.SmallInt, DbType.Int16);
    private static readonly RelationalTypeMapping IntMapping = new IntTypeMapping(DmStoreTypes.Int, DbType.Int32);
    private static readonly RelationalTypeMapping LongMapping = new LongTypeMapping(DmStoreTypes.BigInt, DbType.Int64);

    private static readonly RelationalTypeMapping FloatMapping = new FloatTypeMapping(DmStoreTypes.Real, DbType.Single);
    private static readonly RelationalTypeMapping DoubleMapping = new DoubleTypeMapping(DmStoreTypes.Double, DbType.Double);
    private static readonly RelationalTypeMapping GuidMapping = new GuidTypeMapping(
        DmStoreTypes.VarChar + DmTypeSizes.FormatLength(36),
        DbType.Guid);

    private static readonly RelationalTypeMapping DateOnlyMapping = new DateOnlyTypeMapping(DmStoreTypes.Date, DbType.Date);
    private static readonly RelationalTypeMapping TimeOnlyMapping = new TimeOnlyTypeMapping(DmStoreTypes.Time, DbType.Time);
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
        [DmStoreTypes.Float] = typeof(float),
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
        [DmStoreTypes.Binary] = typeof(byte[]),
        [DmStoreTypes.VarBinary] = typeof(byte[]),
        [DmStoreTypes.Blob] = typeof(byte[]),
        [DmStoreTypes.Date] = typeof(DateTime),
        [DmStoreTypes.Time] = typeof(TimeOnly),
        [DmStoreTypes.Timestamp] = typeof(DateTime),
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
                mappingInfo.IsUnicode ?? false,
                mappingInfo.IsFixedLength ?? false);
        }

        var unicode = mappingInfo.IsUnicode;
        var size = mappingInfo.Size;
        var precision = mappingInfo.Precision;
        var scale = mappingInfo.Scale;

        var baseName = ParseStoreTypeName(mappingInfo.StoreTypeName, ref unicode, ref size, ref precision, ref scale);

        var clrType = mappingInfo.ClrType
            ?? (baseName is not null && ClrTypes.TryGetValue(baseName, out var resolved) ? resolved : null);

        return Build(
            clrType,
            mappingInfo.StoreTypeName,
            size,
            precision,
            scale,
            unicode ?? false,
            mappingInfo.IsFixedLength ?? false);
    }

    private static RelationalTypeMapping? Build(
        Type? clrType,
        string? storeType,
        int? size,
        int? precision,
        int? scale,
        bool unicode,
        bool fixedLength)
    {
        if (clrType is null)
        {
            return null;
        }

        if (clrType == typeof(string))
        {
            return String(storeType, size, unicode, fixedLength);
        }

        if (clrType == typeof(decimal))
        {
            return Decimal(storeType, precision, scale);
        }

        if (clrType == typeof(byte[]))
        {
            return Binary(storeType, size);
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
            var t when t == typeof(TimeOnly) => Retype(storeType, () => TimeOnlyMapping, s => new TimeOnlyTypeMapping(s, DbType.Time)),
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

    private static RelationalTypeMapping String(string? storeType, int? size, bool unicode, bool fixedLength)
    {
        var storeTypeBase = (unicode, fixedLength) switch
        {
            (true, true) => DmStoreTypes.NChar,
            (true, false) => DmStoreTypes.NVarChar,
            (false, true) => DmStoreTypes.Char,
            _ => DmStoreTypes.VarChar
        };

        if (storeType is null)
        {
            storeType = size is null
                ? storeTypeBase
                : storeTypeBase + DmTypeSizes.FormatLength(size.Value);
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

    private static RelationalTypeMapping Binary(string? storeType, int? size)
        => storeType is null
            ? size is null
                ? new ByteArrayTypeMapping(DmStoreTypes.Blob, DbType.Binary)
                : new ByteArrayTypeMapping(
                    DmStoreTypes.VarBinary + DmTypeSizes.FormatLength(size.Value),
                    DbType.Binary,
                    size)
            : new ByteArrayTypeMapping(storeType, DbType.Binary, size);

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
