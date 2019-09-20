using GoFish.DataAccess;
using GoFish.DataAccess.VisualFoxPro;
using GoFish.DataAccess.VisualFoxPro.Search;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void SearchInCache(string text, CancellationToken cancellationToken = default)
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
            CompleteSearch();
        }
        public List<string> SearchDirectory(string directoryPath, string text, CancellationToken cancellationToken = default)
        {
            this.Models.SuspendCollectionChangeNotification();
            this.Models.Clear();
            if (this.VfpLibCache != null)
            {
                SearchInCache(text, cancellationToken);
                return null;
            }

            IEnumerable<string> files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".vcx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".scx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".prg", StringComparison.OrdinalIgnoreCase));

            int fileCount = files.Count();
            this.StatusTotal = fileCount;
            int currentFile = 0;

            BlockingCollection<(string, ClassLibrary)> cache = new BlockingCollection<(string, ClassLibrary)>();
            BlockingCollection<string> errors = new BlockingCollection<string>();

            var fileEncoding = System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252);
#if DEBUG5
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 1 }, (file, state) =>
#else
            Parallel.ForEach(files, (file, state) =>
#endif            
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


                ClassLibrary library;
                try
                {
                    if (fileExtension.Contains("PRG", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var fs = File.OpenRead(file))
                        {
                            library = ClassLibrary.FromPRG(filename, fs, fileEncoding);
                        }
                    }
                    else
                    {
                        string memo = Path.Combine(Path.GetDirectoryName(file), filename + GetMemoExtension(fileExtension));
                        if (!File.Exists(memo))
                        {
                            return;
                        }
                        Dbf dbf = new Dbf(file, memo, fileEncoding);
                        DbfReader reader = new DbfReader(dbf, fileEncoding);
                        IEnumerable<object[]> rows = reader.ReadRows((i, o) => (string)o[Constants.VCX.BODY] != "", includeMemo: true);
                        library = ClassLibrary.FromRows(filename, rows);
                    }
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

                IEnumerable<SearchResult> results = searcher.Search(library, text, ignoreCase: !this.CaseSensitive, cancellationToken);

                lock (this.Models)
                {
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
            CompleteSearch();
            if (errors.Count != 0)
            {
                return errors.ToList();
            }
            return null;
        }

        private void CompleteSearch()
        {
            this.StatusText = $"{this.Models.Count} matches";
            this.StatusCurrent = 0;
            this.StatusTotal = 100;
            this.Models.ResumeCollectionChangeNotification();
            this.Models.RaiseCollectionChanged();
        }

        private static ClassLibrary LibraryFromDbf(string file, System.Text.Encoding fileEncoding, string filename, string memo)
        {
            Dbf dbf = new Dbf(file, memo, fileEncoding);
            DbfReader reader = new DbfReader(dbf, fileEncoding);
            IEnumerable<object[]> rows = reader.ReadRows((i, o) => (string)o[Constants.VCX.BODY] != "", includeMemo: true);
            var library = ClassLibrary.FromRows(filename, rows);
            return library;
        }

        public async Task SearchAsync(string directoryPath, string text)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath) && !string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    CancelSearch();
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
            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllBytes("config.json", jsonBytes);
        }

        public void LoadConfig()
        {
            if (!File.Exists("config.json"))
            {
                return;
            }

            ConfigJson config = JsonSerializer.Deserialize<ConfigJson>(File.ReadAllBytes("config.json"));
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
