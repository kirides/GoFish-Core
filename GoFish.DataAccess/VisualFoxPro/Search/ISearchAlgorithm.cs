using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GoFish.DataAccess.VisualFoxPro.Search
{
    public interface ISearchAlgorithm
    {
        IEnumerable<SearchResult> Search(ClassLibrary lib, string text, bool ignoreCase);
    }

    public class PlainTextAlgorithm : ISearchAlgorithm
    {
        private readonly ConcurrentDictionary<string, BoyerMoore> searcherCache = new ConcurrentDictionary<string, BoyerMoore>();
        public virtual IEnumerable<SearchResult> Search(ClassLibrary lib, string text, bool ignoreCase = false)
        {
            var boyerMoore = searcherCache.GetOrAdd(text, t => new BoyerMoore(t, ignoreCase));

            foreach (var c in lib.Classes)
            {
                for (int i = 0; i < c.Methods.Count; i++)
                {
                    var method = c.Methods[i];
                    var codeBody = method.Body;
                    var code = codeBody.AsMemory();
                    int idx = -1;
                    int lastLines = 0;
                    do
                    {
                        idx = boyerMoore.IndexOf(code.Span);
                        if (idx == -1)
                        {
                            continue;
                        }
                        lastLines += CountNewlines(code.Slice(0, idx).Span);

                        yield return new SearchResult(lib.Name, c.Name, method.Name, lastLines, codeBody);

                        if (code.Length < text.Length + 1)
                        {
                            break;
                        }
                        code = code.Slice(idx + 1 + text.Length);
                    }
                    while (idx != -1);
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
    public class RegexSearchAlgorithm : PlainTextAlgorithm
    {
        public override IEnumerable<SearchResult> Search(ClassLibrary lib, string text, bool ignoreCase = false)
        {
            var rxOptions = RegexOptions.Compiled;
            if (ignoreCase)
            {
                rxOptions |= RegexOptions.IgnoreCase;
            }

            var rx = new Regex(text, rxOptions);

            foreach (var c in lib.Classes)
            {
                for (int i = 0; i < c.Methods.Count; i++)
                {
                    var method = c.Methods[i];
                    var codeBody = method.Body;
                    var code = codeBody.AsMemory();
                    foreach (Match m in rx.Matches(codeBody))
                    {
                        int lastLines = CountNewlines(code.Slice(0, m.Index).Span);
                        yield return new SearchResult(lib.Name, c.Name, method.Name, lastLines, codeBody);
                    }
                }
            }
        }
    }
}