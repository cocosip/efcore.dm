namespace DmProviderFeatureProbe;

internal sealed class User
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Profile? Profile { get; set; }
}

/// <summary>One-to-one with a shared primary key: the profile's key is the user's key.</summary>
internal sealed class Profile
{
    public int Id { get; set; }

    public string Bio { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}

internal sealed class Order
{
    public int Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public ICollection<OrderLine> Lines { get; } = [];

    public Invoice? Invoice { get; set; }

    /// <summary>Owned and stored as one JSON column.</summary>
    public Shipping Shipping { get; set; } = new();
}

internal sealed class OrderLine
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }
}

/// <summary>One-to-one with a nullable foreign key and a unique index.</summary>
internal sealed class Invoice
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public string Number { get; set; } = string.Empty;
}

internal sealed class Student
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Implicit many-to-many join table.</summary>
    public ICollection<Course> Courses { get; } = [];
}

internal sealed class Course
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public ICollection<Student> Students { get; } = [];
}

internal sealed class Project
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Membership> Memberships { get; } = [];
}

internal sealed class Member
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Membership> Memberships { get; } = [];
}

/// <summary>Explicit join entity, so the relationship carries a payload.</summary>
internal sealed class Membership
{
    public int ProjectId { get; set; }

    public int MemberId { get; set; }

    public Project Project { get; set; } = null!;

    public Member Member { get; set; } = null!;

    public string Role { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; }
}

internal sealed class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public Category? Parent { get; set; }

    public ICollection<Category> Children { get; } = [];
}

/// <summary>Two foreign keys to the same target, with different delete behaviour.</summary>
internal sealed class Ticket
{
    public int Id { get; set; }

    public string Subject { get; set; } = string.Empty;

    public int CreatedById { get; set; }

    public User CreatedBy { get; set; } = null!;

    public int? AssignedToId { get; set; }

    public User? AssignedTo { get; set; }
}

/// <summary>
/// A row whose version column is the only concurrency token. It has no computed or generated column,
/// so an update needs no result set and the provider's affected-row check runs.
/// </summary>
internal sealed class Versioned
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Version { get; set; }
}

/// <summary>Defaults, a computed column, a concurrency token and a query filter.</summary>
internal sealed class Note
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public User? User { get; set; }

    public string Text { get; set; } = string.Empty;

    public int Score { get; set; }

    public int Double { get; set; }

    public int Version { get; set; }

    public bool IsDeleted { get; set; }
}
