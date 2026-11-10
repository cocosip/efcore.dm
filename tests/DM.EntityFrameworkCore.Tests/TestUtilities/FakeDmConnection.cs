using System.Collections;
using System.Data;
using System.Data.Common;

// Nullable annotations are disabled for this file: it implements ADO.NET abstractions whose
// annotations cannot all be satisfied by a test double, and the noise would hide real issues.
#nullable disable

namespace DM.EntityFrameworkCore.Tests.TestUtilities;

/// <summary>
/// Records the commands the provider executes, so that DML can be asserted without a server.
/// It is a stub, not a database: it never parses SQL and never stores rows.
/// </summary>
internal sealed class FakeDmConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public List<string> Commands { get; } = [];

    /// <summary>
    /// The row count every statement reports, so that a concurrency failure can be provoked by
    /// setting it to zero.
    /// </summary>
    public int RowsAffected { get; set; } = 1;

    /// <summary>
    /// The timeout the last statement ran with, so a test can see what the context configured.
    /// </summary>
    internal int LastCommandTimeout { get; set; }

    /// <summary>
    /// The value every scalar statement returns.
    /// </summary>
    public object ScalarResult { get; set; }

    /// <summary>
    /// Answers a scalar statement with the next value of this source instead of
    /// <see cref="ScalarResult" />, so that a sequence can advance across calls.
    /// </summary>
    public Func<object> ScalarSource { get; set; }

    /// <summary>
    /// The columns and the row a statement that reads a generated value back returns. An empty
    /// column list makes the command answer with an affected row count instead.
    /// </summary>
    public IReadOnlyList<string> ReadBackColumns { get; set; } = [];

    /// <summary>
    /// The row <see cref="ReadBackColumns" /> describes. A null row makes the read return nothing,
    /// which is how a statement that affected no row is reported.
    /// </summary>
    public object[] ReadBackRow { get; set; }

    /// <summary>
    /// Rows the read back statements return one after another, so that a unit of work that inserts
    /// more than one row can be given the key each insert produced. When this is empty every read
    /// returns <see cref="ReadBackRow" />.
    /// </summary>
    public List<object[]> ReadBackRows { get; } = [];

    private int _readBackIndex;

    internal object[] NextReadBackRow()
        => ReadBackRows.Count == 0
            ? ReadBackRow
            : ReadBackRows[Math.Min(_readBackIndex++, ReadBackRows.Count - 1)];

    /// <summary>
    /// Every transaction this connection opened, so that a test can see whether a unit of work was
    /// wrapped in one and how it ended.
    /// </summary>
    public List<FakeDmTransaction> Transactions { get; } = [];

    public override string ConnectionString { get; set; } = "Server=fake";

    public override string Database => "fake";

    public override string DataSource => "fake";

    public override string ServerVersion => "8.0";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open() => _state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        var transaction = new FakeDmTransaction(this, isolationLevel);
        Transactions.Add(transaction);

        return transaction;
    }

    protected override DbCommand CreateDbCommand() => new FakeDmCommand(this);
}

internal sealed class FakeDmTransaction : DbTransaction
{
    private readonly DbConnection _connection;

    public FakeDmTransaction(DbConnection connection, IsolationLevel isolationLevel)
    {
        _connection = connection;
        IsolationLevel = isolationLevel;
    }

    public override IsolationLevel IsolationLevel { get; }

    public bool Committed { get; private set; }

    public bool RolledBack { get; private set; }

    protected override DbConnection DbConnection => _connection;

    public bool Disposed { get; private set; }

    public override void Commit() => Committed = true;

    public override void Rollback() => RolledBack = true;

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}

internal sealed class FakeDmCommand : DbCommand
{
    private readonly FakeDmConnection _connection;

    public FakeDmCommand(FakeDmConnection connection) => _connection = connection;

    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; } = 30;

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;

    protected override DbConnection DbConnection
    {
        get => _connection;
        set { }
    }

    protected override DbParameterCollection DbParameterCollection { get; } = new FakeDmParameterCollection();

    protected override DbTransaction DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery()
    {
        _connection.Commands.Add(CommandText);
        _connection.LastCommandTimeout = CommandTimeout;

        return _connection.RowsAffected;
    }

    public override object ExecuteScalar()
    {
        _connection.Commands.Add(CommandText);
        _connection.LastCommandTimeout = CommandTimeout;

        return _connection.ScalarSource is null ? _connection.ScalarResult : _connection.ScalarSource();
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new FakeDmParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        _connection.Commands.Add(CommandText);
        _connection.LastCommandTimeout = CommandTimeout;

        // A statement that reads a generated value back produces the row it read. Anything else
        // produces the affected row count the concurrency check reads.
        return _connection.ReadBackColumns.Count > 0
            ? new FakeDmDataReader(
                [new FakeResultSet(_connection.ReadBackColumns, _connection.NextReadBackRow())],
                _connection.RowsAffected)
            : new FakeDmDataReader(
                [new FakeResultSet(["AFFECTED"], [_connection.RowsAffected])],
                _connection.RowsAffected);
    }
}

internal sealed class FakeDmParameter : DbParameter
{
    public override DbType DbType { get; set; } = DbType.String;

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }

    public override object Value { get; set; }

    public override void ResetDbType()
    {
    }
}

internal sealed class FakeDmParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = [];

    public override int Count => _parameters.Count;

    public override object SyncRoot => _parameters;

    public override int Add(object value)
    {
        _parameters.Add((DbParameter)value);

        return _parameters.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value!);
        }
    }

    public override void Clear() => _parameters.Clear();

    public override bool Contains(object value) => _parameters.Contains((DbParameter)value);

    public override bool Contains(string value) => IndexOf(value) >= 0;

    public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName)
        => _parameters.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));

    public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);

    public override void Remove(object value) => _parameters.Remove((DbParameter)value);

    public override void RemoveAt(int index) => _parameters.RemoveAt(index);

    public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => _parameters[index];

    protected override DbParameter GetParameter(string parameterName) => _parameters[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);

        if (index >= 0)
        {
            _parameters[index] = value;
        }
        else
        {
            _parameters.Add(value);
        }
    }
}
