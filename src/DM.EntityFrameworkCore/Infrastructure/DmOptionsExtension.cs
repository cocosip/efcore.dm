using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Provider options carried on the <see cref="DbContextOptionsBuilder" />. Deriving from
/// <see cref="RelationalOptionsExtension" /> is required: EF Core's relational services look
/// for that base type and report "no relational database providers are configured" without it.
/// </summary>
internal sealed class DmOptionsExtension : RelationalOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public DmOptionsExtension()
    {
    }

    private DmOptionsExtension(DmOptionsExtension copyFrom)
        : base(copyFrom)
    {
    }

    public override DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    protected override RelationalOptionsExtension Clone() => new DmOptionsExtension(this);

    public override void ApplyServices(IServiceCollection services) => services.AddEntityFrameworkDm();

    public override void Validate(IDbContextOptions options)
    {
        if (ConnectionString is null && Connection is null)
        {
            throw new InvalidOperationException(
                "A Dameng connection was not configured. Call UseDm with a connection string or a DbConnection.");
        }
    }

    private sealed class ExtensionInfo : RelationalExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
        }

        private new DmOptionsExtension Extension => (DmOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => true;

        public override string LogFragment => "using Dameng ";

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["Dm:ConnectionString"] = Extension.Connection is not null ? "provided" : "configured";
    }
}
