using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Cli
{
    // Точка входа CLI
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Кодировка вывода
            Console.OutputEncoding = new UTF8Encoding(false);

            // Справка
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--help" || args[i] == "-h")
                {
                    PrintUsage();
                    return 0;
                }
            }

            if (args.Length == 0)
            {
                PrintUsage();
                return 0;
            }

            // Разбор аргументов
            var inputs = new List<string>();
            string outDir = null;
            string baseName = null;
            string unit = null;
            string crystal = null;
            string front = null;
            string materials = null;
            bool allowDefault = false;
            bool dropOpen = false;
            bool keepOpen = false;
            bool noSplit = false;
            double weldTol = 0.0;
            var matOverrides = new List<KeyValuePair<string, string>>();

            int idx = 0;
            while (idx < args.Length)
            {
                string a = args[idx];

                if (a == "--out")
                {
                    if (idx + 1 >= args.Length) { Console.Error.WriteLine("Ожидается значение для --out."); return 2; }
                    outDir = args[++idx];
                }
                else if (a == "--name")
                {
                    if (idx + 1 >= args.Length) { Console.Error.WriteLine("Ожидается значение для --name."); return 2; }
                    baseName = args[++idx];
                }
                else if (a == "--unit")
                {
                    if (idx + 1 >= args.Length) { Console.Error.WriteLine("Ожидается значение для --unit."); return 2; }
                    unit = args[++idx];
                }
                else if (a == "--crystal")
                {
                    if (idx + 1 >= args.Length) { Console.Error.WriteLine("Ожидается значение для --crystal."); return 2; }
                    crystal = args[++idx];
                }
                else if (a == "--front")
                {
                    if (idx + 1 >= args.Length) { Console.Error.WriteLine("Ожидается значение для --front."); return 2; }
                    front = args[++idx];
                }
                else if (a == "--materials")
                {
                    if (idx + 1 >= args.Length) { Console.Error.WriteLine("Ожидается значение для --materials."); return 2; }
                    materials = args[++idx];
                }
                else if (a == "--weld")
                {
                    if (idx + 1 >= args.Length) { Console.Error.WriteLine("Ожидается значение для --weld."); return 2; }
                    string wval = args[++idx];
                    double parsed;
                    if (!double.TryParse(wval, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    {
                        Console.Error.WriteLine("Недопустимое значение --weld: " + wval);
                        return 2;
                    }
                    if (parsed <= 0.0 || parsed > 1.0)
                    {
                        Console.Error.WriteLine("Значение --weld должно быть > 0 и <= 1: " + parsed.ToString(CultureInfo.InvariantCulture));
                        return 2;
                    }
                    weldTol = parsed;
                }
                else if (a == "--material")
                {
                    if (idx + 1 >= args.Length) { Console.Error.WriteLine("Ожидается значение для --material."); return 2; }
                    string mval = args[++idx];
                    int sep = mval.LastIndexOf('=');
                    if (sep <= 0 || sep >= mval.Length - 1)
                    {
                        Console.Error.WriteLine("Формат --material: <имя>=<вещество>. Получено: " + mval);
                        return 2;
                    }
                    string partName = mval.Substring(0, sep).Trim();
                    string matName = mval.Substring(sep + 1).Trim();
                    if (partName.Length == 0 || matName.Length == 0)
                    {
                        Console.Error.WriteLine("Обе части --material после trim должны быть непустыми: " + mval);
                        return 2;
                    }
                    matOverrides.Add(new KeyValuePair<string, string>(partName, matName));
                }
                else if (a == "--allow-default-material")
                {
                    allowDefault = true;
                }
                else if (a == "--keep-open")
                {
                    keepOpen = true;
                }
                else if (a == "--drop-open")
                {
                    dropOpen = true;
                }
                else if (a == "--no-split")
                {
                    noSplit = true;
                }
                else if (a.Length > 2 && a[0] == '-' && a[1] == '-')
                {
                    Console.Error.WriteLine("Неизвестный параметр: " + a);
                    return 2;
                }
                else
                {
                    inputs.Add(a);
                }

                idx++;
            }

            // Валидация обязательных полей
            if (outDir == null)
            {
                Console.Error.WriteLine("Не задан --out.");
                return 2;
            }
            if (inputs.Count == 0)
            {
                Console.Error.WriteLine("Не заданы входные файлы.");
                return 2;
            }
            if (keepOpen && dropOpen)
            {
                Console.Error.WriteLine("--keep-open и --drop-open несовместимы.");
                return 2;
            }

            // Конвертация единиц
            double unitToMm = 0.0;
            if (unit != null)
            {
                if (unit == "mm") unitToMm = 1.0;
                else if (unit == "cm") unitToMm = 10.0;
                else if (unit == "m") unitToMm = 1000.0;
                else if (unit == "inch") unitToMm = 25.4;
                else
                {
                    Console.Error.WriteLine("Недопустимые единицы: " + unit);
                    return 2;
                }
            }

            // Проверка --front: допустимы только +z, -z, auto
            if (front != null)
            {
                if (front != "+z" && front != "-z" && front != "auto")
                {
                    Console.Error.WriteLine("неизвестное значение --front: " + front + " (допустимо: +z, -z, auto)");
                    return 2;
                }
            }

            // Сборка опций конвейера
            var opt = new PipelineOptions
            {
                Inputs = inputs,
                OutDir = outDir,
                BaseName = baseName,
                UnitToMm = unitToMm,
                UnitName = unit,
                CrystalName = crystal,
                FrontSign = front,
                MaterialsJsonPath = materials,
                AllowDefaultMaterial = allowDefault,
                DropOpen = dropOpen,
                KeepOpen = keepOpen,
                NoSplit = noSplit,
                WeldTolMm = weldTol,
                MaterialOverrides = matOverrides
            };

            // Запуск конвейера
            PipelineResult result;
            try
            {
                result = Cad2GdmlPipeline.Run(opt);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Внутренняя ошибка: " + ex.Message);
                return 9;
            }

            // Печать итога
            PrintSummary(result);

            return result.ExitCode;
        }

        // Вывод справки
        private static void PrintUsage()
        {
            Console.WriteLine("Использование: cad2gdml <вход>... --out <каталог> [параметры]");
            Console.WriteLine();
            Console.WriteLine("Параметры:");
            Console.WriteLine("  --out <каталог>            Каталог вывода (обязательно)");
            Console.WriteLine("  --name <база>              Базовое имя выходных файлов");
            Console.WriteLine("  --unit mm|cm|m|inch        Единицы входных файлов");
            Console.WriteLine("  --crystal <имя>            Имя детали-кристалла");
            Console.WriteLine("  --front +z|-z|auto         Направление передней грани");
            Console.WriteLine("  --materials <файл.json>    Файл библиотеки веществ");
            Console.WriteLine("  --weld <мм>                Допуск сшивки вершин (по умолчанию 1e-5)");
            Console.WriteLine("  --material <имя>=<вещество>  Назначить вещество деталям, в имени которых есть <имя> (повторяемый)");
            Console.WriteLine("  --allow-default-material   Разрешить вещество по умолчанию");
            Console.WriteLine("  --drop-open                Отбрасывать открытые сетки (неключевые)");
            Console.WriteLine("  --keep-open                Оставлять открытые сетки с предупреждением");
            Console.WriteLine("  --no-split                 Не разбивать деталь на связные тела");
            Console.WriteLine("  --help, -h                 Эта справка");
        }

        // Итоговая печать: счётчики, пути, sha256, диагностика
        private static void PrintSummary(PipelineResult result)
        {
            Console.WriteLine("=== cad2gdml: итог ===");
            Console.WriteLine("Код выхода: " + result.ExitCode.ToString(CultureInfo.InvariantCulture));

            // Счётчики GDML
            if (result.Gdml != null)
            {
                Console.WriteLine("Объёмов: " + result.Gdml.Volumes.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("Тесселированных: " + result.Gdml.Tessellated.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("Треугольников: " + result.Gdml.Triangles.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("Труб: " + result.Gdml.Tubes.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("Поликонусов: " + result.Gdml.Polycones.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("Боксов: " + result.Gdml.Boxes.ToString(CultureInfo.InvariantCulture));
            }

            // Пути выходных файлов
            if (!string.IsNullOrEmpty(result.GdmlPath))
                Console.WriteLine("GDML: " + result.GdmlPath);
            if (!string.IsNullOrEmpty(result.PngPath))
                Console.WriteLine("PNG: " + result.PngPath);
            if (!string.IsNullOrEmpty(result.ReportPath))
                Console.WriteLine("Отчёт: " + result.ReportPath);

            // SHA-256
            if (result.Gdml != null && !string.IsNullOrEmpty(result.Gdml.Sha256))
                Console.WriteLine("SHA-256: " + result.Gdml.Sha256);

            // Диагностика
            if (result.Diagnostics != null && result.Diagnostics.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Диагностика:");
                for (int i = 0; i < result.Diagnostics.Count; i++)
                {
                    Diagnostic d = result.Diagnostics[i];
                    Console.WriteLine("  " + d.Code + " [" + d.Severity.ToString() + "] "
                        + (string.IsNullOrEmpty(d.PartName) ? "" : d.PartName + ": ")
                        + d.Message);
                }
            }

            // Финальная строка: код, ID ошибки, сообщение
            Console.WriteLine();
            if (result.ExitCode == 0)
            {
                Console.WriteLine("cad2gdml: код 0 OK");
            }
            else
            {
                string id = (!string.IsNullOrEmpty(result.MainErrorId)) ? result.MainErrorId : "E_UNKNOWN";
                string msg = (!string.IsNullOrEmpty(result.MainErrorMessage)) ? result.MainErrorMessage : "";
                Console.WriteLine("cad2gdml: код " + result.ExitCode.ToString(CultureInfo.InvariantCulture) + " " + id + " — " + msg);
            }
        }
    }
}
