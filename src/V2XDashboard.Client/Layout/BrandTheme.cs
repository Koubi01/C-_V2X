using MudBlazor;

namespace V2XDashboard.Client.Layout;

public static class BrandTheme
{
    public static readonly MudTheme Value = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#006B6B",
            Secondary = "#D97706",
            Tertiary = "#0F4C81",
            Info = "#0EA5E9",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            Background = "#F4F7FB",
            Surface = "#FFFFFF",
            AppbarBackground = "#0F2D3D",
            AppbarText = "#F8FAFC",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#1F2937",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto", "sans-serif"]
            },
            H3 = new H3Typography
            {
                FontWeight = "700",
                LineHeight = "1.12"
            },
            H4 = new H4Typography
            {
                FontWeight = "700",
                LineHeight = "1.2"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px"
        }
    };
}