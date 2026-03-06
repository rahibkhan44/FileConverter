using FileConverter.Maui.ViewModels;

namespace FileConverter.Maui.Pages;

public partial class ConvertPage : ContentPage
{
    public ConvertPage(ConvertViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
