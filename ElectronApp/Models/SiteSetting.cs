using System.Collections.Generic;

namespace Benjis_Shop_Toolbox.Models
{
    /// <summary>
    /// Stores additional configuration for an IIS site.
    /// </summary>
    public class SiteSetting
    {
        public string Name { get; set; } = string.Empty;
        public bool IsShop { get; set; }
        public string? ShopYamlPath { get; set; }
    }
}
