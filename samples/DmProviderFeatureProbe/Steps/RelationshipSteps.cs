using Microsoft.EntityFrameworkCore;

namespace DmProviderFeatureProbe;

/// <summary>Every relationship shape, read back through the server rather than from the tracked graph.</summary>
internal static class RelationshipSteps
{
    public static IReadOnlyList<Step> All { get; } =
    [
        new("relationship: one-to-many", "relationship", OneToManyAsync),
        new("relationship: one-to-one shared key", "relationship", SharedKeyAsync),
        new("relationship: one-to-one nullable", "relationship", NullableOneToOneAsync),
        new("relationship: many-to-many implicit join", "relationship", ImplicitManyToManyAsync),
        new("relationship: many-to-many explicit join", "relationship", ExplicitManyToManyAsync),
        new("relationship: self-reference", "relationship", SelfReferenceAsync),
        new("relationship: two foreign keys to one target", "relationship", TwoForeignKeysAsync),
        new("relationship: delete behaviour", "relationship", DeleteBehaviourAsync),
        new("relationship: explicit loading", "relationship", ExplicitLoadingAsync),
    ];

    private static Order NewOrder(string number) => new()
    {
        Number = number,
        Shipping = new Shipping { Street = "1 probe street", City = "probe" },
    };

    private static async Task OneToManyAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var order = NewOrder("OM");
        order.Lines.Add(new OrderLine { Sku = "a", Price = 10m });
        order.Lines.Add(new OrderLine { Sku = "b", Price = 20m });
        context.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.Orders.Include(item => item.Lines).SingleAsync(cancellationToken);
        Assert.Equal(2, read.Lines.Count, "both lines load");

        var line = await context.OrderLines.SingleAsync(item => item.Sku == "a", cancellationToken);
        line.Price = 11m;
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        Assert.Equal(11m, (await context.OrderLines.SingleAsync(item => item.Sku == "a", cancellationToken)).Price,
            "a line update reaches the server");

        context.Remove(await context.OrderLines.SingleAsync(item => item.Sku == "b", cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.OrderLines.CountAsync(cancellationToken), "the deleted line is gone");
    }

    private static async Task SharedKeyAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var user = new User { Name = "shared" };
        context.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        var id = user.Id;
        context.ChangeTracker.Clear();

        context.Add(new Profile { Id = id, Bio = "shared bio" });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.Users.Include(item => item.Profile).SingleAsync(cancellationToken);
        Assert.NotNull(read.Profile, "the profile loads from the user");
        Assert.Equal("shared bio", read.Profile!.Bio, "the profile's values load");

