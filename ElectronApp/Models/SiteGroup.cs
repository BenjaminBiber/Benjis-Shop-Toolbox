using System.Collections.Generic;

namespace Benjis_Shop_Toolbox.Models
{
    /// <summary>
    /// Represents a group of IIS sites identified by their names.
    /// </summary>
    public class SiteGroup
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Sites { get; set; } = new();
    }
}
