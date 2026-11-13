using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DM.EntityFrameworkCore.ValueGeneration.Internal;

/// <summary>
/// Chooses the value generator for a property.
/// </summary>
/// <remarks>
/// Only <see cref="DmValueGenerationStrategy.SequenceHiLo" /> is answered here. Every other
/// strategy keeps the relational behaviour, which covers an identity column the store fills and a
/// value the model supplies.
/// </remarks>
internal sealed class DmValueGeneratorSelector : RelationalValueGeneratorSelector
{
    private readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;
    private readonly IRelationalConnection _connection;
    private readonly IRelationalCommandDiagnosticsLogger _commandLogger;

    public DmValueGeneratorSelector(
        ValueGeneratorSelectorDependencies dependencies,
        IRawSqlCommandBuilder rawSqlCommandBuilder,
        IRelationalConnection connection,
        IRelationalCommandDiagnosticsLogger commandLogger)
        : base(dependencies)
    {
        _rawSqlCommandBuilder = rawSqlCommandBuilder;
        _connection = connection;
        _commandLogger = commandLogger;
    }

    private DmValueGeneratorCache DmCache => (DmValueGeneratorCache)Cache;

    public override ValueGenerator Create(IProperty property, ITypeBase typeBase)
    {
        if (property.GetDmValueGenerationStrategy() != DmValueGenerationStrategy.SequenceHiLo)
        {
            return base.Create(property, typeBase);
        }

        var state = DmCache.GetOrAddSequenceState(FindHiLoSequence(property, typeBase));
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

        return clrType switch
        {
            _ when clrType == typeof(byte) => new DmSequenceHiLoValueGenerator<byte>(_rawSqlCommandBuilder, _connection, _commandLogger, state),
            _ when clrType == typeof(sbyte) => new DmSequenceHiLoValueGenerator<sbyte>(_rawSqlCommandBuilder, _connection, _commandLogger, state),
            _ when clrType == typeof(short) => new DmSequenceHiLoValueGenerator<short>(_rawSqlCommandBuilder, _connection, _commandLogger, state),
            _ when clrType == typeof(ushort) => new DmSequenceHiLoValueGenerator<ushort>(_rawSqlCommandBuilder, _connection, _commandLogger, state),
            _ when clrType == typeof(int) => new DmSequenceHiLoValueGenerator<int>(_rawSqlCommandBuilder, _connection, _commandLogger, state),
            _ when clrType == typeof(uint) => new DmSequenceHiLoValueGenerator<uint>(_rawSqlCommandBuilder, _connection, _commandLogger, state),
            _ when clrType == typeof(long) => new DmSequenceHiLoValueGenerator<long>(_rawSqlCommandBuilder, _connection, _commandLogger, state),
            _ when clrType == typeof(ulong) => new DmSequenceHiLoValueGenerator<ulong>(_rawSqlCommandBuilder, _connection, _commandLogger, state),
            _ => throw new InvalidOperationException(
                $"The property '{typeBase.DisplayName()}.{property.Name}' declares the hi-lo strategy, "
                + $"but '{clrType.Name}' has no integer value generator. Use an identity column or a "
                + "sequence for a key of another type.")
        };
    }

    /// <summary>
    /// The sequence the property names, or the model's default one. A strategy that points at no
    /// sequence fails here rather than silently generating values from somewhere else.
    /// </summary>
    private static ISequence FindHiLoSequence(IProperty property, ITypeBase typeBase)
    {
        var name = property.GetDmHiLoSequenceName() ?? typeBase.Model.GetHiLoSequenceName();
        var schema = property.GetDmHiLoSequenceSchema() ?? typeBase.Model.GetHiLoSequenceSchema();

        return typeBase.Model.FindSequence(name, schema)
            ?? throw new InvalidOperationException(
                $"The property '{typeBase.DisplayName()}.{property.Name}' takes hi-lo values from the sequence "
                + $"'{name}', which is not part of the model. Declare it with HasSequence, or use an identity "
                + "column for the key instead.");
    }
}
