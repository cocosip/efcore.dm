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

    /// <summary>Fractional seconds precision applied to temporal types.</summary>
    public const int DefaultTemporalPrecision = 6;

    /// <summary>Length of the binary type used for concurrency tokens.</summary>
    public const int RowVersionLength = 8;

    /// <summary>Formats a precision and scale suffix such as <c>(29,4)</c>.</summary>
    public static string FormatDecimal(int precision, int scale) => $"({precision},{scale})";

    /// <summary>Formats a length suffix such as <c>(255)</c>.</summary>
    public static string FormatLength(int length) => $"({length})";

    /// <summary>Formats a precision suffix such as <c>(6)</c>.</summary>
    public static string FormatPrecision(int precision) => $"({precision})";
}
