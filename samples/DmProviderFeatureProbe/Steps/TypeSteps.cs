using Microsoft.EntityFrameworkCore;

namespace DmProviderFeatureProbe;

/// <summary>
/// Value round trips. A column is written with a value .NET computes, read back, and compared with that
/// value; no assertion reads SQL text and no assertion depends on the client re-evaluating a predicate.
/// </summary>
internal static class TypeSteps
{
    public static IReadOnlyList<Step> All { get; } =
    [
        new("types: non-nullable round trip", "types", NonNullableAsync),
        new("types: nullable columns hold null", "types", NullAsync),
        new("types: nullable columns hold a value", "types", NullableValueAsync),
        new("types: string and byte length boundaries", "types", LengthsAsync),
        new("types: decimal precision and scale", "types", DecimalAsync),
        new("types: date and time precision", "types", DateTimeAsync),
    ];

    private static AllTypes Row() => new()
    {
        Int32 = 42,
        Int64 = 9_000_000_000L,
        Int16 = 1234,
        Byte = 200,
        SByte = -100,
        Decimal = 1234.56m,
        Double = Math.PI,
        Single = 1.5f,
        Boolean = true,
        Character = 'Z',
        Text = "variable text",
        FixedText = "fixed text",
        LongText = new string('L', 5000),
        Binary = [1, 2, 3, 4],
        BigBinary = Enumerable.Range(0, 300).Select(value => (byte)value).ToArray(),
        Timestamp = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified).AddTicks(1_234_560),
        Offset = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)).AddTicks(1_234_560),
        Day = new DateOnly(2024, 2, 29),
        Time = new TimeOnly(12, 34, 56, 789),
        Duration = new TimeSpan(1, 2, 3, 4, 5),
        Key = Guid.NewGuid(),
        Choice = Choice.Two,
    };

    private static async Task NonNullableAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var written = Row();
        context.Add(written);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.AllTypeRows.SingleAsync(cancellationToken);
        Assert.Equal(written.Int32, read.Int32, nameof(AllTypes.Int32));
        Assert.Equal(written.Int64, read.Int64, nameof(AllTypes.Int64));
        Assert.Equal(written.Int16, read.Int16, nameof(AllTypes.Int16));
        Assert.Equal(written.Byte, read.Byte, nameof(AllTypes.Byte));
        Assert.Equal(written.SByte, read.SByte, nameof(AllTypes.SByte));
        Assert.Equal(written.Decimal, read.Decimal, nameof(AllTypes.Decimal));
        Assert.Equal(written.Double, read.Double, nameof(AllTypes.Double));
        Assert.Equal(written.Single, read.Single, nameof(AllTypes.Single));
        Assert.Equal(written.Boolean, read.Boolean, nameof(AllTypes.Boolean));
        Assert.Equal(written.Character, read.Character, nameof(AllTypes.Character));
        Assert.Equal(written.Text, read.Text, nameof(AllTypes.Text));
        Assert.Equal(written.FixedText, read.FixedText.TrimEnd(), nameof(AllTypes.FixedText));
        Assert.Equal(written.LongText, read.LongText, nameof(AllTypes.LongText));
        Assert.SequenceEqual(written.Binary, read.Binary, nameof(AllTypes.Binary));
        Assert.SequenceEqual(written.BigBinary, read.BigBinary, nameof(AllTypes.BigBinary));
        Assert.Equal(written.Timestamp, read.Timestamp, nameof(AllTypes.Timestamp));
        Assert.True(written.Offset.EqualsExact(read.Offset), nameof(AllTypes.Offset) + ": the offset is preserved");
        Assert.Equal(written.Day, read.Day, nameof(AllTypes.Day));
        Assert.Equal(written.Time, read.Time, nameof(AllTypes.Time));
        Assert.Equal(written.Duration, read.Duration, nameof(AllTypes.Duration));
        Assert.Equal(written.Key, read.Key, nameof(AllTypes.Key));
        Assert.Equal(written.Choice, read.Choice, nameof(AllTypes.Choice));
    }

    private static async Task NullAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        context.Add(Row());
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.AllTypeRows.SingleAsync(cancellationToken);
        Assert.True(read.Int32Null is null, nameof(AllTypes.Int32Null));
        Assert.True(read.Int64Null is null, nameof(AllTypes.Int64Null));
        Assert.True(read.Int16Null is null, nameof(AllTypes.Int16Null));
        Assert.True(read.ByteNull is null, nameof(AllTypes.ByteNull));
        Assert.True(read.SByteNull is null, nameof(AllTypes.SByteNull));
        Assert.True(read.DecimalNull is null, nameof(AllTypes.DecimalNull));
        Assert.True(read.DoubleNull is null, nameof(AllTypes.DoubleNull));
        Assert.True(read.SingleNull is null, nameof(AllTypes.SingleNull));
        Assert.True(read.BooleanNull is null, nameof(AllTypes.BooleanNull));
        Assert.True(read.CharacterNull is null, nameof(AllTypes.CharacterNull));
        Assert.True(read.TextNull is null, nameof(AllTypes.TextNull));
        Assert.True(read.FixedTextNull is null, nameof(AllTypes.FixedTextNull));
        Assert.True(read.LongTextNull is null, nameof(AllTypes.LongTextNull));
        Assert.True(read.BinaryNull is null, nameof(AllTypes.BinaryNull));
        Assert.True(read.BigBinaryNull is null, nameof(AllTypes.BigBinaryNull));
        Assert.True(read.TimestampNull is null, nameof(AllTypes.TimestampNull));
        Assert.True(read.OffsetNull is null, nameof(AllTypes.OffsetNull));
        Assert.True(read.DayNull is null, nameof(AllTypes.DayNull));
        Assert.True(read.TimeNull is null, nameof(AllTypes.TimeNull));
        Assert.True(read.DurationNull is null, nameof(AllTypes.DurationNull));
        Assert.True(read.KeyNull is null, nameof(AllTypes.KeyNull));
        Assert.True(read.ChoiceNull is null, nameof(AllTypes.ChoiceNull));
    }

    private static async Task NullableValueAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var written = Row();
        written.Int32Null = 43;
        written.Int64Null = 9_000_000_001L;
        written.Int16Null = 1235;
        written.ByteNull = 201;
        written.SByteNull = -101;
        written.DecimalNull = 1234.57m;
        written.DoubleNull = Math.E;
        written.SingleNull = 2.5f;
        written.BooleanNull = false;
        written.CharacterNull = 'Y';
        written.TextNull = "optional text";
        written.FixedTextNull = "optional";
        written.LongTextNull = new string('O', 5000);
        written.BinaryNull = [5, 6, 7];
        written.BigBinaryNull = Enumerable.Range(1, 300).Select(value => (byte)value).ToArray();
        written.TimestampNull = new DateTime(2025, 6, 7, 8, 9, 10, DateTimeKind.Unspecified).AddTicks(6_543_210);
        written.OffsetNull = new DateTimeOffset(2025, 6, 7, 8, 9, 10, TimeSpan.FromHours(-5));
        written.DayNull = new DateOnly(2025, 6, 7);
        written.TimeNull = new TimeOnly(8, 9, 10, 654);
        written.DurationNull = new TimeSpan(2, 3, 4, 5, 6);
        written.KeyNull = Guid.NewGuid();
        written.ChoiceNull = Choice.Three;

        context.Add(written);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.AllTypeRows.SingleAsync(cancellationToken);
        Assert.Equal(written.Int32Null, read.Int32Null, nameof(AllTypes.Int32Null));
        Assert.Equal(written.Int64Null, read.Int64Null, nameof(AllTypes.Int64Null));
        Assert.Equal(written.Int16Null, read.Int16Null, nameof(AllTypes.Int16Null));
        Assert.Equal(written.ByteNull, read.ByteNull, nameof(AllTypes.ByteNull));
        Assert.Equal(written.SByteNull, read.SByteNull, nameof(AllTypes.SByteNull));
        Assert.Equal(written.DecimalNull, read.DecimalNull, nameof(AllTypes.DecimalNull));
        Assert.Equal(written.DoubleNull, read.DoubleNull, nameof(AllTypes.DoubleNull));
        Assert.Equal(written.SingleNull, read.SingleNull, nameof(AllTypes.SingleNull));
        Assert.Equal(written.BooleanNull, read.BooleanNull, nameof(AllTypes.BooleanNull));
        Assert.Equal(written.CharacterNull, read.CharacterNull, nameof(AllTypes.CharacterNull));
        Assert.Equal(written.TextNull, read.TextNull, nameof(AllTypes.TextNull));
        Assert.Equal(written.FixedTextNull, read.FixedTextNull!.TrimEnd(), nameof(AllTypes.FixedTextNull));
        Assert.Equal(written.LongTextNull, read.LongTextNull, nameof(AllTypes.LongTextNull));
        Assert.SequenceEqual(written.BinaryNull, read.BinaryNull, nameof(AllTypes.BinaryNull));
        Assert.SequenceEqual(written.BigBinaryNull, read.BigBinaryNull, nameof(AllTypes.BigBinaryNull));
        Assert.Equal(written.TimestampNull, read.TimestampNull, nameof(AllTypes.TimestampNull));
        Assert.True(written.OffsetNull!.Value.EqualsExact(read.OffsetNull!.Value),
            nameof(AllTypes.OffsetNull) + ": the offset is preserved");
        Assert.Equal(written.DayNull, read.DayNull, nameof(AllTypes.DayNull));
        Assert.Equal(written.TimeNull, read.TimeNull, nameof(AllTypes.TimeNull));
        Assert.Equal(written.DurationNull, read.DurationNull, nameof(AllTypes.DurationNull));
        Assert.Equal(written.KeyNull, read.KeyNull, nameof(AllTypes.KeyNull));
        Assert.Equal(written.ChoiceNull, read.ChoiceNull, nameof(AllTypes.ChoiceNull));
    }

    private static async Task LengthsAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var text = new string('漢', 100);
        var longText = new string('L', 5000);
        var binary = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
        var bigBinary = Enumerable.Range(0, 300).Select(value => (byte)value).ToArray();

        var written = Row();
        written.Text = text;
        written.FixedText = "abc";
        written.LongText = longText;
        written.Binary = binary;
        written.BigBinary = bigBinary;
        written.TextNull = "space";

        context.Add(written);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.AllTypeRows.SingleAsync(cancellationToken);
        Assert.Equal(100, read.Text.Length, "100 characters are 100 characters, not 100 bytes");
        Assert.Equal(text, read.Text, nameof(AllTypes.Text));
        Assert.Equal(20, read.FixedText.Length, "the fixed-length column is padded to its declaration");
        Assert.Equal("abc", read.FixedText.TrimEnd(), "the fixed-length value survives the padding");
        Assert.Equal(longText, read.LongText, "the large character object round-trips");
        Assert.SequenceEqual(binary, read.Binary, nameof(AllTypes.Binary));
        Assert.SequenceEqual(bigBinary, read.BigBinary, nameof(AllTypes.BigBinary));
        Assert.Equal("space", read.TextNull, "the nullable variable string round-trips");
    }

    private static async Task DecimalAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var property = context.Model.FindEntityType(typeof(AllTypes))!.FindProperty(nameof(AllTypes.Decimal))!;
        Assert.Equal(18, property.GetPrecision(), "the configured precision");
        Assert.Equal(2, property.GetScale(), "the configured scale");

        var written = Row();
        written.Decimal = 1234567890123456.78m;
        written.DecimalNull = 0.01m;

        context.Add(written);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.AllTypeRows.SingleAsync(cancellationToken);
        Assert.Equal(1234567890123456.78m, read.Decimal, "the full-width decimal round-trips exactly");
        Assert.Equal(0.01m, read.DecimalNull, "the nullable decimal round-trips exactly");
    }

    private static async Task DateTimeAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var written = Row();
        written.Timestamp = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Unspecified).AddTicks(1_234_560);
        written.TimestampNull = new DateTime(2025, 4, 5, 6, 7, 8, DateTimeKind.Unspecified).AddTicks(9_999_990);

        context.Add(written);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.AllTypeRows.SingleAsync(cancellationToken);
        Assert.Equal(written.Timestamp, read.Timestamp, "the microsecond of the timestamp survives");
        Assert.Equal(written.TimestampNull, read.TimestampNull, "the nullable timestamp keeps its microsecond");
    }
}
