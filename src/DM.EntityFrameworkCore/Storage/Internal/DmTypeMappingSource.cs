using System.Data;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Maps CLR types to Dameng store types. The store type names come from the dialect layer,
/// so this class only carries the EF Core mapping plumbing.
/// </summary>
internal sealed class DmTypeMappingSource : RelationalTypeMappingSource
{
    private readonly BoolTypeMapping _bool = new(DmStoreTypes.Bit, DbType.Boolean);
    private readonly ByteTypeMapping _byte = new(DmStoreTypes.SmallInt, DbType.Byte);
    private readonly SByteTypeMapping _sByte = new(DmStoreTypes.TinyInt, DbType.SByte);
    private readonly ShortTypeMapping _short = new(DmStoreTypes.SmallInt, DbType.Int16);
    private readonly IntTypeMapping _int = new(DmStoreTypes.Int, DbType.Int32);
    private readonly LongTypeMapping _long = new(DmStoreTypes.BigInt, DbType.Int64);
    private readonly FloatTypeMapping _float = new(DmStoreTypes.Real, DbType.Single);
    private readonly DoubleTypeMapping _double = new(DmStoreTypes.Double, DbType.Double);
    private readonly DecimalTypeMapping _decimal = new(
        DmStoreTypes.Decimal + DmTypeSizes.FormatDecimal(DmTypeSizes.DefaultDecimalPrecision, DmTypeSizes.DefaultDecimalScale),
        DbType.Decimal);

    private readonly StringTypeMapping _ansiString = new(DmStoreTypes.VarChar, DbType.AnsiString);
    private readonly StringTypeMapping _unicodeString = new(DmStoreTypes.NVarChar, DbType.String);
    private readonly ByteArrayTypeMapping _binary = new(DmStoreTypes.Blob, DbType.Binary);
    private readonly DateTimeTypeMapping _dateTime = new(
        DmStoreTypes.Timestamp + DmTypeSizes.FormatPrecision(DmTypeSizes.DefaultTemporalPrecision),
        DbType.DateTime);
    private readonly DateTimeOffsetTypeMapping _dateTimeOffset = new(DmStoreTypes.DateTimeWithTimeZone, DbType.DateTimeOffset);
    private readonly TimeSpanTypeMapping _timeSpan = new(DmStoreTypes.IntervalDayToSecond, DbType.Time);
    private readonly GuidTypeMapping _guid = new(DmStoreTypes.VarChar + DmTypeSizes.FormatLength(36), DbType.Guid);

    public DmTypeMappingSource(
        TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        RelationalTypeMapping? mapping = mappingInfo.ClrType switch
        {
            null => null,
            var t when t == typeof(bool) => _bool,
            var t when t == typeof(byte) => _byte,
            var t when t == typeof(sbyte) => _sByte,
            var t when t == typeof(short) => _short,
            var t when t == typeof(int) => _int,
            var t when t == typeof(long) => _long,
            var t when t == typeof(float) => _float,
            var t when t == typeof(double) => _double,
            var t when t == typeof(decimal) => _decimal,
            var t when t == typeof(string) => mappingInfo.IsUnicode == true ? _unicodeString : _ansiString,
            var t when t == typeof(byte[]) => _binary,
            var t when t == typeof(DateTime) => _dateTime,
            var t when t == typeof(DateTimeOffset) => _dateTimeOffset,
            var t when t == typeof(TimeSpan) => _timeSpan,
            var t when t == typeof(Guid) => _guid,
            _ => null
        };

        return mapping ?? base.FindMapping(mappingInfo);
    }
}
