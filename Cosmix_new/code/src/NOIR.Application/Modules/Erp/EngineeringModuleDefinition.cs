namespace NOIR.Application.Modules.Erp;

public sealed class EngineeringModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Engineering;
    public string DisplayNameKey => "modules.erp.engineering";
    public string DescriptionKey => "modules.erp.engineering.description";
    public string Icon => "DraftingCompass";
    public int SortOrder => 337;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Engineering, "modules.erp.engineering", "modules.erp.engineering.description"),
    ];
}
