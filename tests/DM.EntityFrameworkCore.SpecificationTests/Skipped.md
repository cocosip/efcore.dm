# Specification Suite Skip Ledger

Status: **living document**. Every case that does not run, or does not pass, is listed here with the
measurement that decided it. A row leaves this file when the suite passes the case, not when someone
intends to fix it.

The suite is not part of the offline gate: `BuiltInDataTypesDmTest` needs a running Dameng instance. The
store drops the objects its model declares, and the updates and migrations fixtures own a schema of their
own, so the database it runs against keeps every object an application already has.

Measured on 2026-11-11 against DM 8.1.4.80, `COMPATIBLE_MODE` 2: 32 cases, 31 passed, 1 failed. The
failure is below, and it is the whole of the gap this fixture set exposes.

The store generated fixture set (`StoreGeneratedDmTest`, 211 cases) ran against the same instance on
2026-11-11 and every case passed, so it adds no entry here.

The `Where` set of the Northwind query suite (`NorthwindWhereQueryDmTest`, 408 cases) ran against the
same instance on 2026-11-11 and every case passed, so it adds no entry either. Nine of its cases
compare two constructed values, which the relational pipeline does not translate for any provider; the
class asserts that failure, as the SQL Server test does, so those cases pass as written (EF Core issue
#14672).

The `Join` set of the Northwind query suite (`NorthwindJoinQueryDmTest`, 132 cases) ran against the same
instance on 2026-11-11: 126 passed, two did not (one case in its two forms), and four are skipped
upstream (`#30677` and `#35028`). The failing case is below. Two other families failed on that run and
are fixed rather than recorded: a collection joined in a query was rendered as a bare `VALUES` inside a
set operation, which the server rejects with `语法分析出错`, and is now a `SELECT ... FROM DUAL` per row
joined by `UNION ALL`; a tautological join predicate was rendered as a bare `1`, which the server
rejects with `查询使用值表达式作为过滤条件`, and is now compared with `1` like the statement's own
predicate.

The `Select` set of the Northwind query suite (`NorthwindSelectQueryDmTest`, 372 cases) ran against the
same instance on 2026-11-11: 364 passed, eight did not (four cases in their two forms), and the shipped
base skips none of them. The four are below, and they are two server limitations: a nested `APPLY` whose
inner query references the outermost table, and a row picked without an ordering. A fifth case, the one
that asked for the first row of a keyless entity, did not pass in that run because this project's store
left the keyless table empty; the fixture now seeds it, and the case passes. The run found three
defects that were fixed: a nested `Take` composed through `LEAST` left the function without a type
mapping when an argument was a parameter; a condition in a `CASE` result was written as a bare condition,
which the server rejects at the comparison; and a difference of two dates was translated although the
server answers it with a number of days, which cannot be read back as a time span, so it is now
projected as the date and subtracted by the client.

The `GroupBy` set (`NorthwindGroupByQueryDmTest`, 512 cases) ran against the same instance on 2026-11-11:
502 passed, six did not (three cases in their two forms), and the shipped base skips four. The three are
below, and they are the same server limitation: SQL Server accepts an outer reference where this server
needs the referenced column to be grouped, or refuses it outright. The run found one defect that was
fixed: a null-safe comparison of an `EXTRACT` key was written `MONTH IS NULL OR "OrderDate" IS NULL`,
because the keyword argument propagated nullability like a value, which the server rejects with
`无效的列名[MONTH]`; a fragment no longer propagates.

The `AggregateOperators` set (`NorthwindAggregateOperatorsQueryDmTest`, 422 cases) ran against the same
instance on 2026-11-12: 420 passed, two did not (one case in its two forms), and the shipped base skips
none of them. The case is below, and it is the same server limitation the nested `APPLY` rows record.
The run found no provider defect; the one case that differed from the client's arithmetic is an average
the server computes to its own decimal scale, which the class compares at three decimal places as the
reference provider does, and the one that read a keyless set exposed a gap in this project's store
rather than in the provider: the shipped store is created from a script that fills the keyless tables,
while this store is created from the model, so the fixture now projects those rows from the keyed ones.

The `Include` set of the Northwind query suite (`NorthwindIncludeQueryDmTest`, 236 cases) ran against the
same instance on 2026-11-12 and every case passed. Its first run failed eighteen cases, all from
`MultipleCollectionIncludeWarning`: the shared fixture base throws every warning, and this one is
guidance about splitting a query that loads two collections rather than a provider defect. The warning is
now logged by the shared fixture, as the shipped fixtures log the other guidance warnings, so it adds no
entry here.

The `Updates` set (`UpdatesDmTest`, 36 cases) ran against the same instance on 2026-11-12: 35 ran and
passed, and one does not run because it needs a filtered unique index the server cannot create. It is
under "Cases that do not run". The run found and fixed three defects: the identifier limit was not
declared, so a model with a generated name past 128 characters failed while its tables were created; a
byte array concurrency token the model leaves unbounded was mapped to a `BLOB`, which the server cannot
compare in a statement's predicate, so updates and deletes were rejected with a data type error; and the
provider's own concurrency message left out the framework's explanatory link. The fixture also maps the
abstract product base the shared foreign key case needs, the way the SQL Server fixture does.

## Cases that do not pass

| Case | Cause | Fix |
|---|---|---|
| `Can_perform_query_with_max_length` | The case compares a `byte[9000]` with the equality operator, and this server cannot do both ends of that: mapped to `VARBINARY(9000)`, as the reference provider maps it, the value is refused with `记录超长`, because a row cannot hold what the type declares — measured on a one column table, 7500 bytes were stored and 8000 failed; mapped to a `BLOB`, which is what a byte array past that ceiling becomes, the statement is refused with `字符串过长`, because a large object compares with `DBMS_LOB.COMPARE` rather than with an operator | Not a provider defect: a large value must be a large object, and a large object has no equality operator. The case is inapplicable as written. The insert of the same row does pass now, since the oversized byte array is a `BLOB` rather than a column that cannot hold it |
| `SelectMany_with_selecting_outer_entity_column_and_inner_column` | The statement is the same `CROSS APPLY` with `ORDER BY "o"."OrderID" OFFSET 0 ROWS` inside and `ORDER BY "c"."CustomerID"` outside that the SQL Server test asserts, but the server answered the rows of one customer in a different order: the first row was `1998-04-09` where `1997-08-25` was expected, and the set of rows is the same. SQL does not order the rows inside a subquery, so the outer key is the whole of the ordering guarantee; the case asserts the full order with `assertOrder` | Not a provider defect: the framework's translation keeps the inner ordering and the server does not preserve it. The case is inapplicable as written for this server |
| `Collection_projection_selecting_outer_element_followed_by_take` | The statement the framework builds contains an `OUTER APPLY` whose subquery contains another `OUTER APPLY`, and the inner one references the outermost table (`"c0"."CustomerID"`). Dameng rejects that with `不支持的语句类型`. Measured on 8.1.4.80: a nested `OUTER APPLY` without that reference parsed and answered, the same nested `OUTER APPLY` referencing the outermost table answered `不支持的语句类型`, and a single level of `OUTER APPLY` answered | Not a provider defect: the shape comes from the framework's translation of a nested collection projection, and the correlation is what makes it unanswerable |
| `Select_nested_collection_deep` | The statement has the same nested `OUTER APPLY` whose inner query references the outermost table, which the server rejects with `不支持的语句类型`; the measurement is in the row above | Not a provider defect, same reason |
| `Select_nested_collection_deep_distinct_no_identifiers` | The statement has the same nested `OUTER APPLY` whose inner query references the outermost table, which the server rejects with `不支持的语句类型`; the measurement is in the row above | Not a provider defect, same reason |
| `Take_on_top_level_and_on_collection_projection_with_outer_apply` | The statement selects the first row of the orders whose customer starts with `F` with no ordering, so the row is whichever the server answers first; the case compares the fields of that row, and the server answered `10347` where the reference provider answered `10264`. The rows are the same set | Not a provider defect: a row limiting operation without an ordering has no defined row, which the framework logs as `RowLimitingOperationWithoutOrderByWarning` |
| `GroupBy_aggregate_from_multiple_query_in_same_projection_2` | The statement has `SELECT COUNT(*) + MIN("o"."OrderID") … FROM "Employees" … GROUP BY "e"."City"` inside a subquery of a grouped query. The server answers `不是 GROUP BY 表达式` because `"o"."OrderID"` is an outer column that is not a grouping column of the outer query. Measured on 8.1.4.80: the same subquery with the outer column inside `COUNT` answered `存在集函数`, and the outer query with the subquery narrowed to its own tables answered | Not a provider defect: the framework's translation reads an outer column inside a subquery's aggregate, which SQL Server accepts; this server requires the referenced column to be grouped |
| `GroupBy_with_aggregate_containing_complex_where` | The subquery's `WHERE` references the outer query's aggregate `MAX("o"."OrderID")`, which the server answers with `存在集函数`. Measured on 8.1.4.80: the same statement with the outer aggregate replaced by a constant answered | Not a provider defect: the framework places the outer aggregate in the subquery's predicate, which SQL Server accepts |
| `Complex_query_with_groupBy_in_subquery4` | A scalar subquery inside an `OUTER APPLY` contains a derived table that references the outermost `Customers` row, and the server answers `引用列未找到`. Measured on 8.1.4.80: the same shape with the reference to the outermost row answered `不支持的语句类型`, while the same shape with the reference narrowed to the `APPLY`'s own row answered | Not a provider defect: a reference two scopes out, through a derived table inside an `APPLY`, is the same limitation the nested `APPLY` rows of the `Select` set record |
| `Multiple_collection_navigation_with_FirstOrDefault_chained` | The statement has an `OUTER APPLY` whose scalar subqueries reference the outermost `Customers` row, and the server answers `不支持的语句类型`. Measured on 8.1.4.80: the statement with that scalar subquery replaced by a constant answered 8 rows, the same scalar subquery in a plain subquery answered 44 rows, while inside the `APPLY` it answered `不支持的语句类型` from both its select list and its derived table | Not a provider defect: the framework composes a chained navigation into an `APPLY` whose subqueries read the outermost row, which this server does not resolve |

`Case` names are methods of the test classes this project owns. `DM.EntityFrameworkCore.SpecificationTests.SkipLedgerTests`
fails when a row names a case the suite does not have.

## Cases that do not run

The four cases of the `Join` set and the four of the `GroupBy` set are skipped by the shipped base
itself (`#30677`, `#35028` and `#27130`), not by this project. Every other case of the nine bases ran
in the measurements above; the four capability answers in the `BuiltInDataTypesDmTest` fixture narrow
what the model contains rather than skipping a case.

`Swap_filtered_unique_index_values` does not run. The case swaps two values under a filtered unique
index, and this server has no filtered index: measured on 8.1.4.80, `CREATE INDEX ... WHERE` is rejected
with `语法分析出错`. The shared model's plain unique index on `(Name, Price)` cannot swap the two values
in one save, so the case is inapplicable as written (`Server limitation`). The provider refuses a model
that carries a filter, so a migration cannot silently create an index with different semantics.

`SupportsLargeStringComparisons`, `SupportsBinaryKeys`, `SupportsUnicodeToAnsiConversion` and
`PreservesDateTimeKind` are answered in the fixture rather than here, because they describe the server
rather than the suite. Each answer is a measured fact and the reason is beside the property.
## Migrations cases that do not run

The migrations suite (`MigrationsDmTest`, 134 cases) builds a model, generates the migration and applies it
to the live store. Measured on 2026-11-12 against DM 8.1.4.80: 114 passed, 20 skipped, and the cases below
either do not run or run without their assertion. The migrations store owns `DM_SPEC_MIGRATIONS` and the
named schemas below, so the rest of the shared database is untouched.

| Case | Cause | Fix |
|---|---|---|
| `Add_column_with_computedSql` | The case adds a virtual computed column and scaffolds it back. The server writes the virtual expression into the same catalog field as a default (`(A * 2)` and a default `(1 + 2)` are both `DATA_DEFAULT`), so a scaffolded model cannot name it as computed; a stored computed column is refused by the server (`GENERATED ALWAYS AS (...) STORED` answered `语法分析出错`) | Not a provider defect: the catalog does not distinguish a virtual column from a defaulted one. The DDL itself is verified by the cases that create the column |
| `Alter_column_make_computed` | The same catalog gap as `Add_column_with_computedSql`: the altered column cannot be read back as computed | Same |
| `Add_column_computed_with_collation` | The same catalog gap, plus the column collation gap below | Same |
| `Create_table_with_computed_column` | The `stored: true` row asserts the refusal and passes; the `false`/`null` rows cannot read the virtual expression back, for the reason above | Same |
| `Alter_column_change_computed` | The expression is changed in place; the scaffolded column still reads as a default | Same |
| `Alter_column_change_computed_type` | The type of a virtual column is changed instead of the expression; the scaffolded column still reads as a default | Same |
| `Alter_column_change_computed_recreates_indexes` | The column carries a function index, and the server refuses to modify it (`在要修改的列上已定义函数索引`) | Server limitation |
| `Add_column_with_collation` | The column is created with `COLLATE "BINARY"`, which the server accepts, but `ALL_TAB_COLUMNS` has no collation column, so a scaffolded model cannot name the collation | Server limitation: the catalog does not report a column collation |
| `Alter_column_set_collation` | The same catalog gap when the collation is altered | Same |
| `Move_sequence` | The case moves a sequence to another schema, and the server renames a sequence only inside its own schema: measured, `ALTER SEQUENCE ... RENAME TO schema.name` answered `语法分析出错`, while the same statement without a schema answered | Server limitation |
| `Move_table` | The case moves a table to another schema, and the server has no statement for it: measured, `RENAME TO schema.name`, `SET SCHEMA` and `MOVE SCHEMA` all answered `语法分析出错` | Server limitation |
| `Create_index_with_filter` | The case creates a filtered index, and the server has none: measured, `CREATE INDEX ... WHERE` answered `语法分析出错`. The provider refuses the operation with a message that names the measurement | Server limitation |
| `Create_unique_index_with_filter` | The same filtered index limitation | Same |
| `SqlOperation` | The case runs a migration whose SQL is only a comment (`-- I <3 DDL`), which the server rejects with `语法分析出错` | Server limitation |
| `Create_sequence_all_settings` | The case asserts the declared start value, and `ALL_SEQUENCES` has no start value: it reports `MIN_VALUE`, `MAX_VALUE`, `INCREMENT_BY`, `CYCLE_FLAG` and `LAST_NUMBER`, which is where the sequence is now. The factory deliberately leaves `StartValue` unset rather than scaffold a sequence that restarts on a used database | Server limitation: the catalog does not report the start value |
| `Alter_sequence_restart_with` | The case restarts a sequence, and the server has no statement for it: measured, `ALTER SEQUENCE ... RESTART` and `... RESTART WITH n` answered `未指定 ALTER SEQUENCE 的选项` | Server limitation |
| `Alter_sequence_all_settings` | The same restart limitation, together with the start value gap above | Server limitation |
| `Add_primary_key_int` | The case turns an existing column into an identity column, which the server refuses (`试图修改自增列`); the provider refuses the operation in place with a message that names the measurement | Server limitation |
| `Alter_column_make_required_with_null_data` | The table holds a null in the column that becomes `NOT NULL`, and the server rejects the alteration (`违反列 ... 非空约束`) rather than updating the row | Server limitation |
| `Convert_string_column_to_a_json_column_containing_reference` | The case alters a string column to the JSON type, and the server answers `数据类型的变更无效` | Server limitation |
| `Convert_string_column_to_a_json_column_containing_collection` | The same JSON alteration limitation | Same |
| `Convert_string_column_to_a_json_column_containing_required_reference` | The same JSON alteration limitation | Same |


