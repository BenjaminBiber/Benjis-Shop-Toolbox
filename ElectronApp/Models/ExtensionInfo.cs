namespace Benjis_Shop_Toolbox.Models
{
    public class ExtensionInfo
    {
        public ExtensionInfo(string name, string path, bool linkExists, string repo, string extensionFolder)
        {
            Name = name;
            Path = path;
            LinkExists = linkExists;
            Repo = repo;
            ExtensionFolder = extensionFolder;
        }

        public ExtensionInfo() : this(string.Empty, string.Empty, false, string.Empty, string.Empty)
        {
        }

        public string Name { get; set; }
        public string Path { get; set; }
        public bool LinkExists { get; set; }
        public string Repo { get; set; }
        public string ExtensionFolder { get; set; }
    }
}
