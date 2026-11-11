namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// Turns a name into the integer id <c>DBMS_LOCK</c> takes.
/// </summary>
/// <remarks>
/// The id has to be the same in every process that contends for the lock, so it cannot come from a hash
/// code the runtime randomises per process. The variant used here is FNV-1a over the upper case form of
/// the name, taken modulo the range the package accepts: measured on 8.1.4.80, an id of 2^30 answered
/// <c>3</c>, which is the package's parameter error, while 2^30 - 1 and anything below it were granted.
/// Two names that differ only in case are therefore the same lock.
/// </remarks>
internal static class DmNamedLock
{
    private const uint OffsetBasis = 2166136261;

    private const uint Prime = 16777619;

    public static int Id(string name)
    {
        var hash = OffsetBasis;

        foreach (var character in name)
        {
            hash = (hash ^ char.ToUpperInvariant(character)) * Prime;
        }

        return (int)(hash % (1 << 30));
    }
}
