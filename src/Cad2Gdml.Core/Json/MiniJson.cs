using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cad2Gdml.Json
{
    // Минимальный JSON-ридер/райтер без внешних пакетов
    public static class MiniJson
    {
        // Максимальная вложенность
        const int MaxDepth = 200;

        // Разбор JSON-текста в объект
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            int pos = 0;
            object result = ParseValue(text, ref pos, MaxDepth);
            SkipWs(text, ref pos);
            if (pos != text.Length)
                throw new FormatException("Лишние символы после значения. Позиция: " + pos);
            return result;
        }

        // Сериализация объекта в JSON-строку
        public static string Serialize(object value, bool indented)
        {
            var sb = new StringBuilder();
            SerializeValue(sb, value, indented, 0);
            return sb.ToString();
        }

        // Пропуск пробельных символов
        static void SkipWs(string text, ref int pos)
        {
            while (pos < text.Length)
            {
                char c = text[pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') pos++;
                else break;
            }
        }

        // Разбор произвольного значения с контролем глубины
        static object ParseValue(string text, ref int pos, int depth)
        {
            SkipWs(text, ref pos);
            if (pos >= text.Length)
                throw new FormatException("Неожиданный конец строки. Позиция: " + pos);

            char c = text[pos];
            if (c == 'n') return ParseNull(text, ref pos);
            if (c == 't') return ParseTrue(text, ref pos);
            if (c == 'f') return ParseFalse(text, ref pos);
            if (c == '"') return ParseString(text, ref pos);
            if (c == '{') return ParseObject(text, ref pos, depth);
            if (c == '[') return ParseArray(text, ref pos, depth);
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(text, ref pos);

            throw new FormatException("Неожиданный символ '" + c + "'. Позиция: " + pos);
        }

        // null
        static object ParseNull(string text, ref int pos)
        {
            ExpectWord(text, ref pos, "null");
            return null;
        }

        // true
        static object ParseTrue(string text, ref int pos)
        {
            ExpectWord(text, ref pos, "true");
            return true;
        }

        // false
        static object ParseFalse(string text, ref int pos)
        {
            ExpectWord(text, ref pos, "false");
            return false;
        }

        // Ожидание конкретного слова
        static void ExpectWord(string text, ref int pos, string word)
        {
            for (int i = 0; i < word.Length; i++)
            {
                if (pos + i >= text.Length || text[pos + i] != word[i])
                    throw new FormatException("Ожидалось '" + word + "'. Позиция: " + pos);
            }
            pos += word.Length;
        }

        // Разбор строки с поддержкой \uXXXX
        static string ParseString(string text, ref int pos)
        {
            pos++; // открывающая кавычка
            var sb = new StringBuilder();
            while (true)
            {
                if (pos >= text.Length)
                    throw new FormatException("Незакрытая строка. Позиция: " + pos);
                char c = text[pos];
                if (c == '"')
                {
                    pos++;
                    return sb.ToString();
                }
                if (c == '\\')
                {
                    pos++;
                    if (pos >= text.Length)
                        throw new FormatException("Незакрытый escape. Позиция: " + pos);
                    char e = text[pos];
                    switch (e)
                    {
                        case '"': sb.Append('"'); pos++; break;
                        case '\\': sb.Append('\\'); pos++; break;
                        case '/': sb.Append('/'); pos++; break;
                        case 'b': sb.Append('\b'); pos++; break;
                        case 'f': sb.Append('\f'); pos++; break;
                        case 'n': sb.Append('\n'); pos++; break;
                        case 'r': sb.Append('\r'); pos++; break;
                        case 't': sb.Append('\t'); pos++; break;
                        case 'u':
                            pos++;
                            if (pos + 4 > text.Length)
                                throw new FormatException("Неполный \\uXXXX. Позиция: " + pos);
                            string hex = text.Substring(pos, 4);
                            try
                            {
                                int code = Convert.ToInt32(hex, 16);
                                sb.Append((char)code);
                            }
                            catch (FormatException)
                            {
                                throw new FormatException("Некорректный \\uXXXX: " + hex + ". Позиция: " + pos);
                            }
                            pos += 4;
                            break;
                        default:
                            throw new FormatException("Неизвестный escape: \\" + e + ". Позиция: " + pos);
                    }
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }
        }

        // Разбор числа через InvariantCulture
        static double ParseNumber(string text, ref int pos)
        {
            int start = pos;
            if (pos < text.Length && text[pos] == '-') pos++;
            if (pos >= text.Length || text[pos] < '0' || text[pos] > '9')
                throw new FormatException("Ожидалась цифра. Позиция: " + pos);
            while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9') pos++;
            if (pos < text.Length && text[pos] == '.')
            {
                pos++;
                if (pos >= text.Length || text[pos] < '0' || text[pos] > '9')
                    throw new FormatException("Ожидалась цифра после '.'. Позиция: " + pos);
                while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9') pos++;
            }
            if (pos < text.Length && (text[pos] == 'e' || text[pos] == 'E'))
            {
                pos++;
                if (pos < text.Length && (text[pos] == '+' || text[pos] == '-')) pos++;
                if (pos >= text.Length || text[pos] < '0' || text[pos] > '9')
                    throw new FormatException("Ожидалась цифра в экспоненте. Позиция: " + pos);
                while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9') pos++;
            }
            string numStr = text.Substring(start, pos - start);
            try
            {
                return double.Parse(numStr, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new FormatException("Некорректное число: " + numStr + ". Позиция: " + start);
            }
        }

        // Разбор объекта { key: value, ... } с контролем глубины
        static Dictionary<string, object> ParseObject(string text, ref int pos, int depth)
        {
            if (depth <= 0)
                throw new FormatException("JSON: nesting too deep");
            pos++;
            var dict = new Dictionary<string, object>();
            SkipWs(text, ref pos);
            if (pos >= text.Length)
                throw new FormatException("Неожиданный конец в объекте. Позиция: " + pos);
            if (text[pos] == '}')
            {
                pos++;
                return dict;
            }
            while (true)
            {
                SkipWs(text, ref pos);
                if (pos >= text.Length)
                    throw new FormatException("Неожиданный конец в объекте. Позиция: " + pos);
                if (text[pos] != '"')
                    throw new FormatException("Ожидалась строка-ключ. Позиция: " + pos);
                string key = ParseString(text, ref pos);
                SkipWs(text, ref pos);
                if (pos >= text.Length || text[pos] != ':')
                    throw new FormatException("Ожидалось ':'. Позиция: " + pos);
                pos++;
                object val = ParseValue(text, ref pos, depth - 1);
                dict[key] = val;
                SkipWs(text, ref pos);
                if (pos >= text.Length)
                    throw new FormatException("Неожиданный конец в объекте. Позиция: " + pos);
                if (text[pos] == '}')
                {
                    pos++;
                    return dict;
                }
                if (text[pos] != ',')
                    throw new FormatException("Ожидалось ',' или '}'. Позиция: " + pos);
                pos++;
            }
        }

        // Разбор массива [ value, ... ] с контролем глубины
        static List<object> ParseArray(string text, ref int pos, int depth)
        {
            if (depth <= 0)
                throw new FormatException("JSON: nesting too deep");
            pos++;
            var list = new List<object>();
            SkipWs(text, ref pos);
            if (pos >= text.Length)
                throw new FormatException("Неожиданный конец в массиве. Позиция: " + pos);
            if (text[pos] == ']')
            {
                pos++;
                return list;
            }
            while (true)
            {
                object val = ParseValue(text, ref pos, depth - 1);
                list.Add(val);
                SkipWs(text, ref pos);
                if (pos >= text.Length)
                    throw new FormatException("Неожиданный конец в массиве. Позиция: " + pos);
                if (text[pos] == ']')
                {
                    pos++;
                    return list;
                }
                if (text[pos] != ',')
                    throw new FormatException("Ожидалось ',' или ']'. Позиция: " + pos);
                pos++;
            }
        }

        // Сериализация одного значения
        static void SerializeValue(StringBuilder sb, object value, bool indented, int depth)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            if (value is bool)
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }
            if (value is double)
            {
                sb.Append(((double)value).ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is float)
            {
                sb.Append(((float)value).ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is int)
            {
                sb.Append(((int)value).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is long)
            {
                sb.Append(((long)value).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is short)
            {
                sb.Append(((short)value).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is byte)
            {
                sb.Append(((byte)value).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is string)
            {
                SerializeString(sb, (string)value);
                return;
            }

            // Словарь
            Dictionary<string, object> dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                SerializeObject(sb, dict, indented, depth);
                return;
            }

            // Список
            List<object> list = value as List<object>;
            if (list != null)
            {
                SerializeArray(sb, list, indented, depth);
                return;
            }

            // IEnumerable<KeyValuePair<string,object>>
            var kvpEnumerable = value as IEnumerable<KeyValuePair<string, object>>;
            if (kvpEnumerable != null)
            {
                var kvpList = new List<KeyValuePair<string, object>>(kvpEnumerable);
                SerializeObjectKvp(sb, kvpList, indented, depth);
                return;
            }

            throw new FormatException("Неподдерживаемый тип: " + value.GetType().Name);
        }

        // Экранирование строки; UTF-8-символы не экранируются
        static void SerializeString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        // Сериализация Dictionary как JSON-объект
        static void SerializeObject(StringBuilder sb, Dictionary<string, object> dict, bool indented, int depth)
        {
            if (dict.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append('{');
            int i = 0;
            foreach (var kv in dict)
            {
                if (i > 0) sb.Append(',');
                if (indented)
                {
                    sb.Append('\n');
                    AppendIndent(sb, depth + 1);
                }
                SerializeString(sb, kv.Key);
                sb.Append(':');
                if (indented) sb.Append(' ');
                SerializeValue(sb, kv.Value, indented, depth + 1);
                i++;
            }
            if (indented)
            {
                sb.Append('\n');
                AppendIndent(sb, depth);
            }
            sb.Append('}');
        }

        // Сериализация List как JSON-массив
        static void SerializeArray(StringBuilder sb, List<object> list, bool indented, int depth)
        {
            if (list.Count == 0)
            {
                sb.Append("[]");
                return;
            }
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                if (indented)
                {
                    sb.Append('\n');
                    AppendIndent(sb, depth + 1);
                }
                SerializeValue(sb, list[i], indented, depth + 1);
            }
            if (indented)
            {
                sb.Append('\n');
                AppendIndent(sb, depth);
            }
            sb.Append(']');
        }

        // Сериализация IEnumerable<KVP> как JSON-объект
        static void SerializeObjectKvp(StringBuilder sb, List<KeyValuePair<string, object>> kvps, bool indented, int depth)
        {
            if (kvps.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append('{');
            int i = 0;
            foreach (var kv in kvps)
            {
                if (i > 0) sb.Append(',');
                if (indented)
                {
                    sb.Append('\n');
                    AppendIndent(sb, depth + 1);
                }
                SerializeString(sb, kv.Key);
                sb.Append(':');
                if (indented) sb.Append(' ');
                SerializeValue(sb, kv.Value, indented, depth + 1);
                i++;
            }
            if (indented)
            {
                sb.Append('\n');
                AppendIndent(sb, depth);
            }
            sb.Append('}');
        }

        // Отступ в два пробела
        static void AppendIndent(StringBuilder sb, int level)
        {
            for (int i = 0; i < level; i++)
                sb.Append("  ");
        }
    }
}
