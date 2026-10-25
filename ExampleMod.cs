using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using System.Reflection;

namespace ExampleMod
{
    // Must create a ModMetadata record
    public record ModMetadata : IModMetadata
    {
        public string ModGuid { get; init; } = "com.authorname.examplemod";    // Must be all lowercase
        public string Name { get; init; } = "Example Mod";    // Mod name
        public string Author { get; init; } = "RaiRaiTheRaichu";    // Author name
        public List<string>? Contributors { get; init; }    // Can leave this undefined
        public SemanticVersioning.Version Version { get; init; } = new("1.0.0");    // Mod version
        public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");  // Range of versions the mod is targeting
        public List<string>? Incompatibilities { get; init; }
        public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
        public string? Url { get; init; }
        public string? License { get; init; } = "whatever license";
        public bool HasPrepatcher { get; init; } = false;
    }

    // 
    [Injectable(TypePriority = OnLoadOrder.Preload + 1)]
    public class ExampleMod(
        TemplateTable templateTable,            // Request whatever data you need in the constructor
        ModHelper modHelper,                    // More info: https://wiki.sp-tushonka.com/en/modding/SPT_41_Modding/Server_40_to_41
        LocaleTable localeTable,                // Specifically, the header "Tables and configs are injectable"
        ISptLogger<ExampleMod> logger) : IOnLoad        // Logger type is your class name
    {
        // Creating "MyConfig" here so we can use it anywhere in the class after populating it
        private ExampleConfigType MyConfig = new ExampleConfigType();

        // Required method name/type, depends on the injected TypePriority defined above your class
        public Task OnLoadAsync(CancellationToken cancellationToken)
        {
            // Load example config
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            MyConfig = modHelper.GetJsonDataFromFile<ExampleConfigType>(
                System.IO.Path.Join(modPath, "config"), "config.jsonc");

            // Using data from config:
            logger.Info(MyConfig.SomeString);
            foreach (MongoId modConfigId in MyConfig.ListOfIds)
            {
                logger.Info($"Printing ID from MyConfig: {modConfigId}");
            }

            // Add this throughout the code, allowing the SPT server to shut down (with Control+C, for example) while your mod is loading
            // Good for loops that might take a long time so the server doesn't hang if someone closes the server
            cancellationToken.ThrowIfCancellationRequested();

            // Run your code either here or in methods
            // ex:
            //ExampleDatabase(cancellationToken);
            //ApplyLocaleChanges()

            // Mod loading complete 
            return Task.CompletedTask;
        }

        // Pass in the cancellationToken so it can be used in the function, too
        internal void ExampleDatabase(CancellationToken cancellationToken)
        {
            Dictionary<MongoId, TemplateItem> itemDatabase = templateTable.Items;

            // Can iterate over all items like such
            foreach (TemplateItem TemplateItem in itemDatabase.Values)
            {
                logger.Info($"Logging item: {TemplateItem.Name} with ID {TemplateItem.Id}");
                
                // Iterating over the entire item database takes time, so allowing it to be cancelled is a good idea
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Or access one directly like this
            itemDatabase["SOME_MONGOID"]?.Properties?.Weight = 1.0;
                // Made nullable with ?'s in case it does not exist, prevents server exploding
        }

        internal void ApplyLocaleChanges()
        {
            // can iterate over ALL lang keys with "foreach" and using localeTable.Global.Keys for the key
            if (localeTable.Global.TryGetValue("en", out var lazyloadedValue))
            {
                lazyloadedValue.AddTransformer(lazyloadedLocaleData =>
                {
                    lazyloadedLocaleData?[$"{"SOME_MONGOID"} Description"] = "new description lol";
                    return lazyloadedLocaleData;
                });
            }
        }        
    }
}
