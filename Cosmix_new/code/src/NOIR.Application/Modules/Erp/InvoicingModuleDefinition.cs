namespace NOIR.Application.Modules.Erp;

public sealed class InvoicingModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Invoicing;
    public string DisplayNameKey => "modules.erp.invoicing";
    public string DescriptionKey => "modules.erp.invoicing.description";
    public string Icon => "Receipt";
    public int SortOrder => 334;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Invoicing, "modules.erp.invoicing", "modules.erp.invoicing.description"),
    ];
}
