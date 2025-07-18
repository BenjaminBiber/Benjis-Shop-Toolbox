using MudBlazor;

namespace Benjis_Shop_Toolbox.Themes
{
    public static class ModernTheme
    {
        public static MudTheme Theme { get; } = new MudTheme
        {
            Palette = new Palette
            {
                Primary = Colors.BlueGrey.Darken2,
                Secondary = Colors.DeepPurple.Accent2,
                AppbarBackground = Colors.BlueGrey.Darken4,
                AppbarText = Colors.Shades.White,
                Background = Colors.Grey.Lighten5,
                DrawerBackground = Colors.BlueGrey.Darken4,
                DrawerText = Colors.Shades.White,
                Surface = "#FFFFFF"
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "6px"
            }
        };
    }
}
