namespace NOIR.Application.Modules.Erp;

public sealed class QaModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Qa;
    public string DisplayNameKey => "modules.erp.qa";
    public string DescriptionKey => "modules.erp.qa.description";
    public string Icon => "ClipboardCheck";
    public int SortOrder => 340;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Qa, "modules.erp.qa", "modules.erp.qa.description"),
    ];
}
