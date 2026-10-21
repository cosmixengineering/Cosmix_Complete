namespace NOIR.Application.UnitTests.Features.Accounting;

public class AccountingMapperTests
{
    [Fact]
    public void EntryDto_ShouldMapLinesWithDefaultAccountSnapshots()
    {
        var entry = NOIR.Domain.Entities.Accounting.JournalEntry.Create("JE-001", new DateOnly(2026, 10, 12), null, null);
        var accountId = Guid.NewGuid();
        var line = NOIR.Domain.Entities.Accounting.JournalLine.Create(entry.Id, accountId, 100m, 0m, "Debit");
        entry.AddLine(line);

        var dto = NOIR.Application.Features.Accounting.AccountingMapper.ToEntryDto(entry);

        dto.Lines.Count.ShouldBe(1);
        dto.Lines[0].Id.ShouldBe(line.Id);
        dto.Lines[0].AccountId.ShouldBe(accountId);
        dto.Lines[0].AccountCode.ShouldBeNull();
        dto.Lines[0].AccountName.ShouldBeNull();
        dto.TotalDebit.ShouldBe(100m);
        dto.TotalCredit.ShouldBe(0m);
    }
}
