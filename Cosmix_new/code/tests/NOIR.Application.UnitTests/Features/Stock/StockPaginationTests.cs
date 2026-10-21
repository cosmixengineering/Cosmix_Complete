namespace NOIR.Application.UnitTests.Features.Stock;

public class StockPaginationTests
{
    [Theory]
    [InlineData(0, 1, 0, 1)]
    [InlineData(1, 20, 0, 20)]
    [InlineData(2, 200, 1, 100)]
    public async Task Transfers_ShouldConvertPageNumberToIndex(int page, int size, int index, int normalizedSize)
    {
        var repository = new Mock<IReadRepository<NOIR.Domain.Entities.Stock.StockTransfer, Guid>>();
        repository.Setup(r => r.ListAsync(
                It.IsAny<NOIR.Application.Features.Stock.Specifications.StockTransfersByFilterSpec>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NOIR.Domain.Entities.Stock.StockTransfer>());
        repository.Setup(r => r.CountAsync(
                It.IsAny<NOIR.Application.Features.Stock.Specifications.StockTransfersByFilterSpec>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(250);

        var handler = new NOIR.Application.Features.Stock.Queries.GetStockTransfers.GetStockTransfersQueryHandler(repository.Object);
        var result = await handler.Handle(
            new NOIR.Application.Features.Stock.Queries.GetStockTransfers.GetStockTransfersQuery(Page: page, PageSize: size),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PageIndex.ShouldBe(index);
        result.Value.PageNumber.ShouldBe(index + 1);
        result.Value.PageSize.ShouldBe(normalizedSize);
        result.Value.TotalCount.ShouldBe(250);
    }

    [Theory]
    [InlineData(0, 1, 0, 1)]
    [InlineData(1, 20, 0, 20)]
    [InlineData(2, 200, 1, 100)]
    public async Task Indents_ShouldConvertPageNumberToIndex(int page, int size, int index, int normalizedSize)
    {
        var repository = new Mock<IReadRepository<NOIR.Domain.Entities.Stock.StockIndent, Guid>>();
        repository.Setup(r => r.ListAsync(
                It.IsAny<NOIR.Application.Features.Stock.Specifications.StockIndentsByFilterSpec>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NOIR.Domain.Entities.Stock.StockIndent>());
        repository.Setup(r => r.CountAsync(
                It.IsAny<NOIR.Application.Features.Stock.Specifications.StockIndentsByFilterSpec>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(250);

        var handler = new NOIR.Application.Features.Stock.Queries.GetStockIndents.GetStockIndentsQueryHandler(repository.Object);
        var result = await handler.Handle(
            new NOIR.Application.Features.Stock.Queries.GetStockIndents.GetStockIndentsQuery(Page: page, PageSize: size),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PageIndex.ShouldBe(index);
        result.Value.PageNumber.ShouldBe(index + 1);
        result.Value.PageSize.ShouldBe(normalizedSize);
        result.Value.TotalCount.ShouldBe(250);
    }
}
