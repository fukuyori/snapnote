using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SnapNoteStudio.Services
{
    public static class OcrTextFormatter
    {
        private const string JapaneseLetters = @"\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\uff66-\uff9f\u3005-\u3007";
        private const string JapanesePunctuation = "、。，．！？：；「」『』（）［］【】〈〉《》〔〕";

        // Remove only single ASCII spaces at Japanese text boundaries. Keep English
        // word separators, list-marker separators, tabs, and deliberate wider spacing.
        private static readonly Regex JapaneseSpacing = new Regex(
            @"(?<=[" + JapaneseLetters + @"]) (?=[\p{L}\p{N}])"
            + @"|(?<=[\p{L}\p{N}]) (?=[" + JapaneseLetters + @"])"
            + @"|(?<=[" + JapanesePunctuation + @"]) (?=\S)"
            + @"|(?<=\S) (?=[" + JapanesePunctuation + @"])",
            RegexOptions.Compiled);

        public static string JoinLines(IEnumerable<string> lines, string languageTag)
        {
            var text = string.Join(" ", lines);
            if (!string.Equals(languageTag, "ja", StringComparison.OrdinalIgnoreCase)
                && !languageTag.StartsWith("ja-", StringComparison.OrdinalIgnoreCase))
                return text;

            return JapaneseSpacing.Replace(text, string.Empty);
        }
    }
}
