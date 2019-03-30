using GoFish.DataAccess;
using GoFish.DataAccess.VisualFoxPro;
using GoFish.DataAccess.VisualFoxPro.Search;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GoFish.WpfUI
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public SpeedObservableCollection<SearchModel> Models { get; set; } = new SpeedObservableCollection<SearchModel>();
        private static readonly Searcher searcher = new Searcher();

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            webBrowser.LoadCompleted += (s, e) =>
            {
                try
                { webBrowser.InvokeScript("eval", "var elements = document.getElementsByTagName('mark'); if (elements.length > 0) { elements[0].scrollIntoView(true); }"); }
                catch { /* Does not work with about:blank / non HTML pages */ }
            };
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(2000);
            Models.AddRange(Enumerable.Range(1, 50_000).Select(_ => new SearchModel
            {
                Class = "Class",
                Library = "Lib",
                Content = "My Content is very large and here\n<mark> is the marked line</mark>\nLul",
                Filepath = null,
                LineContent = "LineContent",
                Line = 2,
                Method = "Method"
            }));
        }

        private async void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.BeginInvoke((Action)(() => ButtonSearch_Click(sender, e)), System.Windows.Threading.DispatcherPriority.Normal);
                return;
            }
            var directoryPath = txtBrowse.Text;
            var text = txtSearch.Text;
            if (!string.IsNullOrWhiteSpace(directoryPath) && !string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    btnSearch.IsEnabled = false;
                    await Task.Run(() => SearchDirectory(directoryPath, text));
                    // SearchDirectory(directoryPath, text);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                btnSearch.IsEnabled = true;

            }
        }
        private static void ClearGC()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        private void SearchDirectory(string directoryPath, string text)
        {
            var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".vcx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".scx", StringComparison.OrdinalIgnoreCase));

            Models.SuspendCollectionChangeNotification();
            Models.Clear();
            foreach (var file in files)
            {
                var filename = Path.GetFileNameWithoutExtension(file);
                var fileExtension = Path.GetExtension(file);
                var memo = Path.Combine(Path.GetDirectoryName(file), filename + GetVfpMemoExtension(fileExtension));
                if (!File.Exists(memo))
                {
                    continue;
                }
                var dbf = new Dbf(file, memo, Encoding.GetEncoding(1252));
                var reader = new DbfReader(dbf, Encoding.GetEncoding(1252));
                var rows = reader.ReadRows((i, o) => (string)o[Constants.VCX.BODY] != "", includeMemo: true);
                var library = ClassLibrary.FromRows(filename, rows);
                rows = null;

                var results = searcher.SearchPlainText(library, text, ignoreCase: true);
                // var results = searcher.SearchRegex(library, text, ignoreCase: true);

                // searcher.SaveResults(results, tmpDir.FullName);
                Models.AddRange(results.Select(r => new SearchModel
                {
                    Library = r.Library + fileExtension,
                    Class = r.Class,
                    Method = r.Method,
                    Line = r.Line + 1,
                    Content = r.Content,
                    LineContent = r.Content.GetLine(r.Line).TrimStart(),
                    // Filepath = Path.Combine(tmpDir.FullName, r.Library, $"{r.Class}.{r.Method}.{r.Line}.html")
                }));
            }
            Models.ResumeCollectionChangeNotification();
            ClearGC();
            Models.RaiseCollectionChanged();
        }

        private string GetVfpMemoExtension(string extension)
        {
            switch (extension.ToUpperInvariant())
            {
                case ".VCX": return ".VCT";
                case ".SCX": return ".SCT";
                default: throw new NotSupportedException("This extension is not supported");
            }
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            string filePath = "";
            if (CommonFileDialog.IsPlatformSupported)
            {
                var ofd = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    EnsureFileExists = true
                };
                if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    filePath = ofd.FileName;
                }
            }
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                txtBrowse.Text = filePath;
            }
        }

        private void ListSearchResults_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() => ListSearchResults_SelectionChanged(sender, e)), System.Windows.Threading.DispatcherPriority.Normal);
                return;
            }
            var searchModel = (listSearchResults.SelectedItem as SearchModel);
            if (searchModel is null)
            {
                webBrowser.Visibility = Visibility.Collapsed;
                return;
            }
            else
            {
                webBrowser.Visibility = Visibility.Visible;
            }

            try
            {
                if (searchModel?.Filepath != null || searchModel?.Content != null)
                {
                    var content = searcher.HTMLifySurroundLines(searchModel.Content, searchModel.Line - 1, "<mark>", "</mark>");
                    webBrowser.NavigateToString(content);
                }
                else
                {
                    webBrowser.NavigateToString("<body>No item selected</body>");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}