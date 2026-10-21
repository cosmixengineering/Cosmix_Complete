namespace NOIR.Application.Modules.Erp;

public sealed class ComplaintsModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Complaints;
    public string DisplayNameKey => "modules.erp.complaints";
    public string DescriptionKey => "modules.erp.complaints.description";
    public string Icon => "TriangleAlert";
    public int SortOrder => 331;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Complaints, "modules.erp.complaints", "modules.erp.complaints.description"),
    ];
}
