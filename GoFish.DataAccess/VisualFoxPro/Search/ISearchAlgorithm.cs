using System.Collections.Generic;
using System.Threading;

namespace GoFish.DataAccess.VisualFoxPro.Search;

public interface ISearchAlgorithm
{
    IEnumerable<SearchResult> Search(ClassLibrary lib, string text, bool ignoreCase, CancellationToken cancellationToken);
}
