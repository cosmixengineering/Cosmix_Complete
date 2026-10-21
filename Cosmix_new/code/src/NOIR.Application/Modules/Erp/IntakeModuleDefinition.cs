namespace NOIR.Application.Modules.Erp;

public sealed class IntakeModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.Intake;
    public string DisplayNameKey => "modules.erp.intake";
    public string DescriptionKey => "modules.erp.intake.description";
    public string Icon => "Inbox";
    public int SortOrder => 336;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.Intake, "modules.erp.intake", "modules.erp.intake.description"),
    ];
}
