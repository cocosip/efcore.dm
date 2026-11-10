using System.Text;

namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// Sequence statements. Dameng sequences are Oracle-style: the next value is read from a pseudo
/// column rather than through a function call, and the row has to come from <c>DUAL</c>.
/// </summary>
internal static class DmSequence
{
    /// <summary>The table a statement without a table selects from.</summary>
    public const string DualTable = "DUAL";

    /// <summary>
    /// The statement that advances a sequence and returns the value it produced.
    /// </summary>
    public static string NextValueSql(string name, string? schema)
    {
        var builder = new StringBuilder("SELECT ");

        if (!string.IsNullOrEmpty(schema))
        {
            DmIdentifier.Delimit(builder, schema!);
            builder.Append('.');
        }

        DmIdentifier.Delimit(builder, name);
        builder.Append(".NEXTVAL FROM ").Append(DualTable);

        return builder.ToString();
    }
}
