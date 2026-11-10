using System.Collections;
using System.Data.Common;

// See FakeDmConnection: the ADO.NET annotations cannot be satisfied by a test double.
#nullable disable

namespace DM.EntityFrameworkCore.Tests.TestUtilities;

/// <summary>
/// One result set the fake serves: the column names and at most one row, because every statement
/// the provider reads from here returns a single row. A null row makes the result set empty.
/// </summary>
internal sealed class FakeResultSet
{
    public FakeResultSet(IReadOnlyList<string> columns, object[] row)
    {
        Columns = columns;
        Row = row;
    }

    public IReadOnlyList<string> Columns { get; }

    public object[] Row { get; }
}

/// <summary>
/// Serves the result shape EF Core reads after a modification batch.
/// </summary>
internal sealed class FakeDmDataReader : DbDataReader
{
    private readonly IReadOnlyList<FakeResultSet> _resultSets;
    private readonly int _recordsAffected;
    private bool _closed;
    private int _resultSet;
    private bool _read;

    public FakeDmDataReader(IReadOnlyList<FakeResultSet> resultSets, int recordsAffected = 0)
    {
        _resultSets = resultSets.Count == 0 ? [new FakeResultSet(["AFFECTED"], [recordsAffected])] : resultSets;
        _recordsAffected = recordsAffected;
    }

    private FakeResultSet Current => _resultSets[_resultSet];

    public override int RecordsAffected => _recordsAffected;

    public override int FieldCount => Current.Columns.Count;

    public override int Depth => 0;

    public override bool HasRows => Current.Row is not null;

    public override bool IsClosed => _closed;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        if (_read || Current.Row is null)
        {
            return false;
        }

        _read = true;

        return true;
    }

    public override bool NextResult()
    {
        if (_resultSet + 1 >= _resultSets.Count)
        {
            return false;
        }

        _resultSet++;
        _read = false;

        return true;
    }

    public override void Close() => _closed = true;

    public override IEnumerator GetEnumerator() => (Current.Row ?? []).GetEnumerator();

    public override string GetName(int ordinal) => Current.Columns[ordinal];

    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < Current.Columns.Count; i++)
        {
            if (string.Equals(Current.Columns[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new IndexOutOfRangeException($"The fake result set has no column named '{name}'.");
    }

    public override Type GetFieldType(int ordinal)
        => Current.Row?[ordinal]?.GetType() ?? typeof(object);

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override object GetValue(int ordinal) => Current.Row![ordinal];

    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;

    public override int GetValues(object[] values)
    {
        if (Current.Row is null)
        {
            return 0;
        }

        var count = Math.Min(values.Length, Current.Row.Length);
        Array.Copy(Current.Row, values, count);

        return count;
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        => throw new NotSupportedException("The fake reader serves no binary columns.");

    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        => throw new NotSupportedException("The fake reader serves no character columns.");
}
