using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Import
{
    // Импортер Blender .blend через внешний blender.exe
    public static class BlendImporter
    {
        // Предупреждения последнего запуска
        private static readonly object WarnLock = new object();
        private static readonly List<string> _warns = new List<string>();

        public static List<string> LastWarnings
        {
            get { lock (WarnLock) { return new List<string>(_warns); } }
        }

        public static List<Part> Load(string blendPath, out string errorCode, out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;

            lock (WarnLock) { _warns.Clear(); }

            // (0) Файл должен существовать
            if (!File.Exists(blendPath))
            {
                errorCode = "E_BLEND_READ";
                errorMessage = "Файл .blend не найден: " + blendPath;
                return null;
            }

            // (1) Поиск blender.exe и скрипта
            string blender = FindBlender();
            string script = FindScript();

            if (blender == null || script == null)
            {
                errorCode = "E_BLEND_NO_RUNTIME";
                errorMessage = "Blender не найден. Установите Blender (blender.org) или задайте CAD2GDML_BLENDER; скрипт tools\\blend_export.py должен лежать рядом с программой.";
                return null;
            }

            // (2) Временная папка
            string tempDir = Path.Combine(Path.GetTempPath(), "cad2gdml_blend_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string objPath = Path.Combine(tempDir, "scene.obj");

            try
            {
                // (3) Запуск blender в фоновом режиме
                string args = "-b \"" + blendPath + "\" --python-exit-code 3 -P \"" + script + "\" -- \"" + objPath + "\"";
                ProcessStartInfo psi = new ProcessStartInfo(blender, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                StringBuilder sbOut = new StringBuilder();
                StringBuilder sbErr = new StringBuilder();

                Process proc;
                try
                {
                    proc = new Process();
                    proc.StartInfo = psi;
                    proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) sbOut.AppendLine(e.Data);
                    };
                    proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) sbErr.AppendLine(e.Data);
                    };
                    proc.Start();
                }
                catch (Exception ex)
                {
                    errorCode = "E_BLEND_NO_RUNTIME";
                    errorMessage = "Не удалось запустить Blender (" + ex.Message + "): проверьте переменную CAD2GDML_BLENDER.";
                    return null;
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                try
                {
                    bool exited = proc.WaitForExit(300000);
                    if (!exited)
                    {
                        try { proc.Kill(); }
                        catch { }
                        try { proc.WaitForExit(5000); }
                        catch { }
                        errorCode = "E_BLEND_TIMEOUT";
                        errorMessage = "Чтение .blend заняло больше 5 минут.";
                        return null;
                    }

                    proc.WaitForExit();

                    // Ненулевой код выхода
                    if (proc.ExitCode != 0)
                    {
                        string eCode = "E_BLEND_INTERNAL";
                        string eMsg = null;

                        // Последний JSON в stdout
                        string outText = sbOut.ToString();
                        string lastJson = null;
                        string[] lines = outText.Split('\n');
                        for (int i = lines.Length - 1; i >= 0; i--)
                        {
                            string t = lines[i].Trim();
                            if (t.Length > 0 && t[0] == '{')
                            {
                                lastJson = t;
                                break;
                            }
                        }

                        if (lastJson != null)
                        {
                            Match mCode = Regex.Match(lastJson, "\"error\"\\s*:\\s*\"([A-Za-z0-9_]+)\"");
                            if (mCode.Success)
                                eCode = mCode.Groups[1].Value;

                            Match mMsg = Regex.Match(lastJson, "\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                            if (mMsg.Success)
                                eMsg = mMsg.Groups[1].Value;
                        }

                        if (eMsg != null)
                            eMsg = JsonUnescape(eMsg);

                        if (eMsg == null)
                        {
                            string errText = sbErr.ToString();
                            eMsg = errText.Length > 300 ? errText.Substring(errText.Length - 300) : errText;
                        }

                        AddSkippedWarnings(sbOut.ToString(), sbErr.ToString());

                        errorCode = eCode;
                        errorMessage = "Blender: " + eMsg;
                        return null;
                    }

                    // Предупреждения "skipped"
                    AddSkippedWarnings(sbOut.ToString(), sbErr.ToString());

                    // (4) Загрузка OBJ
                    if (!File.Exists(objPath))
                    {
                        errorCode = "E_BLEND_INTERNAL";
                        errorMessage = "Blender не создал OBJ";
                        return null;
                    }

                    List<Part> parts;
                    try
                    {
                        IList<Part> loaded = ObjImporter.Load(objPath, 1.0);
                        parts = new List<Part>(loaded);
                    }
                    catch
                    {
                        parts = new List<Part>();
                    }

                    for (int i = 0; i < parts.Count; i++)
                        parts[i].SourceFile = blendPath;

                    if (parts.Count == 0)
                    {
                        errorCode = "E_BLEND_EMPTY";
                        errorMessage = "В .blend нет видимых mesh-объектов";
                        return null;
                    }

                    return parts;
                }
                finally
                {
                    proc.Dispose();
                }
            }
            finally
            {
                // (5) Удаление временной папки
                try { Directory.Delete(tempDir, true); }
                catch { }
            }
        }

        // Поиск blender.exe: env → ProgramFiles → PATH
        private static string FindBlender()
        {
            string env = Environment.GetEnvironmentVariable("CAD2GDML_BLENDER");
            if (env != null && File.Exists(env))
                return env;

            // ProgramFiles\Blender Foundation\Blender *
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (programFiles != null)
            {
                string bfDir = Path.Combine(programFiles, "Blender Foundation");
                if (Directory.Exists(bfDir))
                {
                    string[] dirs;
                    try { dirs = Directory.GetDirectories(bfDir); }
                    catch { dirs = new string[0]; }
                    Array.Sort(dirs, StringComparer.Ordinal);
                    // С конца — самая новая версия
                    for (int i = dirs.Length - 1; i >= 0; i--)
                    {
                        string name = Path.GetFileName(dirs[i]);
                        if (name.StartsWith("Blender ", StringComparison.Ordinal))
                        {
                            string candidate = Path.Combine(dirs[i], "blender.exe");
                            if (File.Exists(candidate))
                                return candidate;
                        }
                    }
                }
            }

            // Сканирование PATH
            string pathVar = Environment.GetEnvironmentVariable("PATH");
            if (pathVar != null)
            {
                string[] entries = pathVar.Split(Path.PathSeparator);
                for (int i = 0; i < entries.Length; i++)
                {
                    string dir = entries[i].Trim().Trim('"');
                    if (dir.Length == 0) continue;
                    string candidate;
                    try { candidate = Path.Combine(dir, "blender.exe"); }
                    catch (ArgumentException) { continue; }
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        // Поиск скрипта blend_export.py
        private static string FindScript()
        {
            string env = Environment.GetEnvironmentVariable("CAD2GDML_BLEND_SCRIPT");
            if (env != null && File.Exists(env))
                return env;

            string fb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "blend_export.py");
            if (File.Exists(fb))
                return fb;

            return null;
        }

        // Строки с "skipped" (макс. 5) в LastWarnings
        private static void AddSkippedWarnings(string outText, string errText)
        {
            int added = 0;
            string[] outLines = outText.Split('\n');
            for (int i = 0; i < outLines.Length && added < 5; i++)
            {
                string line = outLines[i].Trim();
                if (line.Length > 0 && line.IndexOf("skipped", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    lock (WarnLock) { _warns.Add("Blender: " + line); }
                    added++;
                }
            }
            if (added < 5)
            {
                string[] errLines = errText.Split('\n');
                for (int i = 0; i < errLines.Length && added < 5; i++)
                {
                    string line = errLines[i].Trim();
                    if (line.Length > 0 && line.IndexOf("skipped", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lock (WarnLock) { _warns.Add("Blender: " + line); }
                        added++;
                    }
                }
            }
        }

        // Декодирование JSON-эскейпов
        private static string JsonUnescape(string s)
        {
            if (s == null || s.Length == 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '\\' || i + 1 >= s.Length)
                {
                    sb.Append(s[i]);
                    continue;
                }
                char n = s[i + 1];
                switch (n)
                {
                    case '"': sb.Append('"'); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case '/': sb.Append('/'); i++; break;
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': sb.Append('\r'); i++; break;
                    case 't': sb.Append('\t'); i++; break;
                    case 'b': sb.Append('\b'); i++; break;
                    case 'f': sb.Append('\f'); i++; break;
                    case 'u':
                        if (i + 5 < s.Length)
                        {
                            int cp = 0;
                            bool ok = true;
                            for (int k = 0; k < 4; k++)
                            {
                                char h = s[i + 2 + k];
                                int d;
                                if (h >= '0' && h <= '9') d = h - '0';
                                else if (h >= 'a' && h <= 'f') d = 10 + (h - 'a');
                                else if (h >= 'A' && h <= 'F') d = 10 + (h - 'A');
                                else { ok = false; break; }
                                cp = cp * 16 + d;
                            }
                            if (ok)
                            {
                                sb.Append((char)cp);
                                i += 5;
                                break;
                            }
                        }
                        sb.Append('\\');
                        break;
                    default:
                        sb.Append('\\');
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
