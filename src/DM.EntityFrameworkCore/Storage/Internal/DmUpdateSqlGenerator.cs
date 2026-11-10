using Microsoft.EntityFrameworkCore.Update;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Generates INSERT, UPDATE and DELETE statements. The relational base implementation
/// already emits standard SQL through <see cref="ISqlGenerationHelper" />, which the
/// provider supplies, so no override is needed yet. Dameng-specific update shapes
/// (RETURNING, identity readback) are added in roadmap phase P3.
/// </summary>
internal sealed class DmUpdateSqlGenerator : UpdateSqlGenerator
{
    public DmUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }
}
