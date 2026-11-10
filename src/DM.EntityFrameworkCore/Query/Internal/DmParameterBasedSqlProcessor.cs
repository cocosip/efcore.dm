using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Applies parameters to the SQL tree and runs nullability processing.
/// </summary>
internal sealed class DmParameterBasedSqlProcessor : RelationalParameterBasedSqlProcessor
{
    public DmParameterBasedSqlProcessor(
        RelationalParameterBasedSqlProcessorDependencies dependencies,
        RelationalParameterBasedSqlProcessorParameters parameters)
        : base(dependencies, parameters)
    {
    }
}
