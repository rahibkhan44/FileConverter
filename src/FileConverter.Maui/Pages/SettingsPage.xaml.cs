namespace FileConverter.Maui.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        ApiUrlEntry.Text = Preferences.Get("ApiBaseUrl", "https://localhost:7001");
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var url = ApiUrlEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            Preferences.Set("ApiBaseUrl", url);
            DisplayAlert("Saved", "API URL updated. Restart app to apply.", "OK");
        }
    }
}
