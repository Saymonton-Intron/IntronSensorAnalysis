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

            while (pos < refString.Length && count < 20)
            {
                if (refString[pos] == newLineChar)
                    count++;

                pos++;
            }

            return refString.Substring(0, pos);
        }        
    }
}
