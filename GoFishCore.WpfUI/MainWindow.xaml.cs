using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GoFish.DataAccess;
using GoFish.DataAccess.VisualFoxPro;
using GoFish.DataAccess.VisualFoxPro.Search;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace GoFishCore.WpfUI
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public SpeedObservableCollection<SearchModel> Models { get; set; } = new SpeedObservableCollection<SearchModel>();
        private static readonly Searcher searcher = new Searcher(new PlainTextAlgorithm());
        private List<(string extension, ClassLibrary lib)> VfpLibCache = null;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName]string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName]string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private string statusText;
        public string StatusText { get => statusText; set => SetProperty(ref statusText, value); }

        private int statusTotal = 100;
        public int StatusTotal { get => statusTotal; set => SetProperty(ref statusTotal, value); }

        private int statusCurrent;
        public int StatusCurrent { get => statusCurrent; set => SetProperty(ref statusCurrent, value); }

        private bool progressUnknown;
        public bool ProgressUnknown { get => progressUnknown; set => SetProperty(ref progressUnknown, value); }

        private bool caseSensitive;
        public bool CaseSensitive { get => caseSensitive; set => SetProperty(ref caseSensitive, value); }

        private bool canSearch = true;
        public bool CanSearch
        {
            get { return canSearch; }
            set { SetProperty(ref canSearch, value); }
        }

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            webBrowser.LoadCompleted += (s, e) =>
            {
                try
                { webBrowser.InvokeScript("eval", @"var elements = document.getElementsByTagName('mark'); if (elements.length > 0) { elements[0].scrollIntoView(true); }"); }
                catch { /* Does not work with about:blank / non HTML pages */ }
            };
            PreviewKeyDown += MainWindowCancelOnEsc;
        }

        private void MainWindowCancelOnEsc(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                searchCancellation?.Cancel();
            }
        }

        private CancellationTokenSource searchCancellation;
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
                    searchCancellation = new CancellationTokenSource();
                    CanSearch = false;
                    await Task.Run(() => SearchDirectory(directoryPath, text, searchCancellation.Token)).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                CanSearch = true;
            }
        }
        private void BtnSearch_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                VfpLibCache?.Clear();
                VfpLibCache = null;
            }
        }
        private void SearchDirectory(string directoryPath, string text, CancellationToken cancellationToken = default)
        {
            Models.SuspendCollectionChangeNotification();
            Models.Clear();
            if (VfpLibCache != null)
            {
                int currentCachedFile = 0;
                int cachedFilesCount = VfpLibCache.Count;
                StatusTotal = cachedFilesCount;
                Parallel.ForEach(VfpLibCache, (entry, state) =>
                {
                    if (cancellationToken.IsCancellationRequested) state.Stop();
                    StatusText = $"[Cache] Processing File {Interlocked.Increment(ref currentCachedFile)} of {cachedFilesCount} (Cancel using ESC)";
                    Interlocked.Exchange(ref statusCurrent, currentCachedFile);
                    RaisePropertyChanged(nameof(StatusCurrent));
                    var results = searcher.Search(entry.lib, text, ignoreCase: !CaseSensitive);
                    lock (Models)
                    {
                        Models.AddRange(results.Select(r => new SearchModel
                        {
                            Library = r.Library + entry.extension,
                            Class = r.Class,
                            Method = r.Method,
                            Line = r.Line + 1,
                            Content = r.Content,
                            LineContent = r.Content.GetLine(r.Line).Trim(),
                        }));
                    }
                });
                StatusText = "";
                StatusCurrent = 0;
                StatusTotal = 100;
                Models.ResumeCollectionChangeNotification();
                Models.RaiseCollectionChanged();
                return;
            }

            //var tempDir = Path.Combine(Path.GetTempPath(), "gofish-core");
            //try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            //catch { /* Nom nom nom */ }
            //var tmpDir = Directory.CreateDirectory(tempDir);

            var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".vcx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".scx", StringComparison.OrdinalIgnoreCase));

            var fileCount = files.Count();
            StatusTotal = fileCount;
            var currentFile = 0;
            var cache = new System.Collections.Concurrent.BlockingCollection<(string, ClassLibrary)>(fileCount);
            var errors = new System.Collections.Concurrent.BlockingCollection<string>();
            Parallel.ForEach(files, (file, state) =>
            {
                if (cancellationToken.IsCancellationRequested) state.Stop();
                StatusText = $"Processing File {Interlocked.Increment(ref currentFile)} of {fileCount} (Cancel using ESC)";
                Interlocked.Exchange(ref statusCurrent, currentFile);
                RaisePropertyChanged(nameof(StatusCurrent));
                var filename = Path.GetFileNameWithoutExtension(file);
                var fileExtension = Path.GetExtension(file);
                var memo = Path.Combine(Path.GetDirectoryName(file), filename + GetExtension(fileExtension));
                if (!File.Exists(memo))
                {
                    return;
                }
                ClassLibrary library;
                try
                {
                    var dbf = new Dbf(file, memo, System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252));
                    var reader = new DbfReader(dbf, System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252));
                    var rows = reader.ReadRows((i, o) => (string)o[Constants.VCX.BODY] != "", includeMemo: true);
                    library = ClassLibrary.FromRows(filename, rows);
                    library.Classes.ForEach(x =>
                    {
                        x.BaseClass = null;
                        foreach (var m in x.Methods)
                        {
                            m.Parameters.Clear();
                        }
                    });
                    cache.Add((fileExtension, library));
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    return;
                }

                var results = searcher.Search(library, text, ignoreCase: !CaseSensitive);

                lock (Models)
                {
                    //searcher.SaveResults(results, r => $"{r.Class}.{ r.Method}.{r.Line}", tmpDir.FullName);
                    Models.AddRange(results.Select(r => new SearchModel
                    {
                        Library = r.Library + fileExtension,
                        Class = r.Class,
                        Method = r.Method,
                        Line = r.Line + 1,
                        Content = r.Content,
                        LineContent = r.Content.GetLine(r.Line).Trim(),
                        //Filepath = Path.Combine(tmpDir.FullName, r.Library, $"{r.Class}.{r.Method}.{r.Line}.html")
                    }));
                }
            });
            if (errors.Count > 0)
            {
                MessageBox.Show(
                        "Fehler beim Dursuchen der folgenden Dateien:" +
                            Environment.NewLine +
                            Environment.NewLine +
                            string.Join($"{Environment.NewLine}- ", errors),
                        "Fehler beim Durchsuchen");
            }
            VfpLibCache = cache.ToList();
            StatusText = "";
            StatusCurrent = 0;
            StatusTotal = 100;
            Models.ResumeCollectionChangeNotification();
            Models.RaiseCollectionChanged();
        }

        public class SearchModel
        {
            public string Library { get; set; }
            public string Class { get; set; }
            public string Method { get; set; }
            public int Line { get; set; }
            private string content;
            public string Content { get => content ?? (Filepath != null ? File.ReadAllText(Filepath) : ""); set => content = value; }
            public string LineContent { get; set; }
            public string Filepath { get; set; }
        }

        private string GetExtension(string extension)
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
                if (searchModel?.Filepath != null)
                {
                    webBrowser.NavigateToString(searchModel.Content);
                }
                else if (searchModel?.Content != null)
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

    public static class StringExtensions
    {
        public static string GetLine(this string value, int line)
        {
            var previousLine = 0;
            var lineIdx = 0;
            for (int i = 0; i <= line; i++)
            {
                previousLine = lineIdx;
                lineIdx = value.IndexOf('\n', lineIdx+1);
            }
            if (lineIdx != -1)
            {
                if (lineIdx <= previousLine)
                {
                    return value[previousLine..];
                }
                return value[previousLine..lineIdx];
            }
            return "";
        }
    }

    public class SpeedObservableCollection<T> : ObservableCollection<T>
    {
        public SpeedObservableCollection()
        {
            _suspendCollectionChangeNotification = false;
        }

        bool _suspendCollectionChangeNotification;

        public void RaiseCollectionChanged()
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suspendCollectionChangeNotification)
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke((Action)(() => OnCollectionChanged(e)), System.Windows.Threading.DispatcherPriority.DataBind);
                    return;
                }
                base.OnCollectionChanged(e);
            }
        }

        public void SuspendCollectionChangeNotification()
        {
            _suspendCollectionChangeNotification = true;
        }

        public void ResumeCollectionChangeNotification()
        {
            _suspendCollectionChangeNotification = false;
        }

        public void AddRange(IEnumerable<T> items)
        {
            bool shouldResume = !_suspendCollectionChangeNotification;
            SuspendCollectionChangeNotification();
            try
            {
                foreach (var i in items)
                    base.InsertItem(Count, i);
            }
            finally
            {
                if (shouldResume)
                {
                    ResumeCollectionChangeNotification();
                }
                var arg = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                OnCollectionChanged(arg);
            }
        }
    }
}
