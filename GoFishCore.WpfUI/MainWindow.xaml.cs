using GoFish.DataAccess.VisualFoxPro.Search;
using GoFishCore.WpfUI.ViewModels;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.ComponentModel;
using System.Windows;

namespace GoFishCore.WpfUI
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel vm;
        public MainWindow()
        {
            this.DataContext = this.vm = new MainViewModel();
            InitializeComponent();
            this.webBrowser.LoadCompleted += (s, e) =>
            {
                try
                { this.webBrowser.InvokeScript("eval", @"var elements = document.getElementsByTagName('mark'); if (elements.length > 0) { elements[0].scrollIntoView(true); }"); }
                catch { /* Does not work with about:blank / non HTML pages */ }
            };
            PreviewKeyDown += MainWindowCancelOnEsc;
            Loaded += LoadConfig;
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

        private async void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.BeginInvoke((Action)(() => ButtonSearch_Click(sender, e)), System.Windows.Threading.DispatcherPriority.Normal);
                return;
            }
            string directoryPath = this.vm.DirectoryPath;
            string text = this.vm.SearchText;

            try
            {
                await this.vm.SearchAsync(directoryPath, text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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

        private void ListSearchResults_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() => ListSearchResults_SelectionChanged(sender, e)), System.Windows.Threading.DispatcherPriority.Normal);
                return;
            }
            SearchModel searchModel = (this.listSearchResults.SelectedItem as SearchModel);
            if (searchModel is null)
            {
                this.webBrowser.Visibility = Visibility.Collapsed;
                return;
            }
            else
            {
                this.webBrowser.Visibility = Visibility.Visible;
            }

            try
            {
                if (searchModel.Filepath != null)
                {
                    this.webBrowser.NavigateToString(searchModel.Content);
                }
                else if (searchModel.Content != null)
                {
                    string content = Searcher.HTMLifySurroundLines(searchModel.Content, searchModel.Line - 1, "<mark>", "</mark>");
                    this.webBrowser.NavigateToString(content);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
