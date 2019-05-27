namespace GoFishCore.WpfUI
{
    public static class StringExtensions
    {
        public static string GetLine(this string value, int line)
        {
            var previousLine = 0;
            var lineIdx = 0;
            for (int i = 0; i <= line; i++)
            {
                previousLine = lineIdx;
                lineIdx = value.IndexOf('\n', lineIdx + 1);
            }
            if (lineIdx <= previousLine)
            {
                return value[previousLine..];
            }
            return value[previousLine..lineIdx];
        }
    }
}
