﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace GoFish.DataAccess.VisualFoxPro.Search
{
    public class PlainTextBoyerMooreAlgorithm : ISearchAlgorithm
    {
        private readonly ConcurrentDictionary<(string pattern, bool ignoreCase), BoyerMoore> searcherCache
            = new ConcurrentDictionary<(string, bool), BoyerMoore>();
        public virtual IEnumerable<SearchResult> Search(ClassLibrary lib, string text, bool ignoreCase = false, CancellationToken cancellationToken = default)
        {
            var boyerMoore = searcherCache.GetOrAdd((text, ignoreCase), (c) => new BoyerMoore(c.pattern, c.ignoreCase));

            foreach (var c in lib.Classes)
            {
                for (int i = 0; i < c.Methods.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) yield break;
                    var method = c.Methods[i];
                    var codeBody = method.Body;
                    var code = codeBody.AsMemory();
                    int idx = -1;
                    int lastLines = 0;
                    do
                    {
                        if (cancellationToken.IsCancellationRequested) yield break;
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
                        code = code.Slice(idx + text.Length);
                    }
                    while (idx != -1 && code.Length > 0);
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
}