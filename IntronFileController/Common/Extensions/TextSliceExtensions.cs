using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntronFileController.Common.Extensions
{
    public static class TextSliceExtensions
    {
        public static string[] Lines(this string text)
            => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        public static string JoinLines(this IEnumerable<string> lines)
            => string.Join(Environment.NewLine, lines);

        // 1-based, inclui end
        private static (int startIdx, int count) ClampRange1Based(int startLine, int endLine, int total)
        {
            startLine = Math.Max(1, startLine);
            endLine = Math.Min(total, Math.Max(startLine, endLine));
            int count = total == 0 ? 0 : (endLine - startLine + 1);
            int startIdx0 = (total == 0) ? 0 : (startLine - 1);
            return (startIdx0, Math.Max(0, count));
        }

        // TOPO
        public static string TopContextBlock(this string text, int cutLine1Based, int contextCount)
        {
            var lines = text.Lines();
            if (lines.Length == 0 || cutLine1Based <= 0) return string.Empty;

            int end = cutLine1Based;
            int start = end - Math.Max(0, contextCount) + 1; // ex.: 96..100
            var (startIdx, count) = ClampRange1Based(start, end, lines.Length);
            return lines.Skip(startIdx).Take(count).JoinLines();
        }

        // NOVO: janela após o corte do topo (cut+1 .. cut+context)
        public static string TopKeepWindow(this string text, int cutLine1Based, int contextCount)
        {
            var lines = text.Lines();
            if (lines.Length == 0 || cutLine1Based >= lines.Length) return string.Empty;

            int start = cutLine1Based + 1;
            int end = Math.Min(lines.Length, start + Math.Max(0, contextCount) - 1);
            var (startIdx, count) = ClampRange1Based(start, end, lines.Length);
            return lines.Skip(startIdx).Take(count).JoinLines();
        }

        // RODAPÉ
        public static string BottomContextBlock(this string text, int cutLine1Based, int contextCount)
        {
            var lines = text.Lines();
            if (lines.Length == 0 || cutLine1Based > lines.Length) return string.Empty;

            int start = Math.Max(1, cutLine1Based);
            int end = Math.Min(lines.Length, start + Math.Max(0, contextCount) - 1); // ex.: 150..154
            var (startIdx, count) = ClampRange1Based(start, end, lines.Length);
            return lines.Skip(startIdx).Take(count).JoinLines();
        }

        // NOVO: janela antes do corte do rodapé (cut-context .. cut-1)
        public static string BottomKeepWindow(this string text, int cutLine1Based, int contextCount)
        {
            var lines = text.Lines();
            if (lines.Length == 0 || cutLine1Based <= 1) return string.Empty;

            int end = cutLine1Based - 1;
            int start = end - Math.Max(0, contextCount) + 1;
            var (startIdx, count) = ClampRange1Based(start, end, lines.Length);
            return lines.Skip(startIdx).Take(count).JoinLines();
        }
    }
}
