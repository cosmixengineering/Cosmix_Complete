namespace NOIR.Application.Modules.Erp;

public sealed class HelpdeskModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Helpdesk;
    public string DisplayNameKey => "modules.erp.helpdesk";
    public string DescriptionKey => "modules.erp.helpdesk.description";
    public string Icon => "LifeBuoy";
    public int SortOrder => 339;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Helpdesk, "modules.erp.helpdesk", "modules.erp.helpdesk.description"),
    ];
}
