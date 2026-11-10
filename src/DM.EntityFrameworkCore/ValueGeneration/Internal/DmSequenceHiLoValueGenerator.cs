using System.Globalization;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DM.EntityFrameworkCore.ValueGeneration.Internal;

/// <summary>
/// Takes hi-lo blocks from a Dameng sequence.
/// </summary>
/// <remarks>
/// One fetch reserves a whole block, so the first value of a block only costs a round trip and the
/// rest come from memory. The sequence's increment is the block size, which keeps the reservation
/// in the database where a second process can see it.
/// </remarks>
internal sealed class DmSequenceHiLoValueGenerator<TValue> : HiLoValueGenerator<TValue>
{
    private readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;
    private readonly IRelationalConnection _connection;
    private readonly IRelationalCommandDiagnosticsLogger _commandLogger;
    private readonly ISequence _sequence;

    public DmSequenceHiLoValueGenerator(
        IRawSqlCommandBuilder rawSqlCommandBuilder,
        IRelationalConnection connection,
        IRelationalCommandDiagnosticsLogger commandLogger,
        DmSequenceValueGeneratorState generatorState)
        : base(generatorState)
    {
        _rawSqlCommandBuilder = rawSqlCommandBuilder;
        _connection = connection;
        _commandLogger = commandLogger;
        _sequence = generatorState.Sequence;
    }

    /// <summary>
    /// The value is a real key, not a placeholder, so an entity that gets one can be inserted
    /// without reading anything back.
    /// </summary>
    public override bool GeneratesTemporaryValues => false;

    protected override long GetNewLowValue()
        => ToLowValue(
            Command().ExecuteScalar(
                new RelationalCommandParameterObject(
                    _connection,
                    null,
                    null,
                    null,
                    _commandLogger,
                    CommandSource.ValueGenerator)));

    protected override async Task<long> GetNewLowValueAsync(CancellationToken cancellationToken = default)
        => ToLowValue(
            await Command().ExecuteScalarAsync(
                    new RelationalCommandParameterObject(
                        _connection,
                        null,
                        null,
                        null,
                        _commandLogger,
                        CommandSource.ValueGenerator),
                    cancellationToken)
                .ConfigureAwait(false));

    private IRelationalCommand Command()
        => _rawSqlCommandBuilder.Build(DmSequence.NextValueSql(_sequence.Name, _sequence.Schema));

    private static long ToLowValue(object? value)
        => (long)Convert.ChangeType(value!, typeof(long), CultureInfo.InvariantCulture)!;
}
