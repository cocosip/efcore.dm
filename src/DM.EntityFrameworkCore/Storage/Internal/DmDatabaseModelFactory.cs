using System.Data.Common;
using System.Globalization;
using System.Text;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Reads a Dameng schema into the model the scaffolding tool builds a context from.
/// </summary>
/// <remarks>
/// The queries come from <see cref="DmCatalog" />. Their columns are read by position, and a view that
/// reports fewer columns than the query names yields nulls rather than failing, so a catalog that
/// differs from the expected shape still produces a model. The catalog queries themselves are not
/// verified against a server; the capability matrix records that.
/// </remarks>
internal sealed class DmDatabaseModelFactory : IDatabaseModelFactory
{
    public DatabaseModel Create(string connectionString, DatabaseModelFactoryOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentNullException.ThrowIfNull(options);

        using var connection = new Dm.DmConnection(connectionString);
        connection.Open();

        return Create(connection, options);
    }

    public DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        var model = new DatabaseModel();
        var tables = ReadTables(connection, model, options);

        ReadColumns(connection, tables);
        ReadKeys(connection, tables);
        ReadIndexes(connection, tables);
        ReadForeignKeys(connection, tables);
        ReadSequences(connection, model, options);

        return model;
    }

    private static List<DatabaseTable> ReadTables(
        DbConnection connection,
        DatabaseModel model,
        DatabaseModelFactoryOptions options)
    {
        using var command = Command(connection, DmCatalog.Tables);
        using var reader = command.ExecuteReader();
        var tables = new List<DatabaseTable>();

        foreach (var values in ReadRows(reader, 2))
        {
            var name = Text(values[1]);
            var schema = Text(values[0]);

            if (name is null || !Matches(options.Schemas, schema) || !Matches(options.Tables, name))
            {
                continue;
            }

            var table = new DatabaseTable { Name = name, Schema = schema, Database = model };
            model.Tables.Add(table);
            tables.Add(table);
        }

        return tables;
    }

    private static void ReadColumns(DbConnection connection, List<DatabaseTable> tables)
    {
        using var command = Command(connection, DmCatalog.Columns);
        using var reader = command.ExecuteReader();

        foreach (var values in ReadRows(reader, 10))
        {
            var owner = Text(values[0]);
            var objectName = Text(values[1]);
            var name = Text(values[2]);

            if (name is null || objectName is null)
            {
                continue;
            }

            var table = tables.FirstOrDefault(item => Same(item.Name, objectName) && Same(item.Schema, owner));

            if (table is null)
            {
                continue;
            }

            table.Columns.Add(
                new DatabaseColumn
                {
                    Name = name,
                    StoreType = StoreType(values[3], values[4], values[5], values[6]),
                    Table = table,
                    IsNullable = Text(values[7]) is not "N",
                    DefaultValueSql = Text(values[8]),
                    ValueGenerated = Text(values[9]) is "YES" or "Y" ? ValueGenerated.OnAdd : ValueGenerated.Never
                });
        }
    }

    private static void ReadKeys(DbConnection connection, List<DatabaseTable> tables)
    {
        var declared = new List<(string? Schema, string Table, string Name, bool IsPrimary)>();

        using (var command = Command(connection, DmCatalog.Constraints))
        using (var reader = command.ExecuteReader())
        {
            foreach (var values in ReadRows(reader, 4))
            {
                if (Text(values[2]) is not { } name || Text(values[1]) is not { } tableName)
                {
                    continue;
                }

                declared.Add((Text(values[0]), tableName, name, Text(values[3]) is "P"));
            }
        }

        var columns = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        using (var command = Command(connection, DmCatalog.ConstraintColumns))
        using (var reader = command.ExecuteReader())
        {
            foreach (var values in ReadRows(reader, 4))
            {
                if (Text(values[1]) is not { } constraintName || Text(values[2]) is not { } columnName)
                {
                    continue;
                }

                var key = Key(Text(values[0]), constraintName);

                if (!columns.TryGetValue(key, out var list))
                {
                    columns[key] = list = [];
                }

                list.Add(columnName);
            }
        }

        foreach (var (schema, table, name, isPrimary) in declared)
        {
            var target = tables.FirstOrDefault(item => Same(item.Name, table) && Same(item.Schema, schema));

            if (target is null || !columns.TryGetValue(Key(schema, name), out var names))
            {
                continue;
            }

            var mapped = Map(target, names);

            if (mapped is null)
            {
                continue;
            }

            if (isPrimary)
            {
                var key = new DatabasePrimaryKey { Name = name, Table = target };
                target.PrimaryKey = key;

                foreach (var column in mapped)
                {
                    key.Columns.Add(column);
                }
            }
            else
            {
                var constraint = new DatabaseUniqueConstraint { Name = name, Table = target };

                foreach (var column in mapped)
                {
                    constraint.Columns.Add(column);
                }

                target.UniqueConstraints.Add(constraint);
            }
        }
    }

    private static void ReadIndexes(DbConnection connection, List<DatabaseTable> tables)
    {
        var declared = new List<(string? Schema, string Table, string Name, bool IsUnique, string Column)>();

        using (var command = Command(connection, DmCatalog.Indexes))
        using (var reader = command.ExecuteReader())
        {
            foreach (var values in ReadRows(reader, 6))
            {
                if (Text(values[2]) is not { } name
                    || Text(values[1]) is not { } tableName
                    || Text(values[4]) is not { } column)
                {
                    continue;
                }

                declared.Add((Text(values[0]), tableName, name, Text(values[3]) is "UNIQUE", column));
            }
        }

        foreach (var group in declared.GroupBy(item => Key(item.Schema, item.Name), StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            var table = tables.FirstOrDefault(item => Same(item.Name, first.Table) && Same(item.Schema, first.Schema));

            if (table is null)
            {
                continue;
            }

            var mapped = Map(table, group.Select(item => item.Column).ToList());

            if (mapped is null)
            {
                continue;
            }

            var index = new DatabaseIndex { Name = first.Name, Table = table, IsUnique = first.IsUnique };

            foreach (var column in mapped)
            {
                index.Columns.Add(column);
            }

            table.Indexes.Add(index);
        }
    }

    private static void ReadForeignKeys(DbConnection connection, List<DatabaseTable> tables)
    {
        var declared = new List<(
            string? Schema,
            string Table,
            string Name,
            string Column,
            string? PrincipalSchema,
            string? PrincipalTable,
            string PrincipalColumn)>();

        using (var command = Command(connection, DmCatalog.ForeignKeys))
        using (var reader = command.ExecuteReader())
        {
            foreach (var values in ReadRows(reader, 8))
            {
                if (Text(values[2]) is not { } name
                    || Text(values[1]) is not { } tableName
                    || Text(values[3]) is not { } column
                    || Text(values[7]) is not { } principalColumn)
                {
                    continue;
                }

                declared.Add((Text(values[0]), tableName, name, column, Text(values[5]), Text(values[6]), principalColumn));
            }
        }

        foreach (var group in declared.GroupBy(item => Key(item.Schema, item.Name), StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            var table = tables.FirstOrDefault(item => Same(item.Name, first.Table) && Same(item.Schema, first.Schema));
            var principal = tables.FirstOrDefault(
                item => Same(item.Name, first.PrincipalTable) && Same(item.Schema, first.PrincipalSchema));

            if (table is null || principal is null)
            {
                continue;
            }

            var dependent = Map(table, group.Select(item => item.Column).ToList());
            var referenced = Map(principal, group.Select(item => item.PrincipalColumn).ToList());

            if (dependent is null || referenced is null)
            {
                continue;
            }

            var foreignKey = new DatabaseForeignKey { Name = first.Name, Table = table, PrincipalTable = principal };

            foreach (var column in dependent)
            {
                foreignKey.Columns.Add(column);
            }

            foreach (var column in referenced)
            {
                foreignKey.PrincipalColumns.Add(column);
            }

            table.ForeignKeys.Add(foreignKey);
        }
    }

    private static void ReadSequences(
        DbConnection connection,
        DatabaseModel model,
        DatabaseModelFactoryOptions options)
    {
        using var command = Command(connection, DmCatalog.Sequences);
        using var reader = command.ExecuteReader();

        foreach (var values in ReadRows(reader, 9))
        {
            var name = Text(values[1]);
            var schema = Text(values[0]);

            if (name is null || !Matches(options.Schemas, schema))
            {
                continue;
            }

            model.Sequences.Add(
                new DatabaseSequence
                {
                    Name = name,
                    Schema = schema,
                    Database = model,
                    MinValue = Whole(values[2]),
                    MaxValue = Whole(values[3]),
                    IncrementBy = (int?)Whole(values[4]),
                    IsCyclic = Text(values[5]) is "Y"

                    // StartValue stays unset: LAST_NUMBER is where the sequence is now, not where it
                    // started, and using it would scaffold a sequence that restarts on a used database.
                });
        }
    }

    private static DbCommand Command(DbConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;

        return command;
    }

    private static IEnumerable<object?[]> ReadRows(DbDataReader reader, int width)
    {
        while (reader.Read())
        {
            var values = new object?[width];

            for (var i = 0; i < width; i++)
            {
                values[i] = i < reader.FieldCount && !reader.IsDBNull(i) ? reader.GetValue(i) : null;
            }

            yield return values;
        }
    }

    /// <summary>
    /// Maps column names onto the columns the table has, or nothing when one of them is missing, so a
    /// constraint that refers to an unread column is skipped instead of producing an incomplete key.
    /// </summary>
    private static List<DatabaseColumn>? Map(DatabaseTable table, List<string> names)
    {
        var mapped = new List<DatabaseColumn>(names.Count);

        foreach (var name in names)
        {
            var column = table.Columns.FirstOrDefault(item => Same(item.Name, name));

            if (column is null)
            {
                return null;
            }

            mapped.Add(column);
        }

        return mapped;
    }

    private static string? Text(object? value)
        => value switch
        {
            null or DBNull => null,
            string text => text.Trim(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim()
        };

    private static long? Whole(object? value)
        => value switch
        {
            null or DBNull => null,
            IConvertible convertible => convertible.ToInt64(CultureInfo.InvariantCulture),
            _ => null
        };

    private static decimal? Number(object? value)
        => value switch
        {
            null or DBNull => null,
            IConvertible convertible => convertible.ToDecimal(CultureInfo.InvariantCulture),
            _ => null
        };

    private static bool Same(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(IEnumerable<string>? filter, string? value)
        => filter is null || !filter.Any() || filter.Any(item => Same(item, value));

    private static string Key(string? schema, string name) => $"{schema ?? string.Empty}.{name}";

    /// <summary>
    /// Composes the store type text. A length belongs to the character and binary types only, and a
    /// precision to the numeric ones: an integer column reports a byte width that is not part of its
    /// type name.
    /// </summary>
    private static string StoreType(object? type, object? length, object? precision, object? scale)
    {
        var name = Text(type) ?? DmStoreTypes.Text;

        if (name.Contains('('))
        {
            return name;
        }

        var builder = new StringBuilder(name);
        var precisionValue = Number(precision);
        var scaleValue = Number(scale);
        var lengthValue = Number(length);

        if (IsNumeric(name) && precisionValue is > 0)
        {
            builder.Append('(').Append(Literal(precisionValue.Value));

            if (scaleValue is > 0)
            {
                builder.Append(',').Append(Literal(scaleValue.Value));
            }

            builder.Append(')');
        }
        else if (HasLength(name) && lengthValue is > 0)
        {
            builder.Append('(').Append(Literal(lengthValue.Value)).Append(')');
        }
        else if (name.StartsWith(DmStoreTypes.Timestamp, StringComparison.OrdinalIgnoreCase) && scaleValue is > 0)
        {
            builder.Append('(').Append(Literal(scaleValue.Value)).Append(')');
        }

        return builder.ToString();
    }

    private static bool IsNumeric(string name)
        => name is DmStoreTypes.Decimal or "NUMBER";

    private static bool HasLength(string name)
        => name is DmStoreTypes.Char
            or DmStoreTypes.NChar
            or DmStoreTypes.VarChar
            or DmStoreTypes.VarChar2
            or DmStoreTypes.NVarChar
            or DmStoreTypes.NVarChar2
            or DmStoreTypes.Binary
            or DmStoreTypes.VarBinary
            or "RAW";

    private static string Literal(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}