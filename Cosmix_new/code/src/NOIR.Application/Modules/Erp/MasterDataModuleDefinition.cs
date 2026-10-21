namespace NOIR.Application.Modules.Erp;

public sealed class MasterDataModuleDefinition : IModuleDefinition, ISingletonService
{
    public string Name => ModuleNames.Erp.MasterData;
    public string DisplayNameKey => "modules.erp.masterdata";
    public string DescriptionKey => "modules.erp.masterdata.description";
    public string Icon => "Table";
    public int SortOrder => 335;
    public bool IsCore => false;
    public bool DefaultEnabled => true;
    public IReadOnlyList<FeatureDefinition> Features =>
    [
        new(ModuleNames.Erp.MasterData, "modules.erp.masterdata", "modules.erp.masterdata.description"),
    ];
}
