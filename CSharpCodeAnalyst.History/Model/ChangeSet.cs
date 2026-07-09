using System.Text.Json.Serialization;

namespace CSharpCodeAnalyst.History.Model
{
    [Serializable]
    public sealed class ChangeSet
    {
        public string Comment { get; set; }
        public string Committer { get; set; }
        public DateTime Date { get; set; }
        public string Id { get; set; }

        // No setter by design (callers must not swap the list, only fill the existing one). System.Text.Json's
        // default ObjectCreationHandling is Replace, which silently skips read-only properties on deserialize -
        // Populate makes it fill the existing instance via Add() instead.
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public List<ChangeItem> Items { get; } = new List<ChangeItem>();
    }
}