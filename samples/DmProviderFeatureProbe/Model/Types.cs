using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DmProviderFeatureProbe;

/// <summary>The acceptance probe's enum, stored as its integer value by default.</summary>
internal enum Choice
{
    One = 1,
    Two = 2,
    Three = 3,
}

/// <summary>
/// Every mapped CLR type in its non-nullable form and its nullable twin. The twin carries the same
/// facets as the property it mirrors, so a null and a value exercise the same store type.
/// </summary>
internal sealed class AllTypes
{
    public int Id { get; set; }

    public int Int32 { get; set; }

    public int? Int32Null { get; set; }

    public long Int64 { get; set; }

    public long? Int64Null { get; set; }

    public short Int16 { get; set; }

    public short? Int16Null { get; set; }

    public byte Byte { get; set; }

    public byte? ByteNull { get; set; }

    public sbyte SByte { get; set; }

    public sbyte? SByteNull { get; set; }

    public decimal Decimal { get; set; }

    public decimal? DecimalNull { get; set; }

    public double Double { get; set; }

    public double? DoubleNull { get; set; }

    public float Single { get; set; }

    public float? SingleNull { get; set; }

    public bool Boolean { get; set; }

    public bool? BooleanNull { get; set; }

    public char Character { get; set; }

    public char? CharacterNull { get; set; }

    public string Text { get; set; } = string.Empty;

    public string? TextNull { get; set; }

    public string FixedText { get; set; } = string.Empty;

    public string? FixedTextNull { get; set; }

    public string LongText { get; set; } = string.Empty;

    public string? LongTextNull { get; set; }

    public byte[] Binary { get; set; } = [];

    public byte[]? BinaryNull { get; set; }

    public byte[] BigBinary { get; set; } = [];

    public byte[]? BigBinaryNull { get; set; }

    public DateTime Timestamp { get; set; }

    public DateTime? TimestampNull { get; set; }

    public DateTimeOffset Offset { get; set; }

    public DateTimeOffset? OffsetNull { get; set; }

    public DateOnly Day { get; set; }

    public DateOnly? DayNull { get; set; }

    public TimeOnly Time { get; set; }

    public TimeOnly? TimeNull { get; set; }

    public TimeSpan Duration { get; set; }

    public TimeSpan? DurationNull { get; set; }

    public Guid Key { get; set; }

    public Guid? KeyNull { get; set; }

    public Choice Choice { get; set; }

    public Choice? ChoiceNull { get; set; }
}

/// <summary>The key shapes the provider generates or accepts. One identity per table: the server allows one.</summary>
internal sealed class KeyShape
{
    public int Id { get; set; }

    public Guid GuidKey { get; set; }

    public string StringKey { get; set; } = string.Empty;
}

internal sealed class LongKeyRow
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class CompositeKeyRow
{
    public int PartA { get; set; }

    public string PartB { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;
}

internal sealed class HiLoRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>Table and column names come from data annotations, not from the fluent API.</summary>
[Table("DM_FEATURE_ANNOTATED")]
internal sealed class AnnotatedRecord
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(40)]
    [Column("caption")]
    public string Caption { get; set; } = string.Empty;

    [Column(TypeName = "DECIMAL(12,3)")]
    public decimal Amount { get; set; }
}

/// <summary>Names that are reserved words in the server, to prove identifier quoting.</summary>
internal sealed class QuotedRecord
{
    public int Id { get; set; }

    public string User { get; set; } = string.Empty;

    public string Order { get; set; } = string.Empty;
}

internal sealed class Shadowed
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Note
    {
        get => _note;
        private set => _note = value;
    }

    public ICollection<ShadowChild> Children { get; } = [];

    private string _note = string.Empty;
}

internal sealed class ShadowChild
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
