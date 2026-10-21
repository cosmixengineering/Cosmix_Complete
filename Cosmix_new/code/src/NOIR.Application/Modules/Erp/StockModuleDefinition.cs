namespace NOIR.Application.Modules.Erp;

/// <summary>
/// Stock Vouchers module - purchase indents and inter-location stock transfers
/// with an approval workflow.
/// </summary>
public sealed class StockModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Stock;
    public string DisplayNameKey => "modules.erp.stock";
    public string DescriptionKey => "modules.erp.stock.description";
    public string Icon => "ClipboardList";
    public int SortOrder => 500;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.StockSub.Indents, "modules.erp.stock.indents", "modules.erp.stock.indents.description"),
        new(ModuleNames.Erp.StockSub.Transfers, "modules.erp.stock.transfers", "modules.erp.stock.transfers.description"),
    ];
}
