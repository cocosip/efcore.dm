using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using DM.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

[assembly: DesignTimeProviderServices("DM.EntityFrameworkCore.Design.Internal.DmDesignTimeServices")]

namespace DM.EntityFrameworkCore.Design.Internal;

/// <summary>
/// Registers the services the tooling needs to read this provider's model.
/// </summary>
/// <remarks>
/// The tools discover this class through the assembly attribute, so a command such as
/// <c>dotnet ef migrations add</c> can build a snapshot without the application registering anything.
/// <para>
/// Migration operations, the history repository and the model differ are the relational defaults.
/// The annotation code generator is the relational one as well: every provider annotation this
/// provider writes is recognized by name, so the snapshot carries it without a special case.
/// The provider configuration code generator is this provider's own, because it is the one place the
/// <c>UseDm</c> call in generated code comes from, and so is the database model factory, which reads a
/// schema into the model scaffolding builds from.
/// </para>
/// </remarks>
public class DmDesignTimeServices : IDesignTimeServices
{
    /// <summary>
    /// Adds the design time services a relational provider needs.
    /// </summary>
    public virtual void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddEntityFrameworkDm();

#pragma warning disable EF1001 // The design services builder is an internal API, as it is for every provider.
        new EntityFrameworkRelationalDesignServicesBuilder(serviceCollection)
            .TryAdd<IAnnotationCodeGenerator, AnnotationCodeGenerator>()
            .TryAdd<IDatabaseModelFactory, DmDatabaseModelFactory>()
            .TryAdd<IProviderConfigurationCodeGenerator, DmProviderConfigurationCodeGenerator>()
            .TryAddCoreServices();
#pragma warning restore EF1001
    }
}
