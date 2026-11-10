using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;

namespace DM.EntityFrameworkCore.Diagnostics.Internal;

/// <summary>
/// Provider logging definitions. Dameng currently adds no event ids of its own, so the
/// relational definitions are sufficient. When the provider introduces diagnostics it adds
/// them here, in its own id range, never reusing an EF Core id.
/// </summary>
public class DmLoggingDefinitions : RelationalLoggingDefinitions;
