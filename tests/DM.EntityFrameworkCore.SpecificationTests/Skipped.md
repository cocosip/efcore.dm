# Specification Suite Skip Ledger

Status: **living document**. Every case that does not run, or does not pass, is listed here with the
measurement that decided it. A row leaves this file when the suite passes the case, not when someone
intends to fix it.

The suite is not part of the offline gate: `BuiltInDataTypesDmTest` needs a running Dameng instance. The
store creates the tables of its own model and drops only those tables and sequences, so the database it
runs against keeps every object an application already has.

Measured on 2026-11-11 against DM 8.1.4.80, `COMPATIBLE_MODE` 2: 31 cases, 28 passed, 3 failed. The
failures are below; the three are the whole of the gap this fixture set exposed.

## Cases that do not pass

| Case | Cause | Fix |
|---|---|---|
| `Can_perform_query_with_ansi_strings_test` | `VARCHAR2(n)` counts bytes, while the model's `HasMaxLength(n)` counts characters: a non-ASCII value of `n` characters overflows with `列[StringAnsi3]长度超出定义`. Measured: `VARCHAR2(3)` takes three ASCII characters and one Chinese character, `VARCHAR2(3 CHAR)` takes three Chinese characters | Emit the `CHAR` qualifier for an ANSI string and read it back: the catalog reports such a column as `DATA_LENGTH 2n` and `CHAR_LENGTH n`, so the model factory has to see the qualifier to keep a scaffolded model and a migration diff empty |
| `Can_perform_query_with_max_length` | The row of `MaxLengthDataTypes` fills its declared widths and exceeds the record size: `记录超长`. Measured on a table of one column: `VARBINARY(4000)` holding 4000 bytes fails, `NVARCHAR2(2000)` holding 2000 characters passes, `NVARCHAR2(4000)` holding 4000 characters fails | Not a provider defect: the declared types are legal, and a row that large cannot be stored. The case is inapplicable as written and stays listed until the fixture set is reviewed |
| `Can_insert_and_read_with_max_length_set` | Same record-size limit as the row above | Same |

`Case` names are methods of `BuiltInDataTypesDmTest`. `DM.EntityFrameworkCore.SpecificationTests.SkipLedgerTests`
fails when a row names a case the suite does not have.

## Cases that do not run

None. Every case of `BuiltInDataTypesDmTest` ran in the measurement above; the four capability answers in
the fixture narrow what the model contains rather than skipping a case.

`SupportsLargeStringComparisons`, `SupportsBinaryKeys`, `SupportsUnicodeToAnsiConversion` and
`PreservesDateTimeKind` are answered in the fixture rather than here, because they describe the server
rather than the suite. Each answer is a measured fact and the reason is beside the property.
