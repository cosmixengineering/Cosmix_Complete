namespace NOIR.Application.Modules.Erp;

public sealed class ProcurementModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Procurement;
    public string DisplayNameKey => "modules.erp.procurement";
    public string DescriptionKey => "modules.erp.procurement.description";
    public string Icon => "Handshake";
    public int SortOrder => 332;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Procurement, "modules.erp.procurement", "modules.erp.procurement.description"),
    ];
}
