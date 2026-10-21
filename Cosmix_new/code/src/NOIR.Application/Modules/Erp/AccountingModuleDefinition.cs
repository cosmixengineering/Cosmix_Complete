namespace NOIR.Application.Modules.Erp;

public sealed class AccountingModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Accounting;
    public string DisplayNameKey => "modules.erp.accounting";
    public string DescriptionKey => "modules.erp.accounting.description";
    public string Icon => "Calculator";
    public int SortOrder => 320;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.AccountingSub.Accounts, "modules.erp.accounting.accounts", "modules.erp.accounting.accounts.description"),
        new(ModuleNames.Erp.AccountingSub.JournalEntries, "modules.erp.accounting.journal", "modules.erp.accounting.journal.description"),
    ];
}
