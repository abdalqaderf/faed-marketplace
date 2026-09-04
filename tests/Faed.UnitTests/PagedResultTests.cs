using Faed.Web.Services.Common;

namespace Faed.UnitTests;

/// <summary>
/// The shared paging value type used by every list, queue and history surface
/// (tasks/TASK-011-HARDENING-AND-DEMO.md "performance/paging review").
/// </summary>
public sealed class PagedResultTests
{
    [Theory]
    [InlineData(0, 25, 1)]
    [InlineData(1, 25, 1)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    [InlineData(51, 25, 3)]
    public void TotalPages_IsAtLeastOne_AndRoundsUp(int totalCount, int pageSize, int expectedPages)
    {
        var result = new PagedResult<int>([], totalCount, 1, pageSize);
        Assert.Equal(expectedPages, result.TotalPages);
    }

    [Fact]
    public void ItemNumbers_DescribeTheWindow()
    {
        var page2 = new PagedResult<int>([26, 27, 28], TotalCount: 90, Page: 2, PageSize: 25);

        Assert.Equal(26, page2.FirstItemNumber);
        Assert.Equal(28, page2.LastItemNumber);
        Assert.True(page2.HasPreviousPage);
        Assert.True(page2.HasNextPage);
    }

    [Fact]
    public void EmptyResult_ReportsZeroItemNumbers_AndNoNeighbours()
    {
        var empty = PagedResult<string>.Empty(page: 3, pageSize: 25);

        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(1, empty.Page);
        Assert.Equal(0, empty.FirstItemNumber);
        Assert.Equal(0, empty.LastItemNumber);
        Assert.False(empty.HasPreviousPage);
        Assert.False(empty.HasNextPage);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    public void NormalizePage_ClampsToOne(int requested, int expected) =>
        Assert.Equal(expected, Paging.NormalizePage(requested));
}
