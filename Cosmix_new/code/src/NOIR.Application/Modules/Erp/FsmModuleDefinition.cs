namespace NOIR.Application.Modules.Erp;

public sealed class FsmModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Fsm;
    public string DisplayNameKey => "modules.erp.fsm";
    public string DescriptionKey => "modules.erp.fsm.description";
    public string Icon => "Wrench";
    public int SortOrder => 330;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Fsm, "modules.erp.fsm", "modules.erp.fsm.description"),
    ];
}
