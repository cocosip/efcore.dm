// Nullable annotations are disabled for this file: the canned catalog rows carry null for absent
// attributes, which is what the reader sees.
#nullable disable

using DM.EntityFrameworkCore.Storage.Internal;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.Tests.Storage;

/// <summary>
/// Tests the reverse engineering factory against canned catalog rows. The queries themselves come from
/// <c>Dialect/DmCatalog</c> and are not verified against a server; what these tests establish is that
/// the rows a catalog returns become the model scaffolding builds from.
/// </summary>
public class DmDatabaseModelFactoryTests
{
    private static readonly string[] ObjectColumns = ["OWNER", "TABLE_NAME"];

    private static readonly string[] ColumnColumns =
        ["OWNER", "TABLE_NAME", "COLUMN_NAME", "DATA_TYPE", "DATA_LENGTH", "DATA_PRECISION", "DATA_SCALE", "NULLABLE", "DATA_DEFAULT", "IDENTITY_COLUMN"];

    private static readonly string[] ConstraintColumns = ["OWNER", "TABLE_NAME", "CONSTRAINT_NAME", "CONSTRAINT_TYPE"];
    private static readonly string[] ConstraintColumnColumns = ["OWNER", "CONSTRAINT_NAME", "COLUMN_NAME", "POSITION"];
    private static readonly string[] IndexColumns = ["OWNER", "TABLE_NAME", "INDEX_NAME", "UNIQUENESS", "COLUMN_NAME", "COLUMN_POSITION"];

    private static readonly string[] ForeignKeyColumns =
        ["OWNER", "TABLE_NAME", "CONSTRAINT_NAME", "COLUMN_NAME", "POSITION", "PK_OWNER", "PK_TABLE", "PK_COLUMN"];

    private static readonly string[] SequenceColumns =
        ["SEQUENCE_OWNER", "SEQUENCE_NAME", "MIN_VALUE", "MAX_VALUE", "INCREMENT_BY", "CYCLE_FLAG", "ORDER_FLAG", "CACHE_SIZE", "LAST_NUMBER"];

    [Fact]
    public void Create_ReadsTablesColumnsAndThePrimaryKey()
    {
        var model = Create(
        [
            Rows(ObjectColumns, ["APP", "ORDERS"]),
            Rows(
                ColumnColumns,
                ["APP", "ORDERS", "ID", "INT", 4, null, null, "N", null, "YES"],
                ["APP", "ORDERS", "NAME", "VARCHAR", 50, null, null, "Y", null, "NO"],
                ["APP", "ORDERS", "PRICE", "DECIMAL", null, 10, 2, "Y", null, "NO"]),
            Rows(ConstraintColumns, ["APP", "ORDERS", "PK_ORDERS", "P"]),
            Rows(ConstraintColumnColumns, ["APP", "PK_ORDERS", "ID", 1]),
            Rows(IndexColumns),
            Rows(ForeignKeyColumns),
            Rows(SequenceColumns)
        ]);

        var table = Assert.Single(model.Tables);

        Assert.Equal("ORDERS", table.Name);
        Assert.Equal("APP", table.Schema);
        Assert.Equal(["ID", "NAME", "PRICE"], table.Columns.Select(column => column.Name));

        Assert.Equal("INT", table.Columns[0].StoreType);
        Assert.False(table.Columns[0].IsNullable);
        Assert.Equal(ValueGenerated.OnAdd, table.Columns[0].ValueGenerated);

        Assert.Equal("VARCHAR(50)", table.Columns[1].StoreType);
        Assert.True(table.Columns[1].IsNullable);

        Assert.Equal("DECIMAL(10,2)", table.Columns[2].StoreType);

        Assert.NotNull(table.PrimaryKey);
        Assert.Equal("PK_ORDERS", table.PrimaryKey.Name);
        Assert.Equal(["ID"], table.PrimaryKey.Columns.Select(column => column.Name));
    }

    /// <summary>
    /// A character column is declared in characters, which the catalog reports twice: on the measured
    /// server a `VARCHAR2(3 CHAR)` column has a length of 6 bytes and a character length of 3. The unit
    /// belongs to the store type, because the same length without it counts bytes and a value the model
    /// says fits would overflow.
    /// </summary>
    [Fact]
    public void Create_ReadsTheCharacterUnitOfAnAnsiColumn()
    {
        // The catalog query reports the column id and the comment before the character length, so a row
        // that leaves them out shifts every value after them.
        string[] columns =
        [
            .. ColumnColumns,
            "COLUMN_ID",
            "COMMENTS",
            "CHAR_LENGTH"
        ];

        var model = Create(
        [
            Rows(ObjectColumns, ["APP", "ORDERS"]),
            Rows(
                columns,
                ["APP", "ORDERS", "CODE", "VARCHAR2", 6, null, null, "N", null, "NO", 1, null, 3],
                ["APP", "ORDERS", "NAME", "NVARCHAR2", 6, null, null, "N", null, "NO", 2, null, 3],
                ["APP", "ORDERS", "BYTE_LENGTH", "VARCHAR2", 3, null, null, "N", null, "NO", 3, null, 3],
                ["APP", "ORDERS", "BINARY", "VARBINARY", 16, null, null, "N", null, "NO", 4, null, 16]),
            Rows(ConstraintColumns),
            Rows(ConstraintColumnColumns),
            Rows(IndexColumns),
            Rows(ForeignKeyColumns),
            Rows(SequenceColumns)
        ]);

        Assert.Equal(
            ["VARCHAR2(3 CHAR)", "NVARCHAR2(3)", "VARCHAR2(3)", "VARBINARY(16)"],
            model.Tables.Single().Columns.Select(column => column.StoreType));
    }

    [Fact]
    public void Create_ReadsTheForeignKeysUniqueConstraintsAndIndexes()
    {
        var model = Create(
        [
            Rows(ObjectColumns, ["APP", "ORDERS"], ["APP", "LINES"]),
            Rows(
                ColumnColumns,
                ["APP", "ORDERS", "ID", "INT", 4, null, null, "N", null, "YES"],
                ["APP", "LINES", "ID", "INT", 4, null, null, "N", null, "YES"],
                ["APP", "LINES", "ORDER_ID", "INT", 4, null, null, "N", null, "NO"],
                ["APP", "LINES", "CODE", "VARCHAR", 20, null, null, "N", null, "NO"]),
            Rows(
                ConstraintColumns,
                ["APP", "ORDERS", "PK_ORDERS", "P"],
                ["APP", "LINES", "PK_LINES", "P"],
                ["APP", "LINES", "UQ_LINES_CODE", "U"]),
            Rows(
                ConstraintColumnColumns,
                ["APP", "PK_ORDERS", "ID", 1],
                ["APP", "PK_LINES", "ID", 1],
                ["APP", "UQ_LINES_CODE", "CODE", 1]),
            Rows(
                IndexColumns,
                ["APP", "LINES", "IX_LINES_ORDER", "UNIQUE", "ORDER_ID", 1],
                ["APP", "LINES", "IX_LINES_ORDER", "UNIQUE", "CODE", 2]),
            Rows(ForeignKeyColumns, ["APP", "LINES", "FK_LINES_ORDERS", "ORDER_ID", 1, "APP", "ORDERS", "ID"]),
            Rows(SequenceColumns)
        ]);

        var lines = model.Tables.Single(table => table.Name == "LINES");

        var foreignKey = Assert.Single(lines.ForeignKeys);
        Assert.Equal("FK_LINES_ORDERS", foreignKey.Name);
        Assert.Equal("ORDERS", foreignKey.PrincipalTable.Name);
        Assert.Equal(["ORDER_ID"], foreignKey.Columns.Select(column => column.Name));
        Assert.Equal(["ID"], foreignKey.PrincipalColumns.Select(column => column.Name));

        var unique = Assert.Single(lines.UniqueConstraints);
        Assert.Equal("UQ_LINES_CODE", unique.Name);
        Assert.Equal(["CODE"], unique.Columns.Select(column => column.Name));

        var index = Assert.Single(lines.Indexes);
        Assert.Equal("IX_LINES_ORDER", index.Name);
        Assert.True(index.IsUnique);
        Assert.Equal(["ORDER_ID", "CODE"], index.Columns.Select(column => column.Name));
    }

    [Fact]
    public void Create_HonoursTheSchemaAndTableFilters()
    {
        var model = Create(
        [
            Rows(ObjectColumns, ["APP", "ORDERS"], ["OTHER", "ORDERS"], ["APP", "LINES"]),
            Rows(ColumnColumns),
            Rows(ConstraintColumns),
            Rows(ConstraintColumnColumns),
            Rows(IndexColumns),
            Rows(ForeignKeyColumns),
            Rows(SequenceColumns)
        ],
        schemas: ["APP"],
        tables: ["ORDERS"]);

        var table = Assert.Single(model.Tables);

        Assert.Equal("ORDERS", table.Name);
        Assert.Equal("APP", table.Schema);
    }

