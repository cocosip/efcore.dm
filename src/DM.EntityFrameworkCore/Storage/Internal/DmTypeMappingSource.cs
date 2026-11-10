using System.Data;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Maps CLR types to Dameng store types, applying size, precision, scale, unicode and fixed
/// length facets. Store type names come from the dialect layer.
/// </summary>
/// <remarks>
/// Facets are applied by building the store type string here rather than by cloning a base
/// mapping, so that every mapping the provider hands out is self-describing. A facet that is
/// not configured produces the unsuffixed type, which is what a plain property should get.
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
    private static readonly RelationalTypeMapping TimeSpanMapping = new TimeSpanTypeMapping(
        DmStoreTypes.IntervalDayToSecond,
        DbType.Time);
    private static readonly RelationalTypeMapping DateTimeOffsetMapping = new DateTimeOffsetTypeMapping(
        DmStoreTypes.DateTimeWithTimeZone,
        DbType.DateTimeOffset);

    public DmTypeMappingSource(
        TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
        => FindByClrType(mappingInfo) ?? base.FindMapping(mappingInfo);

    private static RelationalTypeMapping? FindByClrType(in RelationalTypeMappingInfo mappingInfo)
        => mappingInfo.ClrType switch
        {
            null => null,
            var t when t == typeof(bool) => BoolMapping,
            var t when t == typeof(byte) => ByteMapping,
            var t when t == typeof(sbyte) => SByteMapping,
            var t when t == typeof(short) => ShortMapping,
            var t when t == typeof(int) => IntMapping,
            var t when t == typeof(long) => LongMapping,
            var t when t == typeof(float) => FloatMapping,
            var t when t == typeof(double) => DoubleMapping,
            var t when t == typeof(decimal) => Decimal(mappingInfo),
            var t when t == typeof(string) => String(mappingInfo),
            var t when t == typeof(byte[]) => Binary(mappingInfo),
            var t when t == typeof(Guid) => GuidMapping,
            var t when t == typeof(DateTime) => Temporal(mappingInfo, DmStoreTypes.Timestamp, DbType.DateTime),
            var t when t == typeof(DateTimeOffset) => DateTimeOffsetMapping,
            var t when t == typeof(TimeSpan) => TimeSpanMapping,
            var t when t == typeof(DateOnly) => DateOnlyMapping,
            var t when t == typeof(TimeOnly) => TimeOnlyMapping,
            _ => null
        };

    private static RelationalTypeMapping String(in RelationalTypeMappingInfo mappingInfo)
    {
        var unicode = mappingInfo.IsUnicode ?? false;
        var fixedLength = mappingInfo.IsFixedLength ?? false;
        var size = mappingInfo.Size;

        var storeTypeBase = (unicode, fixedLength) switch
        {
            (true, true) => DmStoreTypes.NChar,
            (true, false) => DmStoreTypes.NVarChar,
            (false, true) => DmStoreTypes.Char,
            _ => DmStoreTypes.VarChar
        };

        var storeType = size is null
            ? storeTypeBase
            : storeTypeBase + DmTypeSizes.FormatLength(size.Value);

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

    private static RelationalTypeMapping Decimal(in RelationalTypeMappingInfo mappingInfo)
    {
        var precision = mappingInfo.Precision;
        var scale = mappingInfo.Scale;

        if (precision is null && scale is null)
        {
            return new DecimalTypeMapping(
                DmStoreTypes.Decimal
                + DmTypeSizes.FormatDecimal(DmTypeSizes.DefaultDecimalPrecision, DmTypeSizes.DefaultDecimalScale),
                DbType.Decimal,
                DmTypeSizes.DefaultDecimalPrecision,
                DmTypeSizes.DefaultDecimalScale);
        }

        var resolvedPrecision = precision ?? DmTypeSizes.DefaultDecimalPrecision;
        var resolvedScale = scale ?? 0;

        return new DecimalTypeMapping(
            DmStoreTypes.Decimal + DmTypeSizes.FormatDecimal(resolvedPrecision, resolvedScale),
            DbType.Decimal,
            resolvedPrecision,
            resolvedScale);
    }

    private static RelationalTypeMapping Binary(in RelationalTypeMappingInfo mappingInfo)
    {
        var size = mappingInfo.Size;

        return size is null
            ? new ByteArrayTypeMapping(DmStoreTypes.Blob, DbType.Binary)
            : new ByteArrayTypeMapping(
                DmStoreTypes.VarBinary + DmTypeSizes.FormatLength(size.Value),
                DbType.Binary,
                size);
    }

    private static RelationalTypeMapping Temporal(
        in RelationalTypeMappingInfo mappingInfo,
        string storeTypeBase,
        DbType dbType)
    {
        var precision = mappingInfo.Precision;

        var storeType = precision is null
            ? storeTypeBase
            : storeTypeBase + DmTypeSizes.FormatPrecision(precision.Value);

        return new DateTimeTypeMapping(storeType, dbType);
    }
}
