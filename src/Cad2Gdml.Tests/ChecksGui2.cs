using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Cad2Gdml.Tests
{
    // GUI-проверка замены выбранной детали (#CG-37)
    public static class ChecksGui2
    {
        // ── P/Invoke (user32) ──
        private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr hWnd, int nIDDlgItem);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        private const uint WM_SETTEXT = 0x000C;
        private const uint BM_CLICK = 0x00F5;

        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("gui_replace_button", () => Run(tempDir)));
            return list;
        }

        private static void Run(string tempDir)
        {
            Exception captured = null;
            var t = new Thread(delegate ()
            {
                try { DoCheck(tempDir); }
                catch (Exception ex) { captured = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            if (!t.Join(TimeSpan.FromSeconds(150)))
                throw new InvalidOperationException("gui_replace_button: тайм-аут 150 с");
            if (captured != null) throw captured;
        }

        private static void DoCheck(string tempDir)
        {
            string inDir = Path.Combine(tempDir, "gui2", "in");
            string outDir = Path.Combine(tempDir, "gui2", "out");
            Directory.CreateDirectory(inDir);
            Directory.CreateDirectory(outDir);

            // Входные STL
            string crystalPath = Path.Combine(inDir, "Crystal_NaI.stl");
            string housingPath = Path.Combine(inDir, "Housing.stl");
            string screwAPath = Path.Combine(inDir, "Screw_A.stl");
            string screwBPath = Path.Combine(inDir, "Screw_B.stl");

            Fixtures.WriteBinaryStl(crystalPath, Fixtures.Box(10, 10, 30, 0, 0, 0));
            Fixtures.WriteBinaryStl(housingPath, Fixtures.Box(20, 20, 50, 0, 0, 0));
            Fixtures.WriteBinaryStl(screwAPath, Fixtures.Box(3, 3, 6, 30, 0, 0));
            Fixtures.WriteBinaryStl(screwBPath, Fixtures.Box(4, 4, 9, 30, 0, 0));

            // Форма вне экрана
            var form = new Cad2Gdml.App.MainForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();

            Type ft = typeof(Cad2Gdml.App.MainForm);

            // Список входов
            var lst = (ListBox)GetPriv(ft, form, "_lstInputs");
            lst.Items.Add(crystalPath);
            lst.Items.Add(housingPath);
            lst.Items.Add(screwAPath);

            // Папка вывода
            ((TextBox)GetPriv(ft, form, "_txtOutDir")).Text = outDir;

            // Фронт: "+z"
            var cmbFront = (ComboBox)GetPriv(ft, form, "_cmbFront");
            cmbFront.SelectedIndex = FindComboIndex(cmbFront, "+z", 0);

            // Разрешить вещество по умолчанию
            ((CheckBox)GetPriv(ft, form, "_chkAllowDefault")).Checked = true;

            // Ссылки для ассертов
            var btn = (Button)GetPriv(ft, form, "_btnConvert");
            var lbl = (Label)GetPriv(ft, form, "_lblStatus");

            MethodInfo miConvert = ft.GetMethod("OnConvert", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo miReplace = ft.GetMethod("OnReplaceSelected", BindingFlags.NonPublic | BindingFlags.Instance);

            // ── Шаг 1: первый прогон ──
            miConvert.Invoke(form, new object[] { null, EventArgs.Empty });
            WaitEnabled(btn, 60);

            string gdmlPath = FindGdml(outDir);
            if (gdmlPath == null) throw new InvalidOperationException("gui_replace_button: GDML не найден (шаг 1)");
            string gdmlText = File.ReadAllText(gdmlPath);
            if (!gdmlText.Contains("Screw_A")) throw new InvalidOperationException("gui_replace_button: GDML не содержит Screw_A (шаг 1)");
            if (gdmlText.Contains("Screw_B")) throw new InvalidOperationException("gui_replace_button: GDML содержит Screw_B (шаг 1)");

            int nBefore = XDocument.Load(gdmlPath).Descendants().Count(e => e.Name.LocalName == "physvol");

            // ── Шаг 2: отрицательные ──
            // 2a — ничего не выбрано
            lst.ClearSelected();
            miReplace.Invoke(form, new object[] { null, EventArgs.Empty });
            Application.DoEvents();
            if (lst.Items.Count != 3) throw new InvalidOperationException("gui_replace_button: список изменился (2a), count=" + lst.Items.Count);
            string st2a = lbl.Text;
            if (!st2a.Contains("Выберите")) throw new InvalidOperationException("gui_replace_button: статус не содержит \"Выберите\" (2a): " + st2a);

            // 2b — выбрано два
            lst.ClearSelected();
            lst.SetSelected(0, true);
            lst.SetSelected(1, true);
            miReplace.Invoke(form, new object[] { null, EventArgs.Empty });
            Application.DoEvents();
            if (lst.Items.Count != 3) throw new InvalidOperationException("gui_replace_button: список изменился (2b), count=" + lst.Items.Count);
            string st2b = lbl.Text;
            if (!st2b.Contains("ровно один")) throw new InvalidOperationException("gui_replace_button: статус не содержит \"ровно один\" (2b): " + st2b);

            // ── Шаг 3: положительная замена ──
            lst.ClearSelected();
            lst.SetSelected(2, true);

            // Потох-помощник: управляет диалогом выбора файла
            var helperDone = new ManualResetEventSlim(false);
            string helperError = null;
            int curPid = System.Diagnostics.Process.GetCurrentProcess().Id;

            var helper = new Thread(delegate ()
            {
                try
                {
                    int waited = 0;
                    bool found = false;
                    while (waited < 30000 && !found)
                    {
                        IntPtr hDlg = IntPtr.Zero;
                        EnumWindows(delegate (IntPtr hWnd, IntPtr l)
                        {
                            if (!IsWindowVisible(hWnd)) return true;
                            uint p;
                            GetWindowThreadProcessId(hWnd, out p);
                            if ((int)p != curPid) return true;
                            var sb = new StringBuilder(256);
                            GetClassName(hWnd, sb, sb.Capacity);
                            if (sb.ToString() == "#32770") { hDlg = hWnd; return false; }
                            return true;
                        }, IntPtr.Zero);

                        if (hDlg != IntPtr.Zero)
                        {
                            IntPtr hEdit = IntPtr.Zero;
                            EnumChildWindows(hDlg, delegate (IntPtr hWnd, IntPtr l)
                            {
                                var sb = new StringBuilder(256);
                                GetClassName(hWnd, sb, sb.Capacity);
                                if (sb.ToString() == "Edit") { hEdit = hWnd; return false; }
                                return true;
                            }, IntPtr.Zero);

                            if (hEdit != IntPtr.Zero)
                            {
                                SendMessage(hEdit, WM_SETTEXT, IntPtr.Zero, screwBPath);
                                IntPtr hOk = GetDlgItem(hDlg, 1);
                                if (hOk != IntPtr.Zero)
                                    SendMessage(hOk, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                                found = true;
                            }
                        }

                        if (!found)
                        {
                            Thread.Sleep(100);
                            waited += 100;
                        }
                    }
                    if (!found) helperError = "диалог выбора файла не найден";
                }
                catch (Exception ex) { helperError = ex.Message; }
                finally { helperDone.Set(); }
            });
            helper.Start();

            // Вызов замены (блокируется в ShowDialog до закрытия диалога)
            miReplace.Invoke(form, new object[] { null, EventArgs.Empty });
            helperDone.Wait(TimeSpan.FromSeconds(35));
            helper.Join();

            if (helperError != null) throw new InvalidOperationException("gui_replace_button: " + helperError);

            // Проверка списка после замены
            var items = lst.Items.Cast<object>().Select(o => o.ToString()).ToList();
            if (items.Count != 3) throw new InvalidOperationException("gui_replace_button: список != 3 (шаг 3): " + string.Join(",", items));
            if (items[0] != crystalPath) throw new InvalidOperationException("gui_replace_button: [0] != Crystal_NaI (шаг 3): " + items[0]);
            if (items[1] != housingPath) throw new InvalidOperationException("gui_replace_button: [1] != Housing (шаг 3): " + items[1]);
            if (items[2] != screwBPath) throw new InvalidOperationException("gui_replace_button: [2] != Screw_B (шаг 3): " + items[2]);

            string st3 = lbl.Text;
            if (!st3.StartsWith("Заменено")) throw new InvalidOperationException("gui_replace_button: статус не начинается с \"Заменено\" (шаг 3): " + st3);

            // ── Шаг 4: повторный прогон ──
            miConvert.Invoke(form, new object[] { null, EventArgs.Empty });
            WaitEnabled(btn, 60);

            string gdmlPath2 = FindGdml(outDir);
            if (gdmlPath2 == null) throw new InvalidOperationException("gui_replace_button: GDML не найден (шаг 4)");
            string gdmlText2 = File.ReadAllText(gdmlPath2);
            if (!gdmlText2.Contains("Screw_B")) throw new InvalidOperationException("gui_replace_button: GDML не содержит Screw_B (шаг 4)");
            if (gdmlText2.Contains("Screw_A")) throw new InvalidOperationException("gui_replace_button: GDML содержит Screw_A (шаг 4)");

            int nAfter = XDocument.Load(gdmlPath2).Descendants().Count(e => e.Name.LocalName == "physvol");
            if (nAfter != nBefore) throw new InvalidOperationException("gui_replace_button: physvol " + nAfter + " != " + nBefore + " (шаг 4)");

            // Освобождение
            form.Close();
            form.Dispose();
        }

        // Чтение приватного поля
        private static object GetPriv(Type type, object inst, string name)
        {
            FieldInfo fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) throw new InvalidOperationException("Поле не найдено: " + name);
            return fi.GetValue(inst);
        }

        // Поиск индекса в ComboBox по началу текста
        private static int FindComboIndex(ComboBox cb, string prefix, int fallback)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                string s = cb.Items[i].ToString();
                if (s.StartsWith(prefix)) return i;
            }
            return fallback;
        }

        // Ожидание включения кнопки с пампом событий
        private static void WaitEnabled(Button btn, int sec)
        {
            for (int k = 0; k < 15; k++) { Application.DoEvents(); Thread.Sleep(20); }
            int waited = 0;
            while (!btn.Enabled)
            {
                Application.DoEvents();
                Thread.Sleep(20);
                waited += 20;
                if (waited >= sec * 1000)
                    throw new InvalidOperationException("gui_replace_button: тайм-аут " + sec + " с");
            }
            for (int k = 0; k < 250; k++) { Application.DoEvents(); Thread.Sleep(20); }
        }

        // Первый GDML в каталоге
        private static string FindGdml(string dir)
        {
            string preferred = Path.Combine(dir, "Crystal_NaI.gdml");
            if (File.Exists(preferred)) return preferred;
            string[] files = Directory.GetFiles(dir, "*.gdml");
            return files.Length > 0 ? files[0] : null;
        }
    }
}
