namespace Synerixis.Application.Helpers
{
    /// <summary>
    /// CBEC 买家语种：ID/TH/VN/EN/ZH（优先级顺序）。启发式检测，无假翻译。
    /// </summary>
    public static class CbecLanguageHelper
    {
        public static readonly string[] SupportedPriority = { "ID", "TH", "VN", "EN", "ZH" };
        public const string DefaultWorkingLanguage = "ZH";
        public const string DefaultSupportedCsv = "ID,TH,VN,EN,ZH";

        public static string Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return DefaultWorkingLanguage;
            var c = code.Trim().ToUpperInvariant();
            if (c is "ZH-CN" or "ZH-TW" or "CN" or "CHS") return "ZH";
            if (c is "ID-ID" or "IN" or "BAHASA") return "ID";
            if (c is "TH-TH") return "TH";
            if (c is "VI" or "VI-VN" or "VN-VN") return "VN";
            if (c is "EN-US" or "EN-GB" or "ENG") return "EN";
            return SupportedPriority.Contains(c) ? c : DefaultWorkingLanguage;
        }

        public static bool IsSupported(string? code) =>
            SupportedPriority.Contains(Normalize(code));

        public static IReadOnlyList<string> ParseSupportedList(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return SupportedPriority;
            var list = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Normalize)
                .Where(c => SupportedPriority.Contains(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return list.Count > 0 ? list : SupportedPriority;
        }

        public static string ToCsv(IEnumerable<string>? langs) =>
            string.Join(",", (langs ?? SupportedPriority).Select(Normalize).Distinct());

        public static string DisplayName(string? code) => Normalize(code) switch
        {
            "ID" => "Bahasa Indonesia",
            "TH" => "ไทย (Thai)",
            "VN" => "Tiếng Việt",
            "EN" => "English",
            "ZH" => "中文",
            _ => code ?? "?"
        };

        /// <summary>廉价启发式语种检测（不调用 LLM）。</summary>
        public static string Detect(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return DefaultWorkingLanguage;
            var t = text.Trim();
            int thai = 0, cjk = 0, latin = 0, vietMarks = 0;
            foreach (var ch in t)
            {
                if (ch >= 0x0E00 && ch <= 0x0E7F) thai++;
                else if (ch >= 0x4E00 && ch <= 0x9FFF) cjk++;
                else if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z') latin++;
                else if ("ăâêôơưđĂÂÊÔƠƯĐáàảãạéèẻẽẹíìỉĩịóòỏõọúùủũụýỳỷỹỵ".Contains(ch))
                    vietMarks++;
            }

            if (thai >= 2 || thai * 3 >= Math.Max(1, t.Length / 4)) return "TH";
            if (cjk >= 2 && cjk >= latin) return "ZH";
            if (vietMarks >= 2) return "VN";

            var lower = t.ToLowerInvariant();
            if (ContainsAny(lower,
                    "saya", "anda", "apakah", "pengiriman", "barang", "pesanan", "kapan",
                    "terima kasih", "berapa", "bisa", "tolong", "sudah", "belum", "ongkir"))
                return "ID";
            if (ContainsAny(lower,
                    "không", "được", "với", "giao hàng", "đơn hàng", "bao giờ", "cảm ơn",
                    "giá", "ship", "hàng"))
                return "VN";
            if (ContainsAny(lower,
                    "the ", " is ", " are ", "please", "order", "shipping", "when ", "how ",
                    "thank", "price", "delivery", "tracking"))
                return "EN";
            if (cjk > 0) return "ZH";
            if (latin > 0) return "EN";
            return DefaultWorkingLanguage;
        }

        public static string PromptLanguageName(string? code) => Normalize(code) switch
        {
            "ID" => "Indonesian (Bahasa Indonesia)",
            "TH" => "Thai",
            "VN" => "Vietnamese",
            "EN" => "English",
            "ZH" => "Simplified Chinese",
            _ => "Simplified Chinese"
        };

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            foreach (var n in needles)
            {
                if (!string.IsNullOrEmpty(n) && haystack.Contains(n, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
