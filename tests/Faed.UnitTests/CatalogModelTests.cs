using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faed.UnitTests;

/// <summary>
/// Shape of the catalog EF model. Built offline (no connection is opened), so these run
/// everywhere and guard the structural rules from tasks/TASK-003-CATALOG.md and
/// docs/adr/0003-CONDITION-VS-DISCOUNT-REASON.md.
/// </summary>
public class CatalogModelTests
{
    private static ApplicationDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\ModelOnly;Database=ModelOnly")
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void ConditionGrade_And_DiscountReason_AreIndependent()
    {
        using var context = BuildContext();
        var grade = context.Model.FindEntityType(typeof(ConditionGrade))!;
        var reason = context.Model.FindEntityType(typeof(DiscountReason))!;

        Assert.Empty(grade.GetForeignKeys());
        Assert.Empty(reason.GetForeignKeys());
        Assert.DoesNotContain(grade.GetNavigations(), n => n.TargetEntityType.ClrType == typeof(DiscountReason));
        Assert.DoesNotContain(reason.GetNavigations(), n => n.TargetEntityType.ClrType == typeof(ConditionGrade));
    }

    [Fact]
    public void Category_IsSelfReferencing()
    {
        using var context = BuildContext();
        var category = context.Model.FindEntityType(typeof(Category))!;

        Assert.Contains(category.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Category));
    }

    [Theory]
    [InlineData(typeof(Category), nameof(Category.Slug))]
    [InlineData(typeof(Brand), nameof(Brand.Slug))]
    [InlineData(typeof(ConditionGrade), nameof(ConditionGrade.Code))]
    [InlineData(typeof(DiscountReason), nameof(DiscountReason.Code))]
    public void NaturalKey_HasUniqueIndex(Type entityType, string propertyName)
    {
        using var context = BuildContext();
        var entity = context.Model.FindEntityType(entityType)!;

        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique && index.Properties.Any(p => p.Name == propertyName));
    }
}
