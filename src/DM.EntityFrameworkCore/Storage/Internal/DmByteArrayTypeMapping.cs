using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Maps a <see cref="byte" /> array to a Dameng binary column.
/// </summary>
/// <remarks>
/// The base mapping writes the value as <c>X'0414'</c>, which Dameng accepts for a bounded binary column
/// but rejects for a large object with <c>数据类型不匹配</c>, measured on 8.1.4.80. <c>HEXTORAW</c> is accepted
/// by every binary type, so a seed value or a literal reaches the column whatever its type is.
/// </remarks>
internal sealed class DmByteArrayTypeMapping : ByteArrayTypeMapping
{
    public DmByteArrayTypeMapping(string storeType, System.Data.DbType dbType, int? size = null)
        : base(storeType, dbType, size)
    {
    }

    protected override string GenerateNonNullSqlLiteral(object value)
        => "HEXTORAW('" + Convert.ToHexString((byte[])value) + "')";
}
