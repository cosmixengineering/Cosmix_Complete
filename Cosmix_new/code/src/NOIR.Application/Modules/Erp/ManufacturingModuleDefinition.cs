namespace NOIR.Application.Modules.Erp;

public sealed class ManufacturingModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Manufacturing;
    public string DisplayNameKey => "modules.erp.manufacturing";
    public string DescriptionKey => "modules.erp.manufacturing.description";
    public string Icon => "Factory";
    public int SortOrder => 320;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.ManufacturingSub.Boms, "modules.erp.manufacturing.boms", "modules.erp.manufacturing.boms.description"),
        new(ModuleNames.Erp.ManufacturingSub.ProductionOrders, "modules.erp.manufacturing.productionorders", "modules.erp.manufacturing.productionorders.description"),
    ];
}
