using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Cad2Gdml.Tests
{
    // GUI-проверка: конвертация дважды в одну папку (регрессия заблокированного PNG)
    public static class ChecksGui
    {
        private static string _lastUi = "";
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("gui_convert_twice", () => Run(tempDir)));
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
            if (!t.Join(TimeSpan.FromSeconds(120)))
                throw new InvalidOperationException("gui_convert_twice: тайм-аут 120 с");
            if (captured != null) throw captured;
        }

        private static void DoCheck(string tempDir)
        {
            string inDir = Path.Combine(tempDir, "gui", "in");
            string outDir = Path.Combine(tempDir, "gui", "out");
            Directory.CreateDirectory(inDir);
            Directory.CreateDirectory(outDir);

            // Запись входных STL
            string crystalPath = Path.Combine(inDir, "crystal.stl");
            string waterPath = Path.Combine(inDir, "water_sample.stl");
            Fixtures.WriteBinaryStl(crystalPath, Fixtures.Cylinder(25.4, 50.8, 64, 10.0));
            Fixtures.WriteBinaryStl(waterPath, Fixtures.Cylinder(30.0, 40.0, 64, 70.0));

            // Создаём форму вне экрана
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
            lst.Items.Add(waterPath);

            // Папка вывода
            ((TextBox)GetPriv(ft, form, "_txtOutDir")).Text = outDir;

            // Единицы: "мм"
            var cmbUnit = (ComboBox)GetPriv(ft, form, "_cmbUnit");
            cmbUnit.SelectedIndex = FindComboIndex(cmbUnit, "мм", 0);

            // Фронт: "+z"
            var cmbFront = (ComboBox)GetPriv(ft, form, "_cmbFront");
            cmbFront.SelectedIndex = FindComboIndex(cmbFront, "+z", 0);

            // Разрешить вещество по умолчанию
            ((CheckBox)GetPriv(ft, form, "_chkAllowDefault")).Checked = true;

            // Ссылки для ожидания и ассертов
            var btn = (Button)GetPriv(ft, form, "_btnConvert");
            var lbl = (Label)GetPriv(ft, form, "_lblStatus");
            var pic = (PictureBox)GetPriv(ft, form, "_picSketch");
            var rep = (TextBox)GetPriv(ft, form, "_txtReport");

            // Метод OnConvert
            Application.ThreadException += (s, ex) => { _lastUi = ex.Exception.ToString(); };
            MethodInfo mi = ft.GetMethod("OnConvert", BindingFlags.NonPublic | BindingFlags.Instance);

            // ── Первый прогон ──
            mi.Invoke(form, new object[] { null, EventArgs.Empty });
            WaitEnabled(btn, 60);

            AssertNotFail(lbl.Text, "1");
            string png1 = FindPng(outDir);
            if (png1 == null) throw new InvalidOperationException("gui_convert_twice: PNG отсутствует (1)");
            DateTime t1 = File.GetLastWriteTime(png1);
            if (pic.Image == null) throw new InvalidOperationException("gui_convert_twice: Image == null (1); status=[" + lbl.Text + "] report_len=" + rep.Text.Length + " btn=" + btn.Enabled + " ui_exception=" + _lastUi);
            if (rep.Text.Length <= 0) throw new InvalidOperationException("gui_convert_twice: отчёт пуст (1)");

            // ── Второй прогон в ту же папку ──
            mi.Invoke(form, new object[] { null, EventArgs.Empty });
            WaitEnabled(btn, 60);

            AssertNotFail(lbl.Text, "2");
            string png2 = FindPng(outDir);
            if (png2 == null) throw new InvalidOperationException("gui_convert_twice: PNG отсутствует (2)");
            DateTime t2 = File.GetLastWriteTime(png2);
            if (t2 < t1) throw new InvalidOperationException("gui_convert_twice: PNG не обновлён (2)");
            if (pic.Image == null) throw new InvalidOperationException("gui_convert_twice: Image == null (2)");
            if (rep.Text.Length <= 0) throw new InvalidOperationException("gui_convert_twice: отчёт пуст (2)");

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
            // даём фоновому обработчику стартовать и после завершения — применить результат к экрану
            for (int k = 0; k < 15; k++) { Application.DoEvents(); Thread.Sleep(20); }
            int waited = 0;
            while (!btn.Enabled)
            {
                Application.DoEvents();
                Thread.Sleep(20);
                waited += 20;
                if (waited >= sec * 1000)
                    throw new InvalidOperationException("gui_convert_twice: тайм-аут " + sec + " с");
            }
            for (int k = 0; k < 25; k++) { Application.DoEvents(); Thread.Sleep(20); }
        }

        // Первый PNG в каталоге
        private static string FindPng(string dir)
        {
            string preferred = Path.Combine(dir, "crystal.png");
            if (File.Exists(preferred)) return preferred;
            string[] files = Directory.GetFiles(dir, "*.png");
            return files.Length > 0 ? files[0] : null;
        }

        // Статус не должен начинаться с "Сбой"
        private static void AssertNotFail(string text, string run)
        {
            if (text.StartsWith("Сбой"))
                throw new InvalidOperationException("gui_convert_twice: сбой в прогоне " + run + ": " + text);
        }
    }
}
