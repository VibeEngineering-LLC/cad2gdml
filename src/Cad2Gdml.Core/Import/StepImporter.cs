using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Import
{
    // Импортер STEP через внешний Python-скрипт
    public static class StepImporter
    {
        // Предупреждения последнего запуска
        private static readonly object WarnLock = new object();
        private static readonly List<string> _warns = new List<string>();

        public static List<string> LastWarnings
        {
            get { lock (WarnLock) { return new List<string>(_warns); } }
        }

        // Загрузка STEP-файла: создаёт STL в temp, затем считывает их
        public static List<Part> Load(string stepPath, out string errorCode, out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;

            // Очищаем предупреждения
            lock (WarnLock)
            {
                _warns.Clear();
            }

            // (0) Проверка существования входного файла
            if (!File.Exists(stepPath))
            {
                errorCode = "E_STEP_READ";
                errorMessage = "Файл STEP не найден: " + stepPath;
                return null;
            }

            // (1) Поиск python и скрипта
            string python = Environment.GetEnvironmentVariable("CAD2GDML_PYTHON");
            if (python != null && !File.Exists(python))
                python = null;
            if (python == null)
            {
                string fb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ocpenv", "Scripts", "python.exe");
                if (File.Exists(fb))
                    python = fb;
                else if (Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "pylib")))
                    python = FindPythonOnPath();
                else
                    python = null;
            }

            string script = Environment.GetEnvironmentVariable("CAD2GDML_STEP_SCRIPT");
            if (script != null && !File.Exists(script))
                script = null;
            if (script == null)
            {
                string fb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "step_export.py");
                if (File.Exists(fb))
                    script = fb;
                else
                    script = null;
            }

            if (python == null || script == null)
            {
                errorCode = "E_STEP_NO_RUNTIME";
                errorMessage = "Модуль STEP не найден. Нужен Python 3.12 (python.exe в PATH или переменная CAD2GDML_PYTHON).";
                return null;
            }

            // (2) Уникальная временная папка
            string tempDir = Path.Combine(Path.GetTempPath(), "cad2gdml_step_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // (3) Запуск процесса
                string args = "\"" + script + "\" \"" + stepPath + "\" \"" + tempDir + "\"";
                ProcessStartInfo psi = new ProcessStartInfo(python, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";

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
                    // Не удалось запустить (не исполняемый файл и т.п.)
                    errorCode = "E_STEP_NO_RUNTIME";
                    errorMessage = "Не удалось запустить модуль STEP (" + ex.Message + "): проверьте переменную CAD2GDML_PYTHON.";
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
                        errorCode = "E_STEP_TIMEOUT";
                        errorMessage = "Чтение STEP заняло больше 5 минут.";
                        return null;
                    }

                    // Дожидаемся доставки всех асинхронных строк
                    proc.WaitForExit();

                    // (4) Ненулевой код выхода
                    if (proc.ExitCode != 0)
                    {
                        string eCode = "E_STEP_INTERNAL";
                        string eMsg = null;

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

                        // Декодирование unicode-эскейпов в сообщении
                        if (eMsg != null)
                        {
                            eMsg = JsonUnescape(eMsg);
                        }

                        if (eMsg == null)
                        {
                            string errText = sbErr.ToString();
                            eMsg = errText.Length > 300 ? errText.Substring(errText.Length - 300) : errText;
                        }

                        // Собираем строки с "skipped"
                        AddSkippedWarnings(sbOut.ToString(), sbErr.ToString());

                        errorCode = eCode;
                        errorMessage = "STEP: " + eMsg;
                        return null;
                    }

                    // Собираем строки с "skipped" из stdout/stderr
                    AddSkippedWarnings(sbOut.ToString(), sbErr.ToString());

                    // (5) Загрузка STL из temp
                    string[] files = Directory.GetFiles(tempDir, "*.stl");
                    Array.Sort(files, StringComparer.Ordinal);

                    List<Part> parts = new List<Part>();
                    for (int i = 0; i < files.Length; i++)
                    {
                        Part p = null;
                        bool failed = false;
                        try
                        {
                            p = StlImporter.Load(files[i], 1.0);
                        }
                        catch
                        {
                            failed = true;
                        }

                        if (failed || p == null)
                        {
                            // Фиксируем предупреждение
                            string warnName = Path.GetFileNameWithoutExtension(files[i]);
                            lock (WarnLock)
                            {
                                _warns.Add("STEP: не удалось прочитать деталь " + warnName);
                            }
                            continue;
                        }

                        // Убираем префикс NNN_ из имени
                        string fn = Path.GetFileNameWithoutExtension(files[i]);
                        fn = Regex.Replace(fn, "^\\d+_", "");
                        p.Name = fn;
                        p.SourceFile = stepPath;
                        parts.Add(p);
                    }

                    if (parts.Count == 0)
                    {
                        errorCode = "E_STEP_EMPTY";
                        errorMessage = "В STEP не найдено тел.";
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
                // (6) Удаление временной папки
                try { Directory.Delete(tempDir, true); }
                catch { }
            }
        }

        // Добавляет строки, содержащие "skipped" (не более 5), в список
        private static void AddSkippedWarnings(string outText, string errText)
        {
            int added = 0;
            string[] outLines = outText.Split('\n');
            for (int i = 0; i < outLines.Length && added < 5; i++)
            {
                string line = outLines[i].Trim();
                if (line.Length > 0 && line.IndexOf("skipped", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    lock (WarnLock)
                    {
                        _warns.Add("STEP: " + line);
                    }
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
                        lock (WarnLock)
                        {
                            _warns.Add("STEP: " + line);
                        }
                        added++;
                    }
                }
            }
        }

        // Поиск python.exe 3.12 в стандартных путях и в PATH
        private static string FindPythonOnPath()
        {
            // Стандартные расположения Python 3.12
            string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (localAppData != null)
            {
                string c = Path.Combine(localAppData, "Programs", "Python", "Python312", "python.exe");
                if (File.Exists(c)) return c;
            }

            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (programFiles != null)
            {
                string c = Path.Combine(programFiles, "Python312", "python.exe");
                if (File.Exists(c)) return c;
            }

            string c3 = @"C:\Python312\python.exe";
            if (File.Exists(c3)) return c3;

            // Сканирование PATH: принимаем только каталоги с меткой 3.12
            string pathVar = Environment.GetEnvironmentVariable("PATH");
            if (pathVar == null) return null;

            string[] entries = pathVar.Split(Path.PathSeparator);
            for (int i = 0; i < entries.Length; i++)
            {
                string dir = entries[i].Trim().Trim('"');
                if (dir.Length == 0) continue;

                string candidate;
                try
                {
                    candidate = Path.Combine(dir, "python.exe");
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (!File.Exists(candidate)) continue;

                // Проверяем, что путь содержит метку Python 3.12
                if (dir.IndexOf("Python312", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dir.IndexOf("Python3.12", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dir.IndexOf("python-3.12", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }
            return null;
        }

        // Декодирование JSON-эскейпов; не бросает исключений
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
