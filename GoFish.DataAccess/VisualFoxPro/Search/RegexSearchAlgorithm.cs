using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace GoFish.DataAccess.VisualFoxPro.Search;

public class RegexSearchAlgorithm : ISearchAlgorithm
{
    private readonly ConcurrentDictionary<(string pattern, bool ignoreCase), Regex> searcherCache
        = new ConcurrentDictionary<(string, bool), Regex>();

    public IEnumerable<SearchResult> Search(ClassLibrary lib, string text, bool ignoreCase = false, CancellationToken cancellationToken = default)
    {
        var rx = searcherCache.GetOrAdd((text, ignoreCase), (c) =>
        {
            var rxOptions = RegexOptions.Compiled;
            if (ignoreCase)
            {
                rxOptions |= RegexOptions.IgnoreCase;
            }

            return new Regex(text, rxOptions);
        });

        foreach (var c in lib.Classes)
        {
            for (int i = 0; i < c.Methods.Count; i++)
            {
                var method = c.Methods[i];
                var codeBody = method.Body;
                var code = codeBody.AsMemory();
                foreach (Match m in rx.Matches(codeBody))
                {
                    if (cancellationToken.IsCancellationRequested) yield break;
                    int lastLines = CountNewlines(code.Slice(0, m.Index).Span);
                    yield return new SearchResult(lib.Name, c.Name, method.Name, lastLines, codeBody);
                }
            }
        }
    }

    protected int CountNewlines(ReadOnlySpan<char> content)
    {
        var count = 0;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n') count++;

        }
        return count;
    }
}
