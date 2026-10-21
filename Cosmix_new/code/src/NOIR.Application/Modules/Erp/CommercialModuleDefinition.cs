namespace NOIR.Application.Modules.Erp;

public sealed class CommercialModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Commercial;
    public string DisplayNameKey => "modules.erp.commercial";
    public string DescriptionKey => "modules.erp.commercial.description";
    public string Icon => "Briefcase";
    public int SortOrder => 338;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Commercial, "modules.erp.commercial", "modules.erp.commercial.description"),
    ];
}
