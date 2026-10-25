using SPTarkov.Server.Core.Models.Common;

namespace ExampleMod
{
    public class ExampleConfigType
    {
        // Names need to match the keys in the json exactly
        public double SomeDouble { get; set; }
        public int SomeInt { get; set; }
        public string SomeString { get; set; }
        public bool SomeBool { get; set; }
        public Dictionary<string, string> NestedDictionary { get; set; }
        public List<MongoId> ListOfIds { get; set; }
    }
}