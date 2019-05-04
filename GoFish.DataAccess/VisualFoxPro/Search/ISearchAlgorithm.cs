using System;
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
        public virtual IEnumerable<SearchResult> Search(ClassLibrary lib, string text, bool ignoreCase = false)
        {
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
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
                        idx = code.Span.IndexOf(text.AsSpan(), comparison);
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
            foreach (var c in content)
            {
                if (c == '\n') count++;
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