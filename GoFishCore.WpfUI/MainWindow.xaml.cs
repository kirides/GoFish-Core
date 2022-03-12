using GoFishCore.WpfUI.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Xml;

namespace GoFishCore.WpfUI;

public partial class MainWindow : Window
{
    private readonly MainViewModel vm;

    public MainWindow()
    {
        this.DataContext = this.vm = new MainViewModel();
        InitializeComponent();

        ConfigureTextEditor();
        PreviewKeyDown += MainWindowCancelOnEsc;
        Loaded += LoadConfig;
    }

    private void ConfigureTextEditor()
    {
        textEditor.TextArea.TextView.BackgroundRenderers.Add(new HighlightingBackgroundRenderer(vm));
        textEditor.Options.EnableHyperlinks = true;
        // Remove Rounded selection
        textEditor.TextArea.SelectionCornerRadius = 0;
        // Remove black stroke around selection
        textEditor.TextArea.SelectionBorder = null;

        var xshdPath = Path.Combine(Environment.CurrentDirectory, "VfpSyntax.xml");
        try
        {
            using var xrdr = XmlReader.Create(xshdPath);
            textEditor.SyntaxHighlighting = HighlightingLoader.Load(xrdr, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(ex, "IOError");
        }
    }

    private void LoadConfig(object sender, RoutedEventArgs e)
    {
        try
        {
            this.vm.LoadConfig();
        }
        catch (Exception ex) { MessageBox.Show($"Could not load configuration.{Environment.NewLine}{ex.Message}"); }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        try
        {
            this.vm.SaveConfig();
        }
        catch (Exception ex) { MessageBox.Show($"Could not save configuration.{Environment.NewLine}{ex.Message}"); }
        base.OnClosing(e);
    }

    private void MainWindowCancelOnEsc(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            this.vm.CancelSearch();
        }
    }

    private void BtnSearch_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            this.vm.ClearCache();
        }
    }

    private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
    {
        string filePath = "";
        if (CommonFileDialog.IsPlatformSupported)
        {
            using (var ofd = new CommonOpenFileDialog
                   {
                       IsFolderPicker = true,
                       EnsurePathExists = true,
                       EnsureFileExists = true
                   })
            {
                if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    filePath = ofd.FileName;
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            this.vm.DirectoryPath = filePath;
        }
    }

    private async void ListSearchResults_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        await App.UIContext;

        SearchModel searchModel = (this.listSearchResults.SelectedItem as SearchModel);
        if (searchModel is null)
        {
            vm.HighlightedLine = -1;
            this.textEditor.Document = new ICSharpCode.AvalonEdit.Document.TextDocument();
            return;
        }

        try
        {
            if (searchModel.Filepath != null)
            {
                this.textEditor.Document = new ICSharpCode.AvalonEdit.Document.TextDocument(System.IO.File.ReadAllText(searchModel.Content));
            }
            else if (searchModel.Content != null)
            {
                this.textEditor.Document.Text = searchModel.Content;
                vm.HighlightedLine = searchModel.Line - 1;
                this.textEditor.ScrollToLine(vm.HighlightedLine);
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
}