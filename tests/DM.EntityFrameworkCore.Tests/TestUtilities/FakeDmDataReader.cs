using System.Collections;
using System.Data.Common;

// See FakeDmConnection: the ADO.NET annotations cannot be satisfied by a test double.
#nullable disable

namespace DM.EntityFrameworkCore.Tests.TestUtilities;

/// <summary>
/// One result set the fake serves: the column names and the rows. The single-row constructor is what
/// the modification pipeline needs, because every statement it reads from here returns one row. The
/// catalog reader needs many rows per statement.
/// </summary>
internal sealed class FakeResultSet
{
    public FakeResultSet(IReadOnlyList<string> columns, object[] row)
        : this(columns, row is null ? new List<object[]>() : new List<object[]> { row })
    {
    }

    public FakeResultSet(IReadOnlyList<string> columns, IReadOnlyList<object[]> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<object[]> Rows { get; }
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
    private int _row = -1;

    public FakeDmDataReader(IReadOnlyList<FakeResultSet> resultSets, int recordsAffected = 0)
    {
        _resultSets = resultSets.Count == 0 ? [new FakeResultSet(["AFFECTED"], [recordsAffected])] : resultSets;
        _recordsAffected = recordsAffected;
    }

    private FakeResultSet Current => _resultSets[_resultSet];

    private object[] CurrentRow
        => _row >= 0 && _row < Current.Rows.Count ? Current.Rows[_row] : null;

    public override int RecordsAffected => _recordsAffected;

    public override int FieldCount => Current.Columns.Count;

    public override int Depth => 0;

    public override bool HasRows => Current.Rows.Count > 0;

    public override bool IsClosed => _closed;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        if (_row + 1 >= Current.Rows.Count)
        {
            return false;
        }

        _row++;

        return true;
    }

    public override bool NextResult()
    {
        if (_resultSet + 1 >= _resultSets.Count)
        {
            return false;
        }

        _resultSet++;
        _row = -1;

        return true;
    }

    public override void Close() => _closed = true;

    public override IEnumerator GetEnumerator() => (CurrentRow ?? Array.Empty<object>()).GetEnumerator();

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
        => CurrentRow?[ordinal]?.GetType() ?? typeof(object);

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override object GetValue(int ordinal) => CurrentRow![ordinal];

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
        if (CurrentRow is null)
        {
            return 0;
        }

        var count = Math.Min(values.Length, CurrentRow.Length);
        Array.Copy(CurrentRow, values, count);

        return count;
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        => throw new NotSupportedException("The fake reader serves no binary columns.");

    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        => throw new NotSupportedException("The fake reader serves no character columns.");
}
