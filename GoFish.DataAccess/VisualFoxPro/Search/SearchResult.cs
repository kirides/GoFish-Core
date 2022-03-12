
namespace GoFish.DataAccess.VisualFoxPro.Search;

public record SearchResult(
    string Library,
    string Class,
    string Method,
    int Line,
    string Content);
