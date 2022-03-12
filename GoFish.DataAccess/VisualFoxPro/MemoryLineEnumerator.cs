using System;

namespace GoFish.DataAccess.VisualFoxPro;
public static class MemoryExtensions
{
    public static MemoryLineEnumerator EnumerateLines(this ReadOnlyMemory<char> buffer)
    {
        return new MemoryLineEnumerator(buffer);
    }
}
/// <summary>
/// See: https://source.dot.net/#System.Private.CoreLib/SpanLineEnumerator.cs
/// <para/>
/// Licensed to the .NET Foundation under one or more agreements.
/// The .NET Foundation licenses this file to you under the MIT license.
/// </summary>
public ref struct MemoryLineEnumerator
{
    private ReadOnlyMemory<char> _remaining;
    private ReadOnlyMemory<char> _current;
    private bool _isEnumeratorActive;

    internal MemoryLineEnumerator(ReadOnlyMemory<char> buffer)
    {
        _remaining = buffer;
        _current = default;
        _isEnumeratorActive = true;
    }

    /// <summary>
    /// Gets the line at the current position of the enumerator.
    /// </summary>
    public ReadOnlyMemory<char> Current => _current;

    /// <summary>
    /// Returns this instance as an enumerator.
    /// </summary>
    public MemoryLineEnumerator GetEnumerator() => this;

    /// <summary>
    /// Advances the enumerator to the next line of the span.
    /// </summary>
    /// <returns>
    /// True if the enumerator successfully advanced to the next line; false if
    /// the enumerator has advanced past the end of the span.
    /// </returns>
    public bool MoveNext()
    {
        if (!_isEnumeratorActive)
        {
            return false; // EOF previously reached or enumerator was never initialized
        }

        int idx = IndexOfNewlineChar(_remaining.Span, out int stride);
        if (idx >= 0)
        {
            _current = _remaining.Slice(0, idx);
            _remaining = _remaining.Slice(idx + stride);
        }
        else
        {
            // We've reached EOF, but we still need to return 'true' for this final
            // iteration so that the caller can query the Current property once more.

            _current = _remaining;
            _remaining = default;
            _isEnumeratorActive = false;
        }

        return true;
    }

    // https://source.dot.net/#System.Private.CoreLib/String.Manipulation.cs,eed35cc999189393
    // Scans the input text, returning the index of the first newline char.
    // Newline chars are given by the Unicode Standard, Sec. 5.8.
    internal static int IndexOfNewlineChar(ReadOnlySpan<char> text, out int stride)
    {
        // !! IMPORTANT !!
        //
        // We expect this method may be called with untrusted input, which means we need to
        // bound the worst-case runtime of this method. We rely on MemoryExtensions.IndexOfAny
        // having worst-case runtime O(i), where i is the index of the first needle match within
        // the haystack; or O(n) if no needle is found. This ensures that in the common case
        // of this method being called within a loop, the worst-case runtime is O(n) rather than
        // O(n^2), where n is the length of the input text.
        //
        // The Unicode Standard, Sec. 5.8, Recommendation R4 and Table 5-2 state that the CR, LF,
        // CRLF, NEL, LS, FF, and PS sequences are considered newline functions. That section
        // also specifically excludes VT from the list of newline functions, so we do not include
        // it in the needle list.

        const string needles = "\r\n\f\u0085\u2028\u2029";

        stride = default;
        int idx = text.IndexOfAny(needles);
        if ((uint)idx < (uint)text.Length)
        {
            stride = 1; // needle found

            // Did we match CR? If so, and if it's followed by LF, then we need
            // to consume both chars as a single newline function match.

            if (text[idx] == '\r')
            {
                int nextCharIdx = idx + 1;
                if ((uint)nextCharIdx < (uint)text.Length && text[nextCharIdx] == '\n')
                {
                    stride = 2;
                }
            }
        }

        return idx;
    }
}
