namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// Catalog queries for reverse engineering. They read the Oracle compatible views Dameng publishes,
/// which is what a schema reader sees its objects through.
/// </summary>
/// <remarks>
/// This layer is deliberately free of any EF Core type reference. Every query names its columns
/// explicitly so that a view with extra columns is read the same, and a column a view does not have is
/// skipped by the reader rather than failing the query. The queries are not verified against a server;
/// the capability matrix records that.
/// <para>
/// Rows are written in the order the caller consumes them: the object first, then its parts.
/// </para>
/// </remarks>
internal static class DmCatalog
{
    /// <summary>
    /// User tables, excluding the schemas Dameng itself owns.
    /// </summary>
    public const string Tables = """
        SELECT OWNER, TABLE_NAME
        FROM ALL_TABLES
        WHERE OWNER NOT IN ('SYS', 'SYSAUDITOR', 'SYSDBA', 'SYSSSO', 'CTISYS')
        ORDER BY OWNER, TABLE_NAME
        """;

    /// <summary>
    /// User views, excluding the schemas Dameng itself owns.
    /// </summary>
    public const string Views = """
        SELECT OWNER, VIEW_NAME
        FROM ALL_VIEWS
        WHERE OWNER NOT IN ('SYS', 'SYSAUDITOR', 'SYSDBA', 'SYSSSO', 'CTISYS')
        ORDER BY OWNER, VIEW_NAME
        """;

    /// <summary>
    /// Columns with the attributes the model needs: the store type, the length or the precision, the
    /// identity flag and the default.
    /// </summary>
    public const string Columns = """
        SELECT OWNER, TABLE_NAME, COLUMN_NAME, DATA_TYPE, DATA_LENGTH, DATA_PRECISION, DATA_SCALE,
               NULLABLE, DATA_DEFAULT, IDENTITY_COLUMN, COLUMN_ID
        FROM ALL_TAB_COLUMNS
        ORDER BY OWNER, TABLE_NAME, COLUMN_ID
        """;

    /// <summary>
    /// Primary keys and unique constraints, one row per constraint.
    /// </summary>
    public const string Constraints = """
        SELECT OWNER, TABLE_NAME, CONSTRAINT_NAME, CONSTRAINT_TYPE
        FROM ALL_CONSTRAINTS
        WHERE CONSTRAINT_TYPE IN ('P', 'U')
        ORDER BY OWNER, TABLE_NAME, CONSTRAINT_NAME
        """;

    /// <summary>
    /// The columns of each primary key or unique constraint, in the order the constraint declares them.
    /// </summary>
    public const string ConstraintColumns = """
        SELECT OWNER, CONSTRAINT_NAME, COLUMN_NAME, POSITION
        FROM ALL_CONS_COLUMNS
        ORDER BY OWNER, CONSTRAINT_NAME, POSITION
        """;

    /// <summary>
    /// Indexes and the columns they cover. The unique indexes are read as indexes; a unique constraint
    /// is read as a constraint, so the two are not reported twice.
    /// </summary>
    public const string Indexes = """
        SELECT i.OWNER, i.TABLE_NAME, i.INDEX_NAME, i.UNIQUENESS, ic.COLUMN_NAME, ic.COLUMN_POSITION
        FROM ALL_INDEXES i
        INNER JOIN ALL_IND_COLUMNS ic
            ON ic.INDEX_OWNER = i.OWNER AND ic.INDEX_NAME = i.INDEX_NAME
        WHERE i.INDEX_TYPE <> 'LOB'
        ORDER BY i.OWNER, i.TABLE_NAME, i.INDEX_NAME, ic.COLUMN_POSITION
        """;

    /// <summary>
    /// Foreign keys with the column pair of each referenced position.
    /// </summary>
    public const string ForeignKeys = """
        SELECT fk.OWNER, fk.TABLE_NAME, fk.CONSTRAINT_NAME,
               fk_column.COLUMN_NAME, fk_column.POSITION,
               pk.OWNER, pk.TABLE_NAME, pk_column.COLUMN_NAME
        FROM ALL_CONSTRAINTS fk
        INNER JOIN ALL_CONS_COLUMNS fk_column
            ON fk_column.OWNER = fk.OWNER AND fk_column.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
        INNER JOIN ALL_CONSTRAINTS pk
            ON pk.OWNER = fk.R_OWNER AND pk.CONSTRAINT_NAME = fk.R_CONSTRAINT_NAME
        INNER JOIN ALL_CONS_COLUMNS pk_column
            ON pk_column.OWNER = pk.OWNER AND pk_column.CONSTRAINT_NAME = pk.CONSTRAINT_NAME
           AND pk_column.POSITION = fk_column.POSITION
        WHERE fk.CONSTRAINT_TYPE = 'R'
        ORDER BY fk.OWNER, fk.TABLE_NAME, fk.CONSTRAINT_NAME, fk_column.POSITION
        """;

    /// <summary>
    /// Sequences, which a hi-lo property reads its blocks from.
    /// </summary>
    public const string Sequences = """
        SELECT SEQUENCE_OWNER, SEQUENCE_NAME, MIN_VALUE, MAX_VALUE, INCREMENT_BY,
               CYCLE_FLAG, ORDER_FLAG, CACHE_SIZE, LAST_NUMBER
        FROM ALL_SEQUENCES
        WHERE SEQUENCE_OWNER NOT IN ('SYS', 'SYSAUDITOR', 'SYSDBA', 'SYSSSO', 'CTISYS')
        ORDER BY SEQUENCE_OWNER, SEQUENCE_NAME
        """;
}
