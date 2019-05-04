
namespace GoFish.DataAccess.VisualFoxPro.Search
{
    public class SearchResult
    {
        public string Library { get; set; }
        public string Class { get; set; }
        public string Method { get; set; }
        public string Content { get; set; }
        public int Line { get; set; }

        public SearchResult(string library, string @class, string method, int line, string content)
        {
            this.Library = library;
            this.Class = @class;
            this.Method = method;
            this.Line = line;
            this.Content = content;
        }
    }
}
