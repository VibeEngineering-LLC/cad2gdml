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
        // Загрузка STEP-файла: создаёт STL в temp, затем считывает их
        public static List<Part> Load(string stepPath, out string errorCode, out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;

            // (1) Поиск python и скрипта
            string python = Environment.GetEnvironmentVariable("CAD2GDML_PYTHON");
            if (python != null && !File.Exists(python))
                python = null;
            if (python == null)
            {
                string fb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ocpenv", "Scripts", "python.exe");
                if (File.Exists(fb))
                    python = fb;
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
                errorMessage = "Модуль STEP не найден: положите каталог tools (step_export.py и ocpenv) рядом с программой.";
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

                Process proc = new Process();
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
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                bool exited = proc.WaitForExit(300000);
                if (!exited)
                {
                    try { proc.Kill(); }
                    catch { }
                    errorCode = "E_STEP_TIMEOUT";
                    errorMessage = "Чтение STEP заняло больше 5 минут.";
                    return null;
                }

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

                        Match mMsg = Regex.Match(lastJson, "\"message\"\\s*:\\s*\"([^\"]*)\"");
                        if (mMsg.Success)
                            eMsg = mMsg.Groups[1].Value;
                    }

                    if (eMsg == null)
                    {
                        string errText = sbErr.ToString();
                        eMsg = errText.Length > 300 ? errText.Substring(errText.Length - 300) : errText;
                    }

                    errorCode = eCode;
                    errorMessage = "STEP: " + eMsg;
                    return null;
                }

                // (5) Загрузка STL из temp
                string[] files = Directory.GetFiles(tempDir, "*.stl");
                Array.Sort(files, StringComparer.Ordinal);

                List<Part> parts = new List<Part>();
                for (int i = 0; i < files.Length; i++)
                {
                    Part p = null;
                    try
                    {
                        p = StlImporter.Load(files[i], 1.0);
                    }
                    catch
                    {
                        continue;
                    }

                    if (p == null) continue;

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
                // (6) Удаление временной папки
                try { Directory.Delete(tempDir, true); }
                catch { }
            }
        }
    }
}
