namespace Benjis_Shop_Toolbox.Models
{
    public class ExtensionProjectInfo
    {
        public ExtensionProjectInfo(string extensionName, string projectName, string csprojPath)
        {
            ExtensionName = extensionName;
            ProjectName = projectName;
            CsprojPath = csprojPath;
        }

        public ExtensionProjectInfo() : this(string.Empty, string.Empty, string.Empty)
        {
        }

        public string ExtensionName { get; set; }
        public string ProjectName { get; set; }
        public string CsprojPath { get; set; }
    }
}
