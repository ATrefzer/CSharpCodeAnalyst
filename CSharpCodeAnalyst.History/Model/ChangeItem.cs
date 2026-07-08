namespace CSharpCodeAnalyst.History.Model
{
    [Serializable]
    [Flags]
    public enum KindOfChange // Cloned from ChangeType
    {
        Add = 2,
        Branch = 128,
        Delete = 32,
        Edit = 4,
        Merge = 256,
        None = 1,
        Rename = 16,

        // Added for git file history
        Copy = 8,
        TypeChanged = 512
    }

    [Serializable]
    public sealed class ChangeItem
    {
        public string Id { get; set; }

        public KindOfChange Kind { get; set; }
        public string LocalPath { get; set; }

        /// <summary>
        ///     Name on server. Later mapped to a local file.
        /// </summary>
        public string ServerPath { get; set; }

        public string FromServerPath { get; set; }

        public bool Exists()
        {
            return File.Exists(LocalPath);
        }

        public bool IsAdd()
        {
            return (Kind & KindOfChange.Add) == KindOfChange.Add;
        }

        public bool IsDelete()
        {
            var isDelete = (Kind & KindOfChange.Delete) == KindOfChange.Delete;
            return isDelete;
        }

        public bool IsEdit()
        {
            return (Kind & KindOfChange.Edit) == KindOfChange.Edit;
        }

        public bool IsMerge()
        {
            return (Kind & KindOfChange.Merge) == KindOfChange.Merge;
        }

        public bool IsRename()
        {
            return (Kind & KindOfChange.Rename) == KindOfChange.Rename;
        }

        public bool IsCopy()
        {
            return (Kind & KindOfChange.Copy) == KindOfChange.Copy;
        }

        public bool IsTypeChange()
        {
            return (Kind & KindOfChange.TypeChanged) == KindOfChange.TypeChanged;
        }
    }
}