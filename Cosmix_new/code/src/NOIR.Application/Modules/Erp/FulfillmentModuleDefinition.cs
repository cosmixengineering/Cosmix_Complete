namespace NOIR.Application.Modules.Erp;

public sealed class FulfillmentModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Fulfillment;
    public string DisplayNameKey => "modules.erp.fulfillment";
    public string DescriptionKey => "modules.erp.fulfillment.description";
    public string Icon => "Truck";
    public int SortOrder => 333;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Fulfillment, "modules.erp.fulfillment", "modules.erp.fulfillment.description"),
    ];
}
