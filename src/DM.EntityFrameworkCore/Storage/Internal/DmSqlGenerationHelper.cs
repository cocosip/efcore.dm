using System.Data.Common;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// SQL text generation. Identifier and parameter rules come from the dialect layer.
/// </summary>
internal sealed class DmSqlGenerationHelper : RelationalSqlGenerationHelper
{
    public DmSqlGenerationHelper(RelationalSqlGenerationHelperDependencies dependencies)
        : base(dependencies)
    {
    }

    public override string BatchTerminator => DmBatch.Terminator;

    public override string GenerateParameterName(string name) => DmParameters.ToName(name);

    public override void GenerateParameterName(System.Text.StringBuilder builder, string name)
        => DmParameters.Append(builder, name);

    public override string DelimitIdentifier(string identifier) => DmIdentifier.Delimit(identifier);

    public override void DelimitIdentifier(System.Text.StringBuilder builder, string identifier)
        => DmIdentifier.Delimit(builder, identifier);

    public override string EscapeIdentifier(string identifier) => DmIdentifier.Escape(identifier);

    public override void EscapeIdentifier(System.Text.StringBuilder builder, string identifier)
        => DmIdentifier.AppendEscaped(builder, identifier);

    public override string DelimitIdentifier(string name, string? schema) => schema is null
        ? DmIdentifier.Delimit(name)
        : string.Concat(DmIdentifier.Delimit(schema), ".", DmIdentifier.Delimit(name));

    public override void DelimitIdentifier(System.Text.StringBuilder builder, string name, string? schema)
    {
        if (schema is null)
        {
            DmIdentifier.Delimit(builder, name);

            return;
        }

        DmIdentifier.Delimit(builder, schema);
        builder.Append('.');
        DmIdentifier.Delimit(builder, name);
    }
}
