using System.Collections;
using System.Data;
using System.Data.Common;

// See FakeDmConnection: the ADO.NET annotations cannot be satisfied by a test double.
#nullable disable

namespace DM.EntityFrameworkCore.Tests.TestUtilities;

/// <summary>
/// Serves the result shape EF Core reads after a modification batch: one result set per statement
/// in the batch, each holding a single affected row count. Store-generated values are not served.
/// </summary>
internal sealed class FakeDmDataReader : DbDataReader
{
    private const string AffectedColumn = "AFFECTED";

    private readonly int _rowsAffected;
    private readonly int _resultSets;
    private bool _closed;
    private int _resultSet;
    private bool _read;

    public FakeDmDataReader(int rowsAffected, int resultSets)
    {
        _rowsAffected = rowsAffected;
        _resultSets = Math.Max(1, resultSets);
    }

    public override int RecordsAffected => _rowsAffected;

    public override int FieldCount => 1;

    public override int Depth => 0;

    public override bool HasRows => true;

    public override bool IsClosed => _closed;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        if (_read)
        {
            return false;
        }

        _read = true;

        return true;
    }

    public override bool NextResult()
    {
        if (_resultSet + 1 >= _resultSets)
        {
            return false;
        }

        _resultSet++;
        _read = false;

        return true;
    }

    public override void Close() => _closed = true;

    public override IEnumerator GetEnumerator() => new object[] { _rowsAffected }.GetEnumerator();

    public override string GetName(int ordinal)
        => ordinal == 0 ? AffectedColumn : throw Column(ordinal);

    public override int GetOrdinal(string name)
        => string.Equals(name, AffectedColumn, StringComparison.OrdinalIgnoreCase) ? 0 : throw Column(0);

    public override Type GetFieldType(int ordinal)
        => ordinal == 0 ? typeof(int) : throw Column(ordinal);

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override object GetValue(int ordinal)
        => ordinal == 0 ? _rowsAffected : throw Column(ordinal);

    public override int GetInt32(int ordinal) => ordinal == 0 ? _rowsAffected : throw Column(ordinal);

    public override bool IsDBNull(int ordinal) => GetFieldType(ordinal) is not null && false;

    public override int GetValues(object[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        values[0] = _rowsAffected;

        return 1;
    }

    public override bool GetBoolean(int ordinal) => throw Column(ordinal);

    public override byte GetByte(int ordinal) => throw Column(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        => throw Column(ordinal);

    public override char GetChar(int ordinal) => throw Column(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        => throw Column(ordinal);

    public override DateTime GetDateTime(int ordinal) => throw Column(ordinal);

    public override decimal GetDecimal(int ordinal) => throw Column(ordinal);

    public override double GetDouble(int ordinal) => throw Column(ordinal);

    public override float GetFloat(int ordinal) => throw Column(ordinal);

    public override Guid GetGuid(int ordinal) => throw Column(ordinal);

    public override short GetInt16(int ordinal) => throw Column(ordinal);

    public override long GetInt64(int ordinal) => throw Column(ordinal);

    public override string GetString(int ordinal) => throw Column(ordinal);

    private static IndexOutOfRangeException Column(int ordinal)
        => new($"The fake reader serves one affected row count column, so column {ordinal} cannot be read.");
}
