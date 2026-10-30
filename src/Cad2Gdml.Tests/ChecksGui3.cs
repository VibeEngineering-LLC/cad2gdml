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
    // GUI-проверка кастомного материала (#CG-32)
    public static class ChecksGui3
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("gui_custom_material", () => Run(tempDir)));
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
            if (!t.Join(TimeSpan.FromSeconds(200)))
                throw new InvalidOperationException("gui_custom_material: тайм-аут 200 с");
            if (captured != null) throw captured;
        }

        private static void DoCheck(string tempDir)
        {
            // Очистка сессии
            Cad2Gdml.App.SceneViewerForm.SessionCustom.Clear();

            string inDir = Path.Combine(tempDir, "gui3", "in");
            string outDir = Path.Combine(tempDir, "gui3", "out");
            Directory.CreateDirectory(inDir);
            Directory.CreateDirectory(outDir);

            // Входные STL
            string crystalPath = Path.Combine(inDir, "Crystal_NaI.stl");
            string housingPath = Path.Combine(inDir, "Housing.stl");
            Fixtures.WriteBinaryStl(crystalPath, Fixtures.Box(10, 10, 30, 0, 0, 0));
            Fixtures.WriteBinaryStl(housingPath, Fixtures.Box(20, 20, 50, 0, 0, 0));

            // Форма вне экрана
            var form = new Cad2Gdml.App.MainForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();

            Type ft = typeof(Cad2Gdml.App.MainForm);

            var lst = (ListBox)GetPriv(ft, form, "_lstInputs");
            lst.Items.Add(crystalPath);
            lst.Items.Add(housingPath);
            ((TextBox)GetPriv(ft, form, "_txtOutDir")).Text = outDir;

            var cmbFront = (ComboBox)GetPriv(ft, form, "_cmbFront");
            cmbFront.SelectedIndex = FindComboIndex(cmbFront, "+z", 0);
            ((CheckBox)GetPriv(ft, form, "_chkAllowDefault")).Checked = true;

            var btn = (Button)GetPriv(ft, form, "_btnConvert");
            var lbl = (Label)GetPriv(ft, form, "_lblStatus");

            MethodInfo miConvert = ft.GetMethod("OnConvert", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo miMaterials = ft.GetMethod("OnMaterials", BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                // ── Шаг 1: первый прогон ──
                miConvert.Invoke(form, new object[] { null, EventArgs.Empty });
                WaitEnabled(btn, 90);

                // ── Шаг 2: OnMaterials + помощник ──
                string[] helperErr = new string[1];
                var helperDone = new ManualResetEventSlim(false);

                var helper = new Thread(delegate ()
                {
                    try
                    {
                        // (a) Ждём SceneViewerForm
                        Form svForm = null;
                        int waited = 0;
                        while (waited < 30000 && svForm == null)
                        {
                            try
                            {
                                svForm = Application.OpenForms.Cast<Form>().OfType<Cad2Gdml.App.SceneViewerForm>().FirstOrDefault();
                            }
                            catch (InvalidOperationException) { }
                            if (svForm == null) { Thread.Sleep(100); waited += 100; }
                        }
                        if (svForm == null) throw new InvalidOperationException("SceneViewerForm не найдена за 30 с");

                        Type svType = typeof(Cad2Gdml.App.SceneViewerForm);
                        var grid = (DataGridView)GetPriv(svType, svForm, "_grid");
                        MethodInfo miCustomMat = svType.GetMethod("OnCustomMaterial", BindingFlags.NonPublic | BindingFlags.Instance);

                        // (b) Вызываем OnCustomMaterial (откроет модалку)
                        svForm.BeginInvoke(new Action(delegate ()
                        {
                            int rowIdx = -1;
                            for (int r = 0; r < grid.Rows.Count; r++)
                            {
                                var val = grid.Rows[r].Cells[1].Value;
                                if (val != null && val.ToString().StartsWith("Crystal_NaI"))
                                { rowIdx = r; break; }
                            }
                            if (rowIdx < 0) throw new InvalidOperationException("Строка Crystal_NaI не найдена в grid");
                            grid.CurrentCell = grid.Rows[rowIdx].Cells[1];
                            miCustomMat.Invoke(svForm, new object[] { null, EventArgs.Empty });
                        }));

                        // (c) Ждём CustomMaterialDialog
                        Form dlg = null;
                        waited = 0;
                        while (waited < 30000 && dlg == null)
                        {
                            try
                            {
                                dlg = Application.OpenForms.Cast<Form>().OfType<Cad2Gdml.App.CustomMaterialDialog>().FirstOrDefault();
                            }
                            catch (InvalidOperationException) { }
                            if (dlg == null) { Thread.Sleep(100); waited += 100; }
                        }
                        if (dlg == null) throw new InvalidOperationException("CustomMaterialDialog не найден за 30 с");

                        // Заполняем поля, кликаем ОК
                        dlg.BeginInvoke(new Action(delegate ()
                        {
                            Type dlgType = typeof(Cad2Gdml.App.CustomMaterialDialog);
                            ((TextBox)GetPriv(dlgType, dlg, "_txtName")).Text = "MyScint";
                            ((TextBox)GetPriv(dlgType, dlg, "_txtDensity")).Text = "4,2";
                            ((TextBox)GetPriv(dlgType, dlg, "_txtComp")).Text = "La=0.36688; Br=0.63312";
                            var okBtn = dlg.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "ОК");
                            if (okBtn == null) throw new InvalidOperationException("Кнопка ОК не найдена");
                            okBtn.PerformClick();
                        }));

                        // (d) Ждём закрытия диалога
                        waited = 0;
                        while (waited < 30000)
                        {
                            bool open = false;
                            try { open = Application.OpenForms.Cast<Form>().Any(f => f is Cad2Gdml.App.CustomMaterialDialog); }
                            catch (InvalidOperationException) { }
                            if (!open) break;
                            Thread.Sleep(100); waited += 100;
                        }

                        // Применяем (OnApply закрывает SceneViewerForm с OK)
                        MethodInfo miApply = svType.GetMethod("OnApply", BindingFlags.NonPublic | BindingFlags.Instance);
                        svForm.BeginInvoke(new Action(delegate ()
                        {
                            miApply.Invoke(svForm, new object[] { null, EventArgs.Empty });
                        }));
                    }
                    catch (Exception ex)
                    {
                        lock (helperErr) { helperErr[0] = ex.Message; }
                    }
                    finally { helperDone.Set(); }
                });
                helper.SetApartmentState(ApartmentState.MTA);
                helper.Start();

                // OnMaterials блокируется в ShowDialog до закрытия SceneViewerForm
                miMaterials.Invoke(form, new object[] { null, EventArgs.Empty });

                helperDone.Wait(TimeSpan.FromSeconds(40));
                helper.Join();

                lock (helperErr)
                {
                    if (helperErr[0] != null)
                        throw new InvalidOperationException("gui_custom_material: помощник: " + helperErr[0]);
                }

                // ── Шаг 3: ждём завершения повторной конвертации ──
                for (int k = 0; k < 50; k++) { Application.DoEvents(); Thread.Sleep(20); }
                WaitEnabled(btn, 90);

                string status = lbl.Text;
                if (status.StartsWith("Сбой"))
                    throw new InvalidOperationException("gui_custom_material: статус: " + status);

                // ── Шаг 4: проверка GDML ──
                string gdmlPath = FindGdml(outDir);
                if (gdmlPath == null) throw new InvalidOperationException("gui_custom_material: GDML не найден");

                XDocument doc = XDocument.Load(gdmlPath);

                // Материал MyScint
                var matEl = doc.Descendants().FirstOrDefault(e =>
                    e.Name.LocalName == "material" && (string)e.Attribute("name") == "MyScint");
                if (matEl == null) throw new InvalidOperationException("gui_custom_material: материал MyScint не найден");

                var dEl = matEl.Elements().FirstOrDefault(e => e.Name.LocalName == "D");
                if (dEl == null) throw new InvalidOperationException("gui_custom_material: элемент D не найден");

                double density = double.Parse((string)dEl.Attribute("value"), System.Globalization.CultureInfo.InvariantCulture);
                if (Math.Abs(density - 4.2) > 1e-9)
                    throw new InvalidOperationException("gui_custom_material: плотность " + density + " != 4.2");

                string dUnit = (string)dEl.Attribute("unit");
                if (dUnit != "g/cm3")
                    throw new InvalidOperationException("gui_custom_material: unit=" + dUnit);

                // Volume с Crystal и materialref
                var volEl = doc.Descendants().FirstOrDefault(e =>
                    e.Name.LocalName == "volume" &&
                    (string)e.Attribute("name") != null &&
                    ((string)e.Attribute("name")).Contains("Crystal"));
                if (volEl == null) throw new InvalidOperationException("gui_custom_material: volume Crystal не найден");

                var matRef = volEl.Elements().FirstOrDefault(e => e.Name.LocalName == "materialref");
                if (matRef == null) throw new InvalidOperationException("gui_custom_material: materialref не найден");

                string refName = (string)matRef.Attribute("ref");
                if (refName != "MyScint")
                    throw new InvalidOperationException("gui_custom_material: materialref ref=" + refName + " != MyScint");
            }
            finally
            {
                Cad2Gdml.App.SceneViewerForm.SessionCustom.Clear();
                form.Close();
                form.Dispose();
            }
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
                    throw new InvalidOperationException("gui_custom_material: тайм-аут " + sec + " с");
            }
            for (int k = 0; k < 250; k++) { Application.DoEvents(); Thread.Sleep(20); }
        }

        // Самый новый GDML в каталоге
        private static string FindGdml(string dir)
        {
            string preferred = Path.Combine(dir, "Crystal_NaI.gdml");
            if (File.Exists(preferred)) return preferred;
            string[] files = Directory.GetFiles(dir, "*.gdml");
            if (files.Length == 0) return null;
            Array.Sort(files, (a, b) => File.GetLastWriteTime(a).CompareTo(File.GetLastWriteTime(b)));
            return files[files.Length - 1];
        }
    }
}
