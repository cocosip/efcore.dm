using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace DM.EntityFrameworkCore.Diagnostics.Internal;

/// <summary>
/// Emits the provider's own warnings.
/// </summary>
/// <remarks>
/// A warning is logged, not thrown: it reports a model the store accepts but that cannot hold every
/// value the property can. A model the store rejects is refused by the model validator instead.
/// </remarks>
internal static class DmLoggerExtensions
{
    private static readonly Action<ILogger, string, string, Exception?> _decimalTypeDefaultWarning
        = LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            DmEventId.DecimalTypeDefaultWarning,
            $"The property '{{0}}.{{1}}' is a decimal without a precision, so Dameng applies "
            + $"DECIMAL({DmTypeSizes.DefaultDecimalPrecision},{DmTypeSizes.DefaultDecimalScale}) and rounds a value that "
            + "does not fit. Set the precision and scale explicitly when that is not intended.");

    /// <summary>
    /// Warns that a decimal property takes the provider default precision and scale.
    /// </summary>
    /// <remarks>
    /// ponytail: logged only, not dispatched to <c>DiagnosticSource</c>; add an event data type when a
    /// tool needs to subscribe to this warning.
    /// </remarks>
    public static void DecimalTypeDefaultWarning(
        this IDiagnosticsLogger<DbLoggerCategory.Model.Validation> diagnostics,
        IProperty property)
        => _decimalTypeDefaultWarning(
            diagnostics.Logger,
            property.DeclaringType.DisplayName(),
            property.Name,
            null);
}
