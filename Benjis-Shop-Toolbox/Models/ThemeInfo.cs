namespace Benjis_Shop_Toolbox.Models
{
    public class ThemeInfo
    {
        public ThemeInfo(string name, string path, bool linkExists)
        {
            Name = name;
            Path = path;
            LinkExists = linkExists;
        }

        public string Name { get; set; }
        public string Path { get; set; }
        public bool LinkExists { get; set; }
    }
}
