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
    public const int MaxCharacterLength = 8188;

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

    /// <summary>Length of the binary type used for concurrency tokens.</summary>
    public const int RowVersionLength = 8;

    /// <summary>Formats a precision and scale suffix such as <c>(29,4)</c>.</summary>
    public static string FormatDecimal(int precision, int scale) => $"({precision},{scale})";

    /// <summary>Formats a length suffix such as <c>(255)</c>.</summary>
    public static string FormatLength(int length) => $"({length})";

    /// <summary>Formats a precision suffix such as <c>(6)</c>.</summary>
    public static string FormatPrecision(int precision) => $"({precision})";
}
