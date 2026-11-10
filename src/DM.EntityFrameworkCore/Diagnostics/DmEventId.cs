using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// The event ids this provider logs under.
/// </summary>
/// <remarks>
/// Declared in <see cref="Microsoft.EntityFrameworkCore.Diagnostics" /> rather than in a provider
/// namespace, so that a logging configuration written for the Dameng provider keeps compiling
/// against this one. The ids live in the provider's own range and never reuse an EF Core id.
/// </remarks>
public static class DmEventId
{
    private enum Id
    {
        DecimalTypeDefaultWarning = 30000
    }

    private static readonly string ValidationPrefix = DbLoggerCategory.Model.Validation.Name + ".";

    /// <summary>
    /// A decimal property carries no precision, so the store applies the provider default and a value
    /// larger than it is rounded.
    /// </summary>
    public static readonly EventId DecimalTypeDefaultWarning = MakeValidationId(Id.DecimalTypeDefaultWarning);

    private static EventId MakeValidationId(Id id)
        => new((int)id, ValidationPrefix + id);
}
