# Specification Suite Skip Ledger

Status: **living document**. Every case that does not run, or does not pass, is listed here with the
measurement that decided it. A row leaves this file when the suite passes the case, not when someone
intends to fix it.

The suite is not part of the offline gate: `BuiltInDataTypesDmTest` needs a running Dameng instance. The
store creates the tables of its own model and drops only those tables and sequences, so the database it
runs against keeps every object an application already has.

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
same instance on 2026-11-11: 362 passed, ten did not (five cases in their two forms), and the shipped
base skips none of them. The five are below, and they are two server limitations: a nested `APPLY` whose
inner query references the outermost table, and a row picked without an ordering. The run found three
defects that were fixed: a nested `Take` composed through `LEAST` left the function without a type
mapping when an argument was a parameter; a condition in a `CASE` result was written as a bare condition,
which the server rejects at the comparison; and a difference of two dates was translated although the
server answers it with a number of days, which cannot be read back as a time span, so it is now
projected as the date and subtracted by the client.

## Cases that do not pass

| Case | Cause | Fix |
|---|---|---|
| `Can_perform_query_with_max_length` | The case compares a `byte[9000]` with the equality operator, and this server cannot do both ends of that: mapped to `VARBINARY(9000)`, as the reference provider maps it, the value is refused with `记录超长`, because a row cannot hold what the type declares — measured on a one column table, 7500 bytes were stored and 8000 failed; mapped to a `BLOB`, which is what a byte array past that ceiling becomes, the statement is refused with `字符串过长`, because a large object compares with `DBMS_LOB.COMPARE` rather than with an operator | Not a provider defect: a large value must be a large object, and a large object has no equality operator. The case is inapplicable as written. The insert of the same row does pass now, since the oversized byte array is a `BLOB` rather than a column that cannot hold it |
| `SelectMany_with_selecting_outer_entity_column_and_inner_column` | The statement is the same `CROSS APPLY` with `ORDER BY "o"."OrderID" OFFSET 0 ROWS` inside and `ORDER BY "c"."CustomerID"` outside that the SQL Server test asserts, but the server answered the rows of one customer in a different order: the first row was `1998-04-09` where `1997-08-25` was expected, and the set of rows is the same. SQL does not order the rows inside a subquery, so the outer key is the whole of the ordering guarantee; the case asserts the full order with `assertOrder` | Not a provider defect: the framework's translation keeps the inner ordering and the server does not preserve it. The case is inapplicable as written for this server |
| `Collection_projection_selecting_outer_element_followed_by_take` | The statement the framework builds contains an `OUTER APPLY` whose subquery contains another `OUTER APPLY`, and the inner one references the outermost table (`"c0"."CustomerID"`). Dameng rejects that with `不支持的语句类型`. Measured on 8.1.4.80: a nested `OUTER APPLY` without that reference parsed and answered, the same nested `OUTER APPLY` referencing the outermost table answered `不支持的语句类型`, and a single level of `OUTER APPLY` answered | Not a provider defect: the shape comes from the framework's translation of a nested collection projection, and the correlation is what makes it unanswerable |
| `Select_nested_collection_deep` | The statement has the same nested `OUTER APPLY` whose inner query references the outermost table, which the server rejects with `不支持的语句类型`; the measurement is in the row above | Not a provider defect, same reason |
| `Select_nested_collection_deep_distinct_no_identifiers` | The statement has the same nested `OUTER APPLY` whose inner query references the outermost table, which the server rejects with `不支持的语句类型`; the measurement is in the row above | Not a provider defect, same reason |
| `Take_on_top_level_and_on_collection_projection_with_outer_apply` | The statement selects the first row of the orders whose customer starts with `F` with no ordering, so the row is whichever the server answers first; the case compares the fields of that row, and the server answered `10347` where the reference provider answered `10264`. The rows are the same set | Not a provider defect: a row limiting operation without an ordering has no defined row, which the framework logs as `RowLimitingOperationWithoutOrderByWarning` |
| `Project_keyless_entity_FirstOrDefault_without_orderby` | The query asks the first row of a keyless entity per company with no ordering, so the row is whichever the server answers first; it answered the other row of the group, and the rows are the same set | Not a provider defect, same reason |

`Case` names are methods of the test classes this project owns. `DM.EntityFrameworkCore.SpecificationTests.SkipLedgerTests`
fails when a row names a case the suite does not have.

## Cases that do not run

The four cases of the `Join` set are skipped by the shipped base itself (`#30677` and `#35028`), not by
this project. Every other case of the five bases ran in the measurements above; the four capability
answers in the `BuiltInDataTypesDmTest` fixture narrow what the model contains rather than skipping a
case.

`SupportsLargeStringComparisons`, `SupportsBinaryKeys`, `SupportsUnicodeToAnsiConversion` and
`PreservesDateTimeKind` are answered in the fixture rather than here, because they describe the server
rather than the suite. Each answer is a measured fact and the reason is beside the property.
