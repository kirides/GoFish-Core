using System;
using System.Collections.Generic;

namespace GoFish.DataAccess.VisualFoxPro.Search
{
    public class BoyerMoore
    {
        readonly int[] badChar = new int[char.MaxValue];
        private readonly int[] pattern;
        private readonly int[] patternLower;
        readonly int patternLengthMinusOne;
        readonly int patternLength;
        private readonly bool caseInsensitive;

        public BoyerMoore(string pattern, bool caseInsensitive = false)
        {
            if (caseInsensitive)
            {
                pattern = pattern.ToUpperInvariant();
                BadCharHeuristicInSensitive(pattern, ref badChar);
            }
            else
            {
                BadCharHeuristic(pattern, ref badChar);
            }

            this.pattern = new int[pattern.Length];
            for (int i = 0; i < this.pattern.Length; i++)
            {
                this.pattern[i] = (int)pattern[i];
            }
            if (caseInsensitive)
            {
                this.patternLower = new int[pattern.Length];
                for (int i = 0; i < this.pattern.Length; i++)
                {
                    this.patternLower[i] = (int)char.ToLowerInvariant(pattern[i]);
                }
            }
            patternLength = pattern.Length;
            patternLengthMinusOne = patternLength - 1;
            this.caseInsensitive = caseInsensitive;
        }

        public int IndexOf(ReadOnlySpan<char> str)
        {
            if (caseInsensitive)
            {
                return IndexOfInsensitive(str);
            }
            return IndexOfSensitive(str);
        }

        private int IndexOfSensitive(ReadOnlySpan<char> str)
        {
            int n = str.Length;
            int s = 0;
            while (s <= (n - patternLength))
            {
                int j = patternLengthMinusOne;

                while (j >= 0 && pattern[j] == str[s + j])
                    --j;

                if (j < 0)
                {
                    return s;
                }
                else
                {
                    s += Math.Max(1, j - badChar[str[s + j]]);
                }
            }
            return -1;
        }

        private int IndexOfInsensitive(ReadOnlySpan<char> str)
        {
            int n = str.Length;
            int s = 0;
            while (s <= (n - patternLength))
            {
                int j = patternLengthMinusOne;

                while (j >= 0 && (pattern[j] == str[s + j] || patternLower[j] == str[s + j])) --j;

                if (j < 0)
                {
                    return s;
                }
                else
                {
                    s += Math.Max(1, j - badChar[str[s + j]]);
                }
            }
            return -1;
        }

        public List<int> IndexOfAll(ReadOnlySpan<char> str)
        {
            int idx = -1;
            List<int> found = new List<int>();
            int len = 0;

            if (caseInsensitive)
            {
                do
                {
                    idx = IndexOfInsensitive(str);
                    if (idx == -1)
                    {
                        continue;
                    }
                    found.Add(len + idx);
                    if (str.Length < pattern.Length + 1)
                    {
                        break;
                    }
                    len += idx + 1 + pattern.Length;
                    str = str.Slice(idx + 1 + pattern.Length);
                }
                while (idx != -1);
            }
            else
            {
                do
                {
                    idx = IndexOfSensitive(str);
                    if (idx == -1)
                    {
                        continue;
                    }
                    found.Add(len + idx);
                    if (str.Length < pattern.Length + 1)
                    {
                        break;
                    }
                    len += idx + 1 + pattern.Length;
                    str = str.Slice(idx + 1 + pattern.Length);
                }
                while (idx != -1);
            }

            return found;
        }

        private static void BadCharHeuristic(ReadOnlySpan<char> str, ref int[] badChar)
        {
            for (int i = 0; i < badChar.Length; i++)
                badChar[i] = -1;

            for (int i = 0; i < str.Length; i++)
                badChar[(int)str[i]] = i;
        }

        private static void BadCharHeuristicInSensitive(ReadOnlySpan<char> str, ref int[] badChar)
        {
            for (int i = 0; i < badChar.Length; i++)
                badChar[i] = -1;

            for (int i = 0; i < str.Length; i++)
                badChar[(int)str[i]] = i;

            for (int i = 0; i < str.Length; i++)
                badChar[(int)char.ToLowerInvariant(str[i])] = i;
        }
    }
}