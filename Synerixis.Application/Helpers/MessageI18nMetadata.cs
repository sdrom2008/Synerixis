using System.Text.Json;

namespace Synerixis.Application.Helpers
{
    /// <summary>ChatMessage.Metadata JSON 中的语种 / 译文字段。</summary>
    public static class MessageI18nMetadata
    {
        private static readonly JsonSerializerOptions Opts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public sealed class Payload
        {
            public string? Lang { get; set; }
            public string? Translation { get; set; }
            public string? TranslatedTo { get; set; }
            public string? TranslationMode { get; set; }
            public string? TranslationWarning { get; set; }
        }

        public static Payload Parse(string? metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata)) return new Payload();
            try
            {
                return JsonSerializer.Deserialize<Payload>(metadata, Opts) ?? new Payload();
            }
            catch
            {
                return new Payload();
            }
        }

        public static string Merge(string? existing, Payload patch)
        {
            var cur = Parse(existing);
            if (!string.IsNullOrWhiteSpace(patch.Lang)) cur.Lang = patch.Lang;
            if (patch.Translation != null) cur.Translation = patch.Translation;
            if (!string.IsNullOrWhiteSpace(patch.TranslatedTo)) cur.TranslatedTo = patch.TranslatedTo;
            if (!string.IsNullOrWhiteSpace(patch.TranslationMode)) cur.TranslationMode = patch.TranslationMode;
            if (patch.TranslationWarning != null) cur.TranslationWarning = patch.TranslationWarning;
            return JsonSerializer.Serialize(cur, Opts);
        }

        public static string WithDetectedLang(string? existing, string lang) =>
            Merge(existing, new Payload { Lang = CbecLanguageHelper.Normalize(lang) });
    }
}
