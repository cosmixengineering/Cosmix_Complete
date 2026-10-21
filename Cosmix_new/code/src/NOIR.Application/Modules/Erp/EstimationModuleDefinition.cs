namespace NOIR.Application.Modules.Erp;

public sealed class EstimationModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Estimation;
    public string DisplayNameKey => "modules.erp.estimation";
    public string DescriptionKey => "modules.erp.estimation.description";
    public string Icon => "Calculator";
    public int SortOrder => 320;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.EstimationSub.Boqs, "modules.erp.estimation.boqs", "modules.erp.estimation.boqs.description"),
    ];
}
