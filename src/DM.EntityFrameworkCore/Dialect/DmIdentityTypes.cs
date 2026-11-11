namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// The rule for the column types that can carry <c>IDENTITY</c>.
/// </summary>
/// <remarks>
/// Measured on 8.1.4.80: <c>IDENTITY</c> on <c>INT</c>, <c>BIGINT</c>, <c>INTEGER</c>, <c>NUMBER</c> and
/// <c>DECIMAL</c> was accepted, while <c>TINYINT</c> and <c>SMALLINT</c> answered
/// <c>非法的IDENTITY列类型</c>, so the server's identity column is an integer of at least 32 bits.
/// <para>
/// The rule is written once here and read by the three places that have to agree: the statement generator
/// that writes the attribute, the model validator that refuses a model the store cannot create, and the
/// insert generator that decides whether a value written into the column needs the session flag.
/// </para>
/// </remarks>
internal static class DmIdentityTypes
{
    /// <summary>
    /// Whether a column of this store type can be an identity column.
    /// </summary>
    public static bool CanCarryIdentity(string? storeType)
        => Name(storeType) is DmStoreTypes.Int or DmStoreTypes.BigInt or "INTEGER" or "NUMBER" or DmStoreTypes.Decimal;

    /// <summary>
    /// Whether a column of this store type is an integer the server refuses as an identity, which is a
    /// model that asks for a generated key the store cannot produce.
    /// </summary>
    public static bool IsNarrowInteger(string? storeType)
        => Name(storeType) is DmStoreTypes.TinyInt or DmStoreTypes.SmallInt;

    /// <summary>
    /// The type name without the size or precision, so <c>DECIMAL(18,2)</c> and <c>DECIMAL</c> answer the
    /// same.
    /// </summary>
    private static string Name(string? storeType)
        => storeType is null
            ? string.Empty
            : storeType.Split('(', 2)[0].Trim().ToUpperInvariant();
}
