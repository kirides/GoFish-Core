using GoFish.DataAccess;
using GoFish.DataAccess.VisualFoxPro;
using GoFish.DataAccess.VisualFoxPro.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GoFishCore.WpfUI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private string statusText;
        public string StatusText { get => this.statusText; set => SetProperty(ref this.statusText, value); }

        private int statusTotal = 100;
        public int StatusTotal { get => this.statusTotal; set => SetProperty(ref this.statusTotal, value); }

        private int statusCurrent;
        public int StatusCurrent { get => this.statusCurrent; set => SetProperty(ref this.statusCurrent, value); }

        private bool progressUnknown;
        public bool ProgressUnknown { get => this.progressUnknown; set => SetProperty(ref this.progressUnknown, value); }

        private bool caseSensitive;
        public bool CaseSensitive { get => this.caseSensitive; set => SetProperty(ref this.caseSensitive, value); }

        private string directoryPath;
        public string DirectoryPath { get => this.directoryPath; set => SetProperty(ref this.directoryPath, value); }

        private string searchText;
        public string SearchText { get => this.searchText; set => SetProperty(ref this.searchText, value); }

        private bool canSearch = true;
        private CancellationTokenSource searchCancellation;

        public bool CanSearch
        {
            get => this.canSearch;
            set => SetProperty(ref this.canSearch, value);
        }

        public SpeedObservableCollection<SearchModel> Models { get; private set; } = new SpeedObservableCollection<SearchModel>();
        private static readonly Searcher searcher = new Searcher(new PlainTextAlgorithm());
        private List<(string extension, ClassLibrary lib)> VfpLibCache = null;

        public void ClearCache()
        {
            this.VfpLibCache?.Clear();
            this.VfpLibCache = null;
        }

        public List<string> SearchDirectory(string directoryPath, string text, CancellationToken cancellationToken = default)
        {
            this.Models.SuspendCollectionChangeNotification();
            this.Models.Clear();
            if (this.VfpLibCache != null)
            {
                int currentCachedFile = 0;
                int cachedFilesCount = this.VfpLibCache.Count;
                this.StatusTotal = cachedFilesCount;
                Parallel.ForEach(this.VfpLibCache, (entry, state) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Stop();
                    }

                    this.StatusText = $"[Cache] Processing File {Interlocked.Increment(ref currentCachedFile)} of {cachedFilesCount} (Cancel using ESC)";
                    Interlocked.Exchange(ref this.statusCurrent, currentCachedFile);
                    RaisePropertyChanged(nameof(this.StatusCurrent));
                    IEnumerable<SearchResult> results = searcher.Search(entry.lib, text, ignoreCase: !this.CaseSensitive);
                    lock (this.Models)
                    {
                        this.Models.AddRange(results.Select(r => new SearchModel
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
                this.StatusText = "";
                this.StatusCurrent = 0;
                this.StatusTotal = 100;
                this.Models.ResumeCollectionChangeNotification();
                this.Models.RaiseCollectionChanged();
                return null;
            }

            //var tempDir = Path.Combine(Path.GetTempPath(), "gofish-core");
            //try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            //catch { /* Nom nom nom */ }
            //var tmpDir = Directory.CreateDirectory(tempDir);

            IEnumerable<string> files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".vcx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".scx", StringComparison.OrdinalIgnoreCase));

            int fileCount = files.Count();
            this.StatusTotal = fileCount;
            int currentFile = 0;
            System.Collections.Concurrent.BlockingCollection<(string, ClassLibrary)> cache = new System.Collections.Concurrent.BlockingCollection<(string, ClassLibrary)>(fileCount);
            System.Collections.Concurrent.BlockingCollection<string> errors = new System.Collections.Concurrent.BlockingCollection<string>();
            Parallel.ForEach(files, (file, state) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    state.Stop();
                }

                this.StatusText = $"Processing File {Interlocked.Increment(ref currentFile)} of {fileCount} (Cancel using ESC)";
                Interlocked.Exchange(ref this.statusCurrent, currentFile);
                RaisePropertyChanged(nameof(this.StatusCurrent));
                string filename = Path.GetFileNameWithoutExtension(file);
                string fileExtension = Path.GetExtension(file);
                string memo = Path.Combine(Path.GetDirectoryName(file), filename + GetMemoExtension(fileExtension));
                if (!File.Exists(memo))
                {
                    return;
                }
                ClassLibrary library;
                try
                {
                    Dbf dbf = new Dbf(file, memo, System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252));
                    DbfReader reader = new DbfReader(dbf, System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252));
                    IEnumerable<object[]> rows = reader.ReadRows((i, o) => (string)o[Constants.VCX.BODY] != "", includeMemo: true);
                    library = ClassLibrary.FromRows(filename, rows);
                    library.Classes.ForEach(x =>
                    {
                        x.BaseClass = null;
                        foreach (Method m in x.Methods)
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

                IEnumerable<SearchResult> results = searcher.Search(library, text, ignoreCase: !this.CaseSensitive);

                lock (this.Models)
                {
                    //searcher.SaveResults(results, r => $"{r.Class}.{ r.Method}.{r.Line}", tmpDir.FullName);
                    this.Models.AddRange(results.Select(r => new SearchModel
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
            this.VfpLibCache = cache.ToList();
            this.StatusText = "";
            this.StatusCurrent = 0;
            this.StatusTotal = 100;
            this.Models.ResumeCollectionChangeNotification();
            this.Models.RaiseCollectionChanged();
            if (errors.Count != 0)
            {
                return errors.ToList();
            }
            return null;
        }

        public async Task SearchAsync(string directoryPath, string text)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath) && !string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    this.searchCancellation = new CancellationTokenSource();
                    this.CanSearch = false;
                    await Task.Run(() => SearchDirectory(directoryPath, text, this.searchCancellation.Token)).ConfigureAwait(false);
                }
                finally
                {
                    this.CanSearch = true;
                }
            }
        }
        public void CancelSearch()
        {
            this.searchCancellation?.Cancel();
        }
        public void SaveConfig()
        {
            ConfigJson config = new ConfigJson
            {
                CaseSensitive = CaseSensitive,
                LastDirectory = DirectoryPath,
                LastSearch = SearchText,
            };
            byte[] jsonBytes = JsonSerializer.ToBytes(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllBytes("config.json", jsonBytes);
        }

        public void LoadConfig()
        {
            if (!File.Exists("config.json"))
            {
                return;
            }

            ConfigJson config = JsonSerializer.Parse<ConfigJson>(File.ReadAllBytes("config.json"));
            this.DirectoryPath = config.LastDirectory;
            this.CaseSensitive = config.CaseSensitive;
            this.SearchText = config.LastSearch;
        }

        /// <param name="extension">The table extension (.VCX, .SCX)</param>
        /// <exception cref="NotSupportedException"/>
        private string GetMemoExtension(string extension)
        {
            switch (extension.ToUpperInvariant())
            {
                case ".VCX": return ".VCT";
                case ".SCX": return ".SCT";
                default: throw new NotSupportedException("This extension is not supported");
            }
        }
    }
}
