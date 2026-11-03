using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BossMod;

public enum UILanguage
{
    [PropertyDisplay("Follow Dalamud")]
    Auto,
    [PropertyDisplay("English")]
    English,
    [PropertyDisplay("简体中文")]
    ChineseSimplified,
}

// UI localization: english source strings are the keys, translations are loaded from embedded json dictionaries.
// Strings are translated at the point they are drawn, so configs, presets and other serialized data always stay in english.
// Keys containing {0}, {1}... are templates that match strings built with interpolation; captured values are reinserted (and translated, if they are known keys themselves).
public static partial class Loc
{
    private sealed class Template(string prefix, string suffix, Regex regex, string translation, int[] order)
    {
        public readonly string Prefix = prefix;
        public readonly string Suffix = suffix;
        public readonly Regex Regex = regex;
        public readonly string Translation = translation;
        public readonly int[] Order = order; // placeholder index for each capture group
    }

    private const int MaxCacheSize = 8192;

    private static readonly System.Threading.Lock _lock = new();
    private static Dictionary<string, string> _exact = [];
    private static Template[] _templates = [];
    private static readonly Dictionary<string, string> _cache = [];
    private static readonly HashSet<string> _missing = [];
    private static UILanguage _configured = UILanguage.Auto;
    private static string _dalamudLanguage = "en";

    public static bool IsChinese { get; private set; }
    public static int Version { get; private set; } // incremented every time the active language changes, for code that caches translated text
    public static bool CollectMissing; // when set, strings that were drawn but have no translation are recorded and can be dumped with /bmr loc
    public static event Action? Changed;

    public static void Initialize(string dalamudLanguage, UILanguage configured)
    {
        _dalamudLanguage = dalamudLanguage;
        _configured = configured;
        Update();
    }

    public static void SetDalamudLanguage(string language)
    {
        _dalamudLanguage = language;
        Update();
    }

    public static void SetConfigured(UILanguage language)
    {
        _configured = language;
        Update();
    }

    // translate a string for display; strings without a translation are returned as is
    // label suffixes used by imgui for ids ("Text##id", "Text###id") are preserved and not translated
    [return: NotNullIfNotNull(nameof(text))]
    public static string? Tr(string? text)
    {
        if (!IsChinese || string.IsNullOrEmpty(text))
            return text;
        if (_exact.TryGetValue(text, out var exact))
            return exact;

        lock (_lock)
        {
            if (_cache.TryGetValue(text, out var cached))
                return cached;
            var res = TranslateComplex(text);
            if (_cache.Count >= MaxCacheSize)
                _cache.Clear();
            _cache[text] = res;
            if (CollectMissing && ReferenceEquals(res, text) && HasLetters(text))
                _missing.Add(text);
            return res;
        }
    }

    // translate an imgui widget label; the widget id stays derived from the english text, so it is stable and doesn't collide when two labels share a translation
    public static string Id(string label) => !IsChinese || label.Contains("##", StringComparison.Ordinal) ? Tr(label) : $"{Tr(label)}###{label}";

    // translate a composite format string, then format it with the given arguments
    public static string Format(string format, params object?[] args) => string.Format(Tr(format), args);

    // translate each element; useful for combo option lists
    public static string[] Tr(string[] texts)
    {
        if (!IsChinese)
            return texts;
        var res = new string[texts.Length];
        for (var i = 0; i < texts.Length; ++i)
            res[i] = Tr(texts[i]);
        return res;
    }

    public static IReadOnlyCollection<string> Missing
    {
        get
        {
            lock (_lock)
                return [.. _missing];
        }
    }

    public static string DumpMissing(string directory)
    {
        var path = Path.Combine(directory, "missing-translations.json");
        var list = Missing.Order(StringComparer.Ordinal).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(list.ToDictionary(s => s, _ => ""), new JsonSerializerOptions() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return path;
    }

    private static void Update()
    {
        var chinese = _configured switch
        {
            UILanguage.English => false,
            UILanguage.ChineseSimplified => true,
            _ => _dalamudLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
        };
        if (chinese && _exact.Count == 0)
            Load("zh_CN");

        lock (_lock)
        {
            if (chinese == IsChinese)
                return;
            IsChinese = chinese;
            _cache.Clear();
            ++Version;
        }
        Changed?.Invoke();
    }

    private static void Load(string language)
    {
        var exact = new Dictionary<string, string>(16384, StringComparer.Ordinal);
        var templates = new List<Template>();
        var asm = Assembly.GetExecutingAssembly();
        var prefix = $"BossMod.Localization.{language}.";
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            try
            {
                using var stream = asm.GetManifestResourceStream(name)!;
                using var json = JsonDocument.Parse(stream, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                foreach (var entry in json.RootElement.EnumerateObject())
                {
                    var value = entry.Value.GetString();
                    if (string.IsNullOrEmpty(value) || value == entry.Name)
                        continue;
                    exact[entry.Name] = value; // template keys are also stored as is, for Format calls
                    if (PlaceholderRegex().IsMatch(entry.Name) && BuildTemplate(entry.Name, value) is { } t)
                        templates.Add(t);
                }
            }
            catch (Exception ex)
            {
                Service.Log($"[Loc] Failed to load {name}: {ex}");
            }
        }
        // more specific templates (longer literal parts) are tried first
        templates.Sort((a, b) => (b.Prefix.Length + b.Suffix.Length).CompareTo(a.Prefix.Length + a.Suffix.Length));
        lock (_lock)
        {
            _exact = exact;
            _templates = [.. templates];
            _cache.Clear();
        }
        Service.Log($"[Loc] Loaded {exact.Count} strings and {templates.Count} templates for {language}");
    }

    private static Template? BuildTemplate(string key, string translation)
    {
        var matches = PlaceholderRegex().Matches(key);
        var pattern = new StringBuilder("^");
        var order = new int[matches.Count];
        var last = 0;
        for (var i = 0; i < matches.Count; ++i)
        {
            var m = matches[i];
            pattern.Append(Regex.Escape(key[last..m.Index]));
            pattern.Append("(.+?)");
            order[i] = int.Parse(m.Groups[1].Value);
            last = m.Index + m.Length;
        }
        pattern.Append(Regex.Escape(key[last..]));
        pattern.Append('$');
        try
        {
            return new(key[..matches[0].Index], key[last..], new Regex(pattern.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant), translation, order);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // called with lock held
    private static string TranslateComplex(string text)
    {
        // imgui id suffix
        var idStart = text.IndexOf("##", StringComparison.Ordinal);
        if (idStart >= 0)
        {
            return idStart == 0 ? text : TranslateSimple(text[..idStart]) + text[idStart..];
        }

        var res = TranslateSimple(text);
        if (!ReferenceEquals(res, text))
            return res;

        // multi-line text: translate each line separately
        if (text.Contains('\n', StringComparison.Ordinal))
        {
            var lines = text.Split('\n');
            var changed = false;
            for (var i = 0; i < lines.Length; ++i)
            {
                var line = TranslateSimple(lines[i]);
                changed |= !ReferenceEquals(line, lines[i]);
                lines[i] = line;
            }
            return changed ? string.Join('\n', lines) : text;
        }
        return text;
    }

    private static string TranslateSimple(string text)
    {
        if (text.Length == 0)
            return text;
        if (_exact.TryGetValue(text, out var exact))
            return exact;

        // surrounding whitespace
        var trimmed = text.AsSpan().Trim();
        if (trimmed.Length != text.Length && trimmed.Length > 0)
        {
            var start = text.IndexOf(trimmed[0]);
            var core = trimmed.ToString();
            var inner = TranslateSimple(core);
            return ReferenceEquals(inner, core) ? text : string.Concat(text.AsSpan(0, start), inner, text.AsSpan(start + core.Length));
        }

        foreach (var t in _templates)
        {
            if (text.Length < t.Prefix.Length + t.Suffix.Length || !text.StartsWith(t.Prefix, StringComparison.Ordinal) || !text.EndsWith(t.Suffix, StringComparison.Ordinal))
                continue;
            var m = t.Regex.Match(text);
            if (!m.Success)
                continue;
            var args = new object?[t.Order.Length == 0 ? 0 : t.Order.Max() + 1];
            var valid = true;
            for (var i = 0; i < t.Order.Length; ++i)
            {
                var arg = m.Groups[i + 1].Value;
                var translated = arg.Length < text.Length ? TranslateSimple(arg) : arg;
                // an untranslated value starting with a lowercase word is the tail of an unrelated english sentence, not a name or a number
                if (ReferenceEquals(translated, arg) && arg.Length > 0 && arg[0] is >= 'a' and <= 'z')
                    valid = false;
                args[t.Order[i]] = translated;
            }
            if (!valid)
                continue;
            try
            {
                return string.Format(t.Translation, args);
            }
            catch (FormatException)
            {
                return text;
            }
        }

        // trailing counter, eg "Tower 2" or "Raidwide x3"
        var numbered = NumberedRegex().Match(text);
        if (numbered.Success)
        {
            var baseText = numbered.Groups[1].Value;
            var baseTranslated = TranslateSimple(baseText);
            if (!ReferenceEquals(baseTranslated, baseText))
                return baseTranslated + numbered.Groups[2].Value;
        }

        // sequences built by modules, eg "Out -> In -> Back" or "A > B"
        foreach (var (separator, replacement) in _sequenceSeparators)
        {
            if (!text.Contains(separator, StringComparison.Ordinal))
                continue;
            var parts = text.Split(separator);
            var changed = false;
            for (var i = 0; i < parts.Length; ++i)
            {
                var part = TranslateSimple(parts[i]);
                changed |= !ReferenceEquals(part, parts[i]);
                parts[i] = part;
            }
            return changed ? string.Join(replacement, parts) : text;
        }

        return text;
    }

    private static readonly (string, string)[] _sequenceSeparators = [(" -> ", " → "), (" --> ", " → "), (" ---> ", " → "), (" > ", " > "), (" + ", " + "), (" / ", " / "), (", ", "，")];

    private static bool HasLetters(string text)
    {
        foreach (var c in text)
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
                return true;
        return false;
    }

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"^(.*?[^\d\s])(\s*(?:x?\d+|#\d+|\(\d+\)))$")]
    private static partial Regex NumberedRegex();
}
