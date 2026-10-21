namespace NOIR.Application.Modules.Erp;

public sealed class CrmModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Crm;
    public string DisplayNameKey => "modules.erp.crm";
    public string DescriptionKey => "modules.erp.crm.description";
    public string Icon => "Users";
    public int SortOrder => 300;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Crm + ".Contacts", "modules.erp.crm.contacts", "modules.erp.crm.contacts.description"),
        new(ModuleNames.Erp.Crm + ".Companies", "modules.erp.crm.companies", "modules.erp.crm.companies.description"),
        new(ModuleNames.Erp.Crm + ".Pipeline", "modules.erp.crm.pipeline", "modules.erp.crm.pipeline.description"),
    ];
}
