using System.Data;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Storage;

/// <summary>
/// T2 tests for the column a plain CLR type maps to and the value the provider binds for it. A mapping
/// that is wrong in either direction is silent: the statement runs and the value arrives as something
/// else.
/// </summary>
/// <remarks>
/// The parameter names and their order come from the statement the generator produced, so the assertions
/// follow the insert rather than the model.
/// </remarks>
public class DmParameterBindingTests
{
    private static readonly DateTimeOffset When = new(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(8));

    [Fact]
    public void PlainClrTypes_HaveTheirOwnColumn()
    {
        using var context = new SampleContext(new FakeDmConnection());

        Assert.Equal(
            Normalize(
                """
                CREATE TABLE "Samples" (
                    "Id" INT IDENTITY(1,1) NOT NULL,
                    "Letter" VARCHAR(1) NOT NULL,
                    "Grade" INT NOT NULL,
                    "NullableGrade" INT NULL,
                    "When" DATETIME WITH TIME ZONE NOT NULL,
                    "Span" INTERVAL DAY TO SECOND NOT NULL,
                    CONSTRAINT "PK_Samples" PRIMARY KEY ("Id")
                );
                /
                """),
            Normalize(context.Database.GenerateCreateScript()));
    }

    /// <summary>
    /// Each value is bound with the type the column has, so a reader can convert it back. An enum is
    /// bound as the integer it stores, a character as one character rather than as a string.
    /// </summary>
    [Fact]
    public void Insert_BindsTheValueWithTheTypeOfTheColumn()
    {
        var connection = new FakeDmConnection { RowsAffected = 1, ReadBackColumns = ["Id"], ReadBackRow = [5] };
        using var context = new SampleContext(connection);

        context.Add(
            new Sample
            {
                Letter = 'x',
                Grade = Grade.High,
                NullableGrade = Grade.Low,
                When = When,
                Span = TimeSpan.FromMinutes(90)
            });

        context.SaveChanges();

        var bound = Assert.Single(connection.ExecutionParameters, parameters => parameters.Count == 5);

        // An enum is bound as the integer it stores, and a character as a one character string, which
        // is what the column holds.
        var expected = new (string Name, object Value, DbType DbType)[]
        {
            (":p0", (object)1, DbType.Int32),
            (":p1", "x", DbType.AnsiString),
            (":p2", (object)0, DbType.Int32),
            (":p3", TimeSpan.FromMinutes(90), DbType.Time),
            (":p4", When, DbType.DateTimeOffset)
        };

        Assert.Equal(expected, bound);
    }

    private static string Normalize(string sql) => sql.ReplaceLineEndings("\n").TrimEnd();

    private enum Grade
    {
        Low = 0,
        High = 1
    }

    private sealed class SampleContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public SampleContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Sample> Samples => Set<Sample>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);
    }

    private sealed class Sample
    {
        public int Id { get; set; }

        public char Letter { get; set; }

        public Grade Grade { get; set; }

        public Grade? NullableGrade { get; set; }

        public DateTimeOffset When { get; set; }

        public TimeSpan Span { get; set; }
    }
}
