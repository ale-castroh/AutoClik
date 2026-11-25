using System.Globalization;
using System.Text;

namespace Autoclicker.Persistence;

/// <summary>
/// Lector/escritor JSON mínimo (sin dependencias): objetos → Dictionary&lt;string, object?&gt;,
/// listas → List&lt;object?&gt;, números → double, más string, bool y null.
/// Suficiente para settings.json y compatible con el formato que escribían las versiones anteriores.
/// </summary>
internal static class MiniJson
{
    public static object? Parse(string text)
    {
        var p = new Parser(text);
        object? value = p.ReadValue();
        p.SkipWhitespace();
        if (!p.AtEnd) throw new FormatException("JSON: contenido extra al final");
        return value;
    }

    public static string Serialize(object? value)
    {
        var sb = new StringBuilder();
        Write(sb, value, 0);
        return sb.ToString();
    }

    // ---------------- Escritura ----------------

    private static void Write(StringBuilder sb, object? value, int indent)
    {
        switch (value)
        {
            case null: sb.Append("null"); break;
            case string s: WriteString(sb, s); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case Enum e: WriteString(sb, e.ToString()); break;
            case int or long or uint or short or byte:
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
            case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
            case IDictionary<string, object?> dict:
                if (dict.Count == 0) { sb.Append("{}"); break; }
                sb.Append("{\n");
                int i = 0;
                foreach (var kv in dict)
                {
                    sb.Append(' ', indent + 2);
                    WriteString(sb, kv.Key);
                    sb.Append(": ");
                    Write(sb, kv.Value, indent + 2);
                    sb.Append(++i < dict.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent).Append('}');
                break;
            case System.Collections.IEnumerable list:
                var items = list.Cast<object?>().ToList();
                if (items.Count == 0) { sb.Append("[]"); break; }
                sb.Append("[\n");
                for (int k = 0; k < items.Count; k++)
                {
                    sb.Append(' ', indent + 2);
                    Write(sb, items[k], indent + 2);
                    sb.Append(k + 1 < items.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent).Append(']');
                break;
            default:
                WriteString(sb, value.ToString() ?? ""); break;
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    // ---------------- Lectura ----------------

    private sealed class Parser(string text)
    {
        private int _i;

        public bool AtEnd => _i >= text.Length;

        public void SkipWhitespace()
        {
            while (_i < text.Length && char.IsWhiteSpace(text[_i])) _i++;
            // Algunos editores guardan con BOM al principio
            if (_i == 0 && text.Length > 0 && text[0] == '﻿') { _i++; SkipWhitespace(); }
        }

        public object? ReadValue()
        {
            SkipWhitespace();
            if (AtEnd) throw new FormatException("JSON: fin inesperado");
            char c = text[_i];
            switch (c)
            {
                case '{': return ReadObject();
                case '[': return ReadArray();
                case '"': return ReadString();
                case 't': Expect("true"); return true;
                case 'f': Expect("false"); return false;
                case 'n': Expect("null"); return null;
                default: return ReadNumber();
            }
        }

        private Dictionary<string, object?> ReadObject()
        {
            var dict = new Dictionary<string, object?>();
            _i++; // {
            SkipWhitespace();
            if (Peek('}')) { _i++; return dict; }
            while (true)
            {
                SkipWhitespace();
                string key = ReadString();
                SkipWhitespace();
                Consume(':');
                dict[key] = ReadValue();
                SkipWhitespace();
                if (Peek(',')) { _i++; continue; }
                Consume('}');
                return dict;
            }
        }

        private List<object?> ReadArray()
        {
            var list = new List<object?>();
            _i++; // [
            SkipWhitespace();
            if (Peek(']')) { _i++; return list; }
            while (true)
            {
                list.Add(ReadValue());
                SkipWhitespace();
                if (Peek(',')) { _i++; continue; }
                Consume(']');
                return list;
            }
        }

        private string ReadString()
        {
            Consume('"');
            var sb = new StringBuilder();
            while (true)
            {
                if (AtEnd) throw new FormatException("JSON: cadena sin cerrar");
                char c = text[_i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                char e = text[_i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)int.Parse(text.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        _i += 4;
                        break;
                    default: throw new FormatException("JSON: escape no válido");
                }
            }
        }

        private double ReadNumber()
        {
            int start = _i;
            while (_i < text.Length && "+-0123456789.eE".IndexOf(text[_i]) >= 0) _i++;
            if (start == _i) throw new FormatException("JSON: valor no válido");
            return double.Parse(text.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private void Expect(string word)
        {
            if (string.CompareOrdinal(text, _i, word, 0, word.Length) != 0) throw new FormatException("JSON: valor no válido");
            _i += word.Length;
        }

        private bool Peek(char c) => _i < text.Length && text[_i] == c;

        private void Consume(char c)
        {
            if (!Peek(c)) throw new FormatException($"JSON: se esperaba '{c}'");
            _i++;
        }
    }
}

/// <summary>Lectura cómoda y tolerante de un objeto JSON (valores ausentes o de otro tipo → valor por defecto).</summary>
internal readonly struct JsonObj(Dictionary<string, object?>? dict)
{
    public static JsonObj From(object? value) => new(value as Dictionary<string, object?>);

    public bool Has(string key) => dict != null && dict.ContainsKey(key) && dict[key] != null;

    private object? Raw(string key) => dict != null && dict.TryGetValue(key, out var v) ? v : null;

    public string? Str(string key, string? fallback = null) => Raw(key) as string ?? fallback;

    public double Num(string key, double fallback) => Raw(key) is double d ? d : fallback;

    public int Int(string key, int fallback) =>
        Raw(key) is double d && d >= int.MinValue && d <= int.MaxValue ? (int)d : fallback;

    public int? IntOrNull(string key) => Raw(key) is double d && d >= int.MinValue && d <= int.MaxValue ? (int)d : null;

    public long Long(string key, long fallback) => Raw(key) is double d ? (long)d : fallback;

    public bool Bool(string key, bool fallback) => Raw(key) is bool b ? b : fallback;

    public double? NumOrNull(string key) => Raw(key) is double d ? d : null;

    /// <summary>Enum guardado como texto ("Seconds") o como número.</summary>
    public T Enum<T>(string key, T fallback) where T : struct =>
        EnumOrNull<T>(key) ?? fallback;

    public T? EnumOrNull<T>(string key) where T : struct
    {
        object? raw = Raw(key);
        if (raw is string s && System.Enum.TryParse(s, ignoreCase: true, out T parsed) && System.Enum.IsDefined(typeof(T), parsed)) return parsed;
        if (raw is double d && System.Enum.IsDefined(typeof(T), (int)d)) return (T)System.Enum.ToObject(typeof(T), (int)d);
        return null;
    }

    public JsonObj Obj(string key) => From(Raw(key));

    public IEnumerable<KeyValuePair<string, object?>> Entries => dict ?? new Dictionary<string, object?>();

    public IEnumerable<JsonObj> Array(string key) =>
        Raw(key) is List<object?> list ? list.Select(From) : Enumerable.Empty<JsonObj>();
}
