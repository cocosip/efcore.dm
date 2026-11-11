namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// Type size and precision defaults for Dameng.
/// </summary>
/// <remarks>
/// These values drive generated DDL when a property carries no explicit size. They are
/// confirmed against the server catalog in roadmap phase P1; any correction is made here
/// and propagates to the type mapping layer and the DDL templates.
/// </remarks>
internal static class DmTypeSizes
{
    /// <summary>Maximum length of a bounded character type, in bytes.</summary>
    /// <remarks>
    /// Measured on 8.1.4.80: a single value near this length is already refused with `记录超长`, because a row
    /// cannot hold what the type allows, so a string past this length is mapped to a character large object
    /// rather than to a column that declares more than it can store.
    /// </remarks>
    public const int MaxCharacterLength = 8188;

    /// <summary>Maximum length of a bounded binary type, in bytes.</summary>
    /// <remarks>
    /// The same ceiling as the character types, and the same reason: a `VARBINARY(9000)` column is created and
    /// then refuses a value of 9000 bytes with `记录超长`, so a byte array past this length is mapped to a
    /// binary large object. Measured on 8.1.4.80 against a specification case that inserts one.
    /// </remarks>
    public const int MaxBinaryLength = 8188;

    /// <summary>Precision applied to a decimal property configured without one.</summary>
    public const int DefaultDecimalPrecision = 29;

    /// <summary>Scale applied to a decimal property configured without one.</summary>
    public const int DefaultDecimalScale = 4;

    /// <summary>
    /// Fractional second precision applied to a time of day. Dameng gives a <c>TIME</c> column that names
    /// no precision a precision of 0, which drops the part a <see cref="TimeOnly" /> carries, so the
    /// provider always names one: this default, or the precision the model asks for. Six is the largest
    /// Dameng accepts and what the reference provider writes.
    /// </summary>
    public const int DefaultTimePrecision = 6;

    /// <summary>
    /// Length a unicode string key or index column gets when the model names none, in characters. A key
    /// has to be a bounded column, because Dameng indexes no character large object, and this is the bound
    /// the framework's own providers use for a key.
    /// </summary>
    public const int DefaultUnicodeKeyLength = 450;

    /// <summary>
    /// Length a non-unicode string key or index column gets when the model names none, in bytes. The same
    /// budget as the unicode bound: 900 single byte characters or 450 characters of two bytes.
    /// </summary>
    public const int DefaultAnsiKeyLength = 900;

    /// <summary>
    /// Length a binary key or index column gets when the model names none, in bytes. Dameng indexes no
    /// binary large object either, and 900 bytes is the bound the framework's own providers use for a
    /// binary key, measured to be accepted as a primary key by 8.1.4.80.
    /// </summary>
    public const int DefaultBinaryKeyLength = 900;


    /// <summary>Formats a precision and scale suffix such as <c>(29,4)</c>.</summary>
    public static string FormatDecimal(int precision, int scale) => $"({precision},{scale})";

    /// <summary>
    /// Formats a length suffix such as <c>(255)</c>, or <c>(255 CHAR)</c> when the unit matters.
    /// </summary>
    /// <remarks>
    /// A length without a unit counts bytes for the ANSI character types, which is not what the model's
    /// length means: measured on 8.1.4.80, <c>VARCHAR2(3)</c> holds three ASCII characters and one Chinese
    /// character, while <c>VARCHAR2(3 CHAR)</c> holds three of either.
    /// </remarks>
    public static string FormatLength(int length, string? unit = null)
        => unit is null ? $"({length})" : $"({length} {unit})";

    /// <summary>Formats a precision suffix such as <c>(6)</c>.</summary>
    public static string FormatPrecision(int precision) => $"({precision})";
}
