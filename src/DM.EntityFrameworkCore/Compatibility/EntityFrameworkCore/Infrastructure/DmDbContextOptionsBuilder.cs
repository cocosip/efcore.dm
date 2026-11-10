using System.Data.Common;
using DM.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Dameng-specific options builder. Declared in the namespace the Dameng provider declares it in,
/// so that consumer source that names the type compiles unchanged.
/// </summary>
public class DmDbContextOptionsBuilder
{
    public DmDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        OptionsBuilder = optionsBuilder;
    }

    protected virtual DbContextOptionsBuilder OptionsBuilder { get; }

    public virtual DmDbContextOptionsBuilder WithConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        var extension = (DmOptionsExtension)GetOrCreateExtension().WithConnectionString(connectionString);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    public virtual DmDbContextOptionsBuilder WithConnection(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var extension = (DmOptionsExtension)GetOrCreateExtension().WithConnection(connection);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    private RelationalOptionsExtension GetOrCreateExtension()
        => OptionsBuilder.Options.FindExtension<DmOptionsExtension>() ?? new DmOptionsExtension();
}