    [Fact]
    public void Create_SkipsAKeyWhoseColumnsWereNotRead()
    {
        // A constraint that names a column the catalog did not report would produce a primary key with
        // fewer columns than the database has, which is worse than no key at all.
        var model = Create(
        [
            Rows(ObjectColumns, ["APP", "ORDERS"]),
            Rows(ColumnColumns, ["APP", "ORDERS", "ID", "INT", 4, null, null, "N", null, "YES"]),
            Rows(ConstraintColumns, ["APP", "ORDERS", "PK_ORDERS", "P"]),
            Rows(ConstraintColumnColumns, ["APP", "PK_ORDERS", "MISSING", 1]),
            Rows(IndexColumns),
            Rows(ForeignKeyColumns),
            Rows(SequenceColumns)
        ]);

        var table = Assert.Single(model.Tables);

        Assert.Null(table.PrimaryKey);
    }

    [Fact]
    public void Create_ReadsSequences()
    {
        var model = Create(
        [
            Rows(ObjectColumns),
            Rows(ColumnColumns),
            Rows(ConstraintColumns),
            Rows(ConstraintColumnColumns),
            Rows(IndexColumns),
            Rows(ForeignKeyColumns),
            Rows(SequenceColumns, ["APP", "ORDERS_SEQUENCE", 1, 999999999, 10, "N", "N", 20, 41])
        ]);

        var sequence = Assert.Single(model.Sequences);

        Assert.Equal("ORDERS_SEQUENCE", sequence.Name);
        Assert.Equal("APP", sequence.Schema);
        Assert.Equal(1L, sequence.MinValue);
        Assert.Equal(10, sequence.IncrementBy);
        Assert.False(sequence.IsCyclic);
        Assert.Null(sequence.StartValue);
    }

    [Fact]
    public void Create_ReadsTheTableAndColumnComments()
    {
        var model = Create(
        [
            Rows(["OWNER", "TABLE_NAME", "COMMENTS"], ["APP", "ORDERS", "the orders"]),
            Rows(
                [.. ColumnColumns, "COLUMN_ID", "COMMENTS"],
                ["APP", "ORDERS", "ID", "INT", 4, null, null, "N", null, "YES", 1, null],
                ["APP", "ORDERS", "STATUS", "VARCHAR", 20, null, null, "N", null, "NO", 2, "the order status"]),
            Rows(ConstraintColumns),
            Rows(ConstraintColumnColumns),
            Rows(IndexColumns),
            Rows(ForeignKeyColumns),
            Rows(SequenceColumns)
        ]);

        var table = Assert.Single(model.Tables);

        Assert.Equal("the orders", table.Comment);
        Assert.Null(table.Columns[0].Comment);
        Assert.Equal("the order status", table.Columns[1].Comment);
    }

    [Fact]
    public void DesignTimeServices_RegisterThisProvidersFactory()
    {
        using var services = ProviderDesignTimeServices.Build();

        Assert.IsType<DmDatabaseModelFactory>(services.GetRequiredService<IDatabaseModelFactory>());
    }

    /// <summary>
    /// EF Core 10 gives reverse engineering no place for a view: the factory options carry tables and
    /// schemas, and the model carries tables and sequences. The public <c>DatabaseView</c> type exists,
    /// but nothing in the pipeline consumes it, so a provider cannot report one. When this changes, the
    /// matrix row that records views as unread has to change with it.
    /// </summary>
    [Fact]
    public void TheReverseEngineeringContract_HasNoViews()
    {
        Assert.True(
            typeof(DatabaseModelFactoryOptions).GetProperty("Views") is null,
            "The factory options now carry views; the matrix row that defers them must be revisited.");

        Assert.True(
            typeof(DatabaseModel).GetProperty("Views") is null,
            "The database model now carries views; the matrix row that defers them must be revisited.");
    }

    private static DatabaseModel Create(
        FakeResultSet[] resultSets,
        IEnumerable<string> schemas = null,
        IEnumerable<string> tables = null)
    {
        var connection = new FakeDmConnection();

        foreach (var resultSet in resultSets)
        {
            connection.ResultSets.Enqueue(resultSet);
        }

        return new DmDatabaseModelFactory().Create(
            connection,
            new DatabaseModelFactoryOptions(tables ?? [], schemas ?? []));
    }

    private static FakeResultSet Rows(string[] columns, params object[][] rows)
        => new(columns, (IReadOnlyList<object[]>)rows);
}