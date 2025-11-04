using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace IntronFileController.Common
{
    public static class ExtensionsMethods
    {
        /// <summary>
        /// Retrieves the specified number of lines from the end of the string.
        /// </summary>
        /// <param name="refString">The string from which to extract the lines.</param>
        /// <param name="linesCount">The number of lines to retrieve. Must be a non-negative integer.</param>
        /// <returns>A string containing the last <paramref name="linesCount"/> lines of <paramref name="refString"/>.  If the
        /// string contains fewer lines than specified, the entire string is returned.</returns>
        public static string LastLines(this string refString, int linesCount)
        {
            int count = 0;
            int pos = refString.Length - 1;

            while (pos >= 0 && count < linesCount)
            {
                if (refString[pos] == '\n')
                    count++;

                pos--;
            }

            return refString[pos..];
        }
        /// <summary>
        /// Extracts the first specified number of lines from the given string, using the specified newline character.
        /// </summary>
        /// <param name="refString">The string from which to extract the lines.</param>
        /// <param name="linesCount">The number of lines to extract. Must be a positive integer.</param>
        /// <param name="newLineChar">The character used to identify line breaks. Defaults to <see langword="'\n'"/>.</param>
        /// <returns>A substring containing the first <paramref name="linesCount"/> lines from <paramref name="refString"/>. If
        /// the string contains fewer lines than specified, the entire string is returned.</returns>
        public static string FirstLines(this string refString, int linesCount, char newLineChar = '\n')
        {
            int count = 0;
            int pos = 0;

            while (pos < refString.Length && count < linesCount)
            {
                if (refString[pos] == newLineChar)
                    count++;

                pos++;
            }

            return refString[..pos];
        }

        /// <summary>
        /// Retrieves a specified range of lines from the end of the given text.
        /// </summary>
        /// <remarks>This method splits the input text into lines using the specified newline character, then extracts the
        /// desired range of lines starting from the calculated position. If <paramref name="fromEndStart"/> exceeds the total
        /// number of lines, the range starts from the beginning of the text.</remarks>
        /// <param name="text">The input string from which lines are extracted. If <paramref name="text"/> is <see langword="null"/>, an empty
        /// string is returned.</param>
        /// <param name="fromEndStart">The number of lines to skip from the end of the text before starting the range. Must be non-negative.</param>
        /// <param name="count">The number of lines to include in the range. Must be non-negative.</param>
        /// <param name="newLineChar">The character used to split the text into lines. Defaults to <see langword="'\n'"/>.</param>
        /// <returns>A string containing the specified range of lines, joined by the newline character. If the range is empty, an empty
        /// string is returned.</returns>
        public static string GetLastRange(this string text, int fromEndStart, int count, char newLineChar = '\n')
        {
            if (text == null)
                return string.Empty;

            var lines = text.Split(newLineChar);

            int n = lines.Length;

            // começa em "n - fromEndStart"
            int startIndex = Math.Max(0, n - fromEndStart);

            // pega "count" linhas dali
            var slice = lines.Skip(startIndex).Take(count);

            return string.Join("\n", slice);
        }
    }
}
