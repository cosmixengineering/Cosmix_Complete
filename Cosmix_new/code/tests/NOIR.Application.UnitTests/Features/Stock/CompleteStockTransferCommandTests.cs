namespace NOIR.Application.UnitTests.Features.Stock;

public class CompleteStockTransferCommandTests
{
    [Fact]
    public void Command_ShouldIdentifyTransferAndAuditActor()
    {
        var id = Guid.NewGuid();
        var command = new NOIR.Application.Features.Stock.Commands.CompleteStockTransfer.CompleteStockTransferCommand(id)
        {
            UserId = "actor-1"
        };

        IAuditableCommand<NOIR.Application.Features.Stock.DTOs.StockTransferDto> audit = command;
        command.UserId.ShouldBe("actor-1");
        audit.GetTargetId().ShouldBe(id);
        audit.OperationType.ShouldBe(AuditOperationType.Update);
        audit.GetActionDescription().ShouldBe("Completed stock transfer");
    }

    [Fact]
    public void Command_ShouldNotAcceptAuditActorFromJson()
    {
        var id = Guid.NewGuid();
        var command = JsonSerializer.Deserialize<NOIR.Application.Features.Stock.Commands.CompleteStockTransfer.CompleteStockTransferCommand>(
            $"{{\"Id\":\"{id}\",\"UserId\":\"untrusted-actor\"}}");

        command.ShouldNotBeNull();
        command.Id.ShouldBe(id);
        command.UserId.ShouldBeNull();
    }
}
