using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;

namespace McOnlineApp;
public sealed partial class TextViewerDialog : ContentDialog
{
    string FileName;
    public TextViewerDialog(string title, string name, bool widthMode)
    {
        InitializeComponent();
        this.Title = title;
        this.FileName = name;
        textBlock.Width = widthMode ? 3500 : 600;
    }

    private async void ContentDialog_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var uri = new Uri($"ms-appx:///TextResources/{FileName}");
        var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
        textBlock.Text = await FileIO.ReadTextAsync(file);
    }
}
