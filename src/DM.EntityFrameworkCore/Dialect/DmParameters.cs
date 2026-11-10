using System.Text;

namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// Parameter naming rules for Dameng.
/// </summary>
internal static class DmParameters
{
    /// <summary>Dameng binds parameters by name with a colon prefix.</summary>
    public const char Prefix = ':';

    /// <summary>Prefixes a bare name with the parameter marker unless already present.</summary>
    public static string ToName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return name[0] == Prefix ? name : string.Concat(Prefix.ToString(), name);
    }

    /// <summary>Appends the parameter marker and the bare name.</summary>
    public static void Append(StringBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.Append(Prefix).Append(name);
    }

    /// <summary>Strips the parameter marker, if present. Used when mapping to driver parameter names.</summary>
    public static string ToBareName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return name[0] == Prefix ? name[1..] : name;
    }
}
