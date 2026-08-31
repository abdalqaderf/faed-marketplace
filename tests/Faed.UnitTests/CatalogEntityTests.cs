using Faed.Web.Models;
using Faed.Web.Models.Entities;

namespace Faed.UnitTests;

/// <summary>
/// Catalog entity invariants (tasks/TASK-003-CATALOG.md). Persistence-level rules
/// (unique slugs, seed idempotency) are covered by the integration tests.
/// </summary>
public class CatalogEntityTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Category_RequiresName(string name)
    {
        Assert.Throws<DomainException>(() => new Category(name, "some-slug", null, 0));
    }

    [Fact]
    public void Category_WithoutParent_IsRoot_AndGetsAnId()
    {
        var root = new Category("Fashion Overstock", "fashion-overstock", null, 0);

        Assert.True(root.IsRoot);
        Assert.NotEqual(Guid.Empty, root.Id);
        Assert.True(root.IsActive);
    }

    [Fact]
    public void Category_WithParent_IsNotRoot()
    {
        var root = new Category("Fashion Overstock", "fashion-overstock", null, 0);

        var child = new Category("Clothing", "clothing", root.Id, 1);

        Assert.False(child.IsRoot);
        Assert.Equal(root.Id, child.ParentCategoryId);
    }

    [Fact]
    public void ConditionGrade_RequiresCodeNameAndDescription()
    {
        Assert.Throws<DomainException>(() => new ConditionGrade(" ", "New", "desc", 1));
        Assert.Throws<DomainException>(() => new ConditionGrade("A", " ", "desc", 1));
        Assert.Throws<DomainException>(() => new ConditionGrade("A", "New", " ", 1));
    }

    [Fact]
    public void DiscountReason_RequiresCodeAndName_DescriptionIsOptional()
    {
        Assert.Throws<DomainException>(() => new DiscountReason(" ", "Overstock"));
        Assert.Throws<DomainException>(() => new DiscountReason("Overstock", " "));

        var reason = new DiscountReason("Overstock", "Overstock");
        Assert.Null(reason.Description);
        Assert.True(reason.IsActive);
    }

    [Fact]
    public void Brand_RequiresNameAndSlug()
    {
        Assert.Throws<DomainException>(() => new Brand(" ", "nike"));
        Assert.Throws<DomainException>(() => new Brand("Nike", " "));
    }
}
