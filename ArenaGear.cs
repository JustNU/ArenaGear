using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;

namespace ArenaGear;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.justnu.arenagear";
    public string Name { get; init; } = "ArenaGear";
    public string Author { get; init; } = "JustNU";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new Range("^3.0.0") }
    };
    public string? Url { get; init; }
    public string? License { get; init; } = "Unlicense";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public class ArenaGearServer(
    WTTServerCommonLib.WTTServerCommonLib wttCommon) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        await wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly);
    }   
}