        var reverse = await context.Profiles.Include(item => item.User).SingleAsync(cancellationToken);
        Assert.Equal("shared", reverse.User.Name, "the user loads from the shared key");
    }

    private static async Task NullableOneToOneAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var order = NewOrder("OI");
        context.Add(order);
        await context.SaveChangesAsync(cancellationToken);

        context.Add(new Invoice { Number = "INV", Order = order });
        await context.SaveChangesAsync(cancellationToken);

        context.Add(NewOrder("OI2"));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var withInvoice = await context.Orders.Include(item => item.Invoice)
            .SingleAsync(item => item.Number == "OI", cancellationToken);
        Assert.Equal("INV", withInvoice.Invoice!.Number, "the invoice loads from the order");

        var withoutInvoice = await context.Orders.Include(item => item.Invoice)
            .SingleAsync(item => item.Number == "OI2", cancellationToken);
        Assert.True(withoutInvoice.Invoice is null, "an order without an invoice loads null");
    }

    private static async Task ImplicitManyToManyAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var student = new Student { Name = "student" };
        student.Courses.Add(new Course { Title = "course" });
        context.Add(student);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.Students.Include(item => item.Courses).SingleAsync(cancellationToken);
        Assert.Equal("course", read.Courses.Single().Title, "the implicit join row loads the course");

        read.Courses.Clear();
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var emptied = await context.Students.Include(item => item.Courses).SingleAsync(cancellationToken);
        Assert.Equal(0, emptied.Courses.Count, "clearing the collection removes the join row");
    }

    private static async Task ExplicitManyToManyAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var project = new Project { Name = "project" };
        var member = new Member { Name = "member" };
        context.Add(new Membership
        {
            Project = project,
            Member = member,
            Role = "owner",
            JoinedAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
        });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.Memberships
            .Include(item => item.Project)
            .Include(item => item.Member)
            .SingleAsync(cancellationToken);
        Assert.Equal("owner", read.Role, "the join payload round-trips");
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5), read.JoinedAt, "the join timestamp round-trips");
        Assert.Equal("project", read.Project.Name, "the project loads from the join");
        Assert.Equal("member", read.Member.Name, "the member loads from the join");
    }

    private static async Task SelfReferenceAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var root = new Category { Name = "root" };
        var middle = new Category { Name = "middle", Parent = root };
        var leaf = new Category { Name = "leaf", Parent = middle };
        context.AddRange(root, middle, leaf);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.Categories
            .Include(item => item.Children)
            .ThenInclude(item => item.Children)
            .SingleAsync(item => item.Name == "root", cancellationToken);
        Assert.Equal("middle", read.Children.Single().Name, "the first level loads");
        Assert.Equal("leaf", read.Children.Single().Children.Single().Name, "the second level loads");

        var middleRow = await context.Categories.SingleAsync(item => item.Name == "middle", cancellationToken);
        Assert.Equal(read.Id, middleRow.ParentId, "the nullable self-reference is stored");
    }

    private static async Task TwoForeignKeysAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var creator = new User { Name = "creator" };
        var assignee = new User { Name = "assignee" };
        context.AddRange(creator, assignee);
        await context.SaveChangesAsync(cancellationToken);

        context.Add(new Ticket { Subject = "subject", CreatedBy = creator, AssignedTo = assignee });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var read = await context.Tickets
            .Include(item => item.CreatedBy)
            .Include(item => item.AssignedTo)
            .SingleAsync(cancellationToken);
        Assert.Equal("creator", read.CreatedBy.Name, "the first foreign key resolves");
        Assert.Equal("assignee", read.AssignedTo!.Name, "the second foreign key resolves independently");
    }

    private static async Task DeleteBehaviourAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var order = NewOrder("DC");
        order.Lines.Add(new OrderLine { Sku = "cascade", Price = 1m });
        context.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        await context.Orders.Where(item => item.Number == "DC").ExecuteDeleteAsync(cancellationToken);
        Assert.Equal(0, await context.OrderLines.CountAsync(cancellationToken),
            "the required foreign key cascades on the server");

        var user = new User { Name = "delete me" };
        context.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        var id = user.Id;
        context.ChangeTracker.Clear();

        context.Add(new Note { UserId = id, Text = "note" });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        // ClientSetNull is a client-side behaviour: the dependent has to be tracked for it to be nulled.
        await context.Notes.SingleAsync(cancellationToken);
        context.Remove(await context.Users.SingleAsync(item => item.Id == id, cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        Assert.True((await context.Notes.SingleAsync(cancellationToken)).UserId is null,
            "ClientSetNull nulls the optional foreign key on the client");
    }

    private static async Task ExplicitLoadingAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        var order = NewOrder("EL");
        order.Lines.Add(new OrderLine { Sku = "explicit", Price = 1m });
        context.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        var id = order.Id;
        context.ChangeTracker.Clear();

        var read = await context.Orders.SingleAsync(item => item.Id == id, cancellationToken);
        await context.Entry(read).Collection(item => item.Lines).LoadAsync(cancellationToken);
        Assert.Equal(1, read.Lines.Count, "explicit collection loading populates the navigation");

        context.Add(new Invoice { Number = "EL-INV", OrderId = id });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var again = await context.Orders.SingleAsync(item => item.Id == id, cancellationToken);
        await context.Entry(again).Reference(item => item.Invoice).LoadAsync(cancellationToken);
        Assert.NotNull(again.Invoice, "explicit reference loading populates the navigation");
    }
}
