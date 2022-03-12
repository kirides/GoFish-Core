using GoFish.DataAccess;
using GoFish.DataAccess.VisualFoxPro;
using GoFish.DataAccess.VisualFoxPro.Search;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace GoFishCore.WpfUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private static ThreadLocal<StringBuilder> sbTL = new ThreadLocal<StringBuilder>(() => new StringBuilder(4096));

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

    private bool _useRegex;
    public bool UseRegex
    {
        get => _useRegex;
        set
        {
            if (SetProperty(ref _useRegex, value) && !string.IsNullOrWhiteSpace(SearchText))
            {
                if (value && MessageBox.Show("Regex-escape current search text?", "Escape search", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    SearchText = Regex.Escape(SearchText);
                }
                else if (!value && MessageBox.Show("Unescape current search text?", "Unescape search", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    SearchText = Regex.Unescape(SearchText ?? "");
                }
            }
        }
    }

    public ICommand SearchCommand { get; }

    private bool canSearch = true;
    private CancellationTokenSource searchCancellation;

    public bool CanSearch
    {
        get => this.canSearch;
        set => SetProperty(ref this.canSearch, value);
    }

    public SpeedObservableCollection<SearchModel> Models { get; private set; } = new SpeedObservableCollection<SearchModel>();
    public int HighlightedLine { get; internal set; }

    private static readonly Searcher _regexSearcher = new Searcher(new RegexSearchAlgorithm());
    private static readonly Searcher _plainTextSearcher = new Searcher(new PlainTextNetCoreAlgorithm());
    private List<(string extension, ClassLibrary lib)> VfpLibCache = null;

    public MainViewModel()
    {
        SearchCommand = new DelegateCommand(PerformSearch, () => !string.IsNullOrWhiteSpace(SearchText));
    }

    private async void PerformSearch()
    {
        await App.UIContext;

        string directoryPath = DirectoryPath;
        string text = SearchText;

        try
        {
            await SearchAsync(directoryPath, text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    public void ClearCache()
    {
        this.VfpLibCache?.Clear();
        this.VfpLibCache = null;
        StatusText = "Cache cleared";
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private Searcher GetSearcher()
    {
        return UseRegex
            ? _regexSearcher
            : _plainTextSearcher;
    }

    private void SearchInCache(string text, CancellationToken cancellationToken = default)
    {
        int currentCachedFile = 0;
        int cachedFilesCount = this.VfpLibCache.Count;
        this.StatusTotal = cachedFilesCount;

        var searcher = GetSearcher();

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
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
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
        var searcher = GetSearcher();

#if DEBUG
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
            bool isPrg = false;
            try
            {
                if (fileExtension.Equals(".PRG", StringComparison.OrdinalIgnoreCase))
                {
                    using (var fs = File.OpenRead(file))
                    {
                        library = ClassLibrary.FromPRG(filename, fs, fileEncoding);
                    }
                    isPrg = true;
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
                    Array.Fill(reader.IncludeFieldIndices, false);
                    reader.IncludeFieldIndices[Constants.VCX.CLASS] = true; 
                    reader.IncludeFieldIndices[Constants.VCX.BASE_CLASS] = true;
                    reader.IncludeFieldIndices[Constants.VCX.NAME] = true;
                    reader.IncludeFieldIndices[Constants.VCX.PARENT_NAME] = true;
                    //reader.IncludeFieldIndices[Constants.VCX.PROPERTIES] = true;
                    reader.IncludeFieldIndices[Constants.VCX.BODY] = true;
                    IEnumerable<object[]> rows = reader.ReadRows((i, o) => (string)o[Constants.VCX.BODY] != "", includeMemo: true);
                    library = ClassLibrary.FromRows(filename, rows);
                }
                library.Classes.ForEach(x =>
                {
                    //x.BaseClass = null;
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
            /* WRITE OUT THE CLASS FILE TO COMPARE IT */
            if (!isPrg && false)
            {
                WriteSCCFile(library, Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".scc"));
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

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (errors.Count != 0)
        {
            return errors.ToList();
        }
        return null;
    }

    private void WriteSCCFile(ClassLibrary library, string targetFile)
    {
        var sb = sbTL.Value;
        foreach (var c in library.Classes)
        {
            sb.Append("DEFINE CLASS ");
            sb.Append(c.Name);
            sb.Append(" AS ");
            sb.AppendLine(c.BaseClass);
            sb.AppendLine();

            foreach (var p in c.Properties)
            {
                sb.Append("\t");
                sb.Append(p);
                sb.AppendLine();
            }
            sb.AppendLine();

            foreach (var m in c.Methods)
            {
                sb.Append("\t");
                if (m.Type == MethodType.Function)
                {
                    sb.Append("FUNCTION ");
                }
                else
                {
                    sb.Append("PROCEDURE ");
                }
                sb.Append(m.Name);
                sb.Append("(");
                for (int i = 0; i < m.Parameters.Count; i++)
                {
                    var p = m.Parameters[i];

                    sb.Append(p.Name);
                    if (!string.IsNullOrEmpty(p.Type))
                    {
                        sb.Append(" AS ");
                        sb.Append(p.Type);
                    }
                    if (i != m.Parameters.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
                sb.AppendLine(")");
                bool isStart = true;
                foreach (var line in m.Body.AsSpan().EnumerateLines())
                {
                    var isEmptyLine = line.Trim().IsEmpty;
                    if (isStart && isEmptyLine)
                    {
                        continue;
                    }
                    else
                    {
                        isStart = false;
                    }
                    if (!isEmptyLine)
                    {
                        sb.Append("\t\t");
                    }
                    sb.Append(line);
                    sb.AppendLine();
                }
                while (sb[sb.Length - 1] == '\n' || sb[sb.Length - 1] == '\r')
                {
                    sb.Remove(sb.Length - 1, 1);
                }
                sb.AppendLine();
                sb.Append("\t");
                if (m.Type == MethodType.Function)
                {
                    sb.AppendLine("ENDFUNC");
                }
                else
                {
                    sb.AppendLine("ENDPROC");
                }
                sb.AppendLine();
            }
            sb.AppendLine("ENDDEFINE");
            sb.AppendLine();
        }
        if (sb.Length == 0)
        {
            sb.Clear();
            return;
        }
        while (sb.Length > 0 && (sb[sb.Length - 1] == '\n' || sb[sb.Length - 1] == '\r'))
        {
            sb.Remove(sb.Length - 1, 1);
        }
        sb.AppendLine();
        Directory.CreateDirectory("classes");
        File.WriteAllText(targetFile, sb.ToString());
        sb.Clear();
    }

    private void CompleteSearch()
    {
        this.StatusText = $"{this.Models.Count} matches";
        this.StatusCurrent = 0;
        this.StatusTotal = 100;
        this.Models.ResumeCollectionChangeNotification();
        this.Models.RaiseCollectionChanged();
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
            UseRegex = UseRegex,
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
        this.UseRegex = config.UseRegex;
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