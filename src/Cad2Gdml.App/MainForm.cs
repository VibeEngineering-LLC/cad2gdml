using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Cad2Gdml.Pipeline;
using Cad2Gdml.Scene;

namespace Cad2Gdml.App
{
    // Точка входа
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // Главное окно (без дизайнера)
    public sealed class MainForm : Form
    {
        // Контролы верхней панели
        private Button _btnAddFiles;
        private Button _btnAddFolder;
        private ComboBox _cmbUnit;
        private TextBox _txtCrystal;
        private ComboBox _cmbFront;
        private TextBox _txtOutDir;
        private Button _btnBrowseOut;
        private Button _btnConvert;
        private CheckBox _chkAllowDefault;
        private CheckBox _chkDropOpen;

        // Левый список, картинка, отчёт, статус
        private ListBox _lstInputs;
        private PictureBox _picSketch;
        private TextBox _txtReport;
        private Label _lblStatus;

        // Фоновая работа
        private BackgroundWorker _worker;

        // Последняя папка (запоминаем в форме)
        private string _lastFolder;

        public MainForm()
        {
            BuildUi();
            SetupWorker();
        }

        // Построение всех контролов кодом
        private void BuildUi()
        {
            this.Text = "CAD → GDML";
            this.ClientSize = new Size(1060, 740);
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- Верхняя панель (2 ряда) ---
            var topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 74;
            topPanel.Padding = new Padding(6, 6, 6, 4);

            _btnAddFiles = MakeButton("Добавить файлы…", 8, 6, 124, 26, OnAddFiles);
            _btnAddFolder = MakeButton("Добавить папку…", 8, 38, 124, 26, OnAddFolder);

            _cmbUnit = new ComboBox();
            _cmbUnit.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbUnit.Items.AddRange(new object[] { "мм", "см", "м", "дюйм" });
            _cmbUnit.SelectedIndex = 0;
            _cmbUnit.Location = new Point(146, 6);
            _cmbUnit.Size = new Size(56, 24);
            AddLabel(topPanel, "Ед.", 146, 34);

            // Замечание про 3MF
            var lblUnitNote = new Label();
            lblUnitNote.Text = "для 3MF единицы берутся из файла (только если все входы 3MF)";
            lblUnitNote.Font = new Font(this.Font, FontStyle.Italic);
            lblUnitNote.AutoSize = true;
            lblUnitNote.Location = new Point(204, 38);
            topPanel.Controls.Add(lblUnitNote);

            _txtCrystal = new TextBox();
            _txtCrystal.Location = new Point(330, 6);
            _txtCrystal.Size = new Size(110, 24);
            AddLabel(topPanel, "Кристалл:", 330, 34);

            _cmbFront = new ComboBox();
            _cmbFront.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFront.Items.AddRange(new object[] { "auto", "+z", "-z" });
            _cmbFront.SelectedIndex = 0;
            _cmbFront.Location = new Point(452, 6);
            _cmbFront.Size = new Size(56, 24);
            AddLabel(topPanel, "Фронт:", 452, 34);

            _txtOutDir = new TextBox();
            _txtOutDir.Location = new Point(522, 6);
            _txtOutDir.Size = new Size(190, 24);
            AddLabel(topPanel, "Папка вывода:", 522, 34);

            _btnBrowseOut = MakeButton("Обзор…", 716, 6, 56, 26, OnBrowseOut);

            _btnConvert = new Button();
            _btnConvert.Text = "Конвертировать";
            _btnConvert.Location = new Point(786, 6);
            _btnConvert.Size = new Size(130, 56);
            _btnConvert.Font = new Font(this.Font, FontStyle.Bold);
            _btnConvert.Click += OnConvert;

            // Чекбоксы второго ряда
            _chkAllowDefault = new CheckBox();
            _chkAllowDefault.Text = "Разрешить вещество по умолчанию";
            _chkAllowDefault.Checked = false;
            _chkAllowDefault.Location = new Point(330, 38);
            _chkAllowDefault.AutoSize = true;

            _chkDropOpen = new CheckBox();
            _chkDropOpen.Text = "Пропускать открытые детали";
            _chkDropOpen.Checked = false;
            _chkDropOpen.Location = new Point(560, 38);
            _chkDropOpen.AutoSize = true;

            topPanel.Controls.Add(_btnAddFiles);
            topPanel.Controls.Add(_btnAddFolder);
            topPanel.Controls.Add(_cmbUnit);
            topPanel.Controls.Add(_txtCrystal);
            topPanel.Controls.Add(_cmbFront);
            topPanel.Controls.Add(_txtOutDir);
            topPanel.Controls.Add(_btnBrowseOut);
            topPanel.Controls.Add(_btnConvert);
            topPanel.Controls.Add(_chkAllowDefault);
            topPanel.Controls.Add(_chkDropOpen);

            // --- Левая панель: список входов ---
            var leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Left;
            leftPanel.Width = 220;
            _lstInputs = new ListBox();
            _lstInputs.Dock = DockStyle.Fill;
            _lstInputs.DrawMode = DrawMode.OwnerDrawFixed;
            _lstInputs.DrawItem += OnDrawItem;
            leftPanel.Controls.Add(_lstInputs);

            // --- Центр: картинка ---
            _picSketch = new PictureBox();
            _picSketch.Dock = DockStyle.Fill;
            _picSketch.SizeMode = PictureBoxSizeMode.Zoom;
            _picSketch.BackColor = Color.White;

            // --- Низ: отчёт + статус ---
            var bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 170;
            _txtReport = new TextBox();
            _txtReport.Multiline = true;
            _txtReport.ReadOnly = true;
            _txtReport.ScrollBars = ScrollBars.Both;
            _txtReport.Dock = DockStyle.Fill;
            _txtReport.BackColor = Color.White;
            _txtReport.Font = new Font("Consolas", 9f);
            bottomPanel.Controls.Add(_txtReport);

            _lblStatus = new Label();
            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 22;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.Padding = new Padding(6, 2, 0, 0);
            _lblStatus.Text = "Готово.";
            _lblStatus.BorderStyle = BorderStyle.FixedSingle;

            // Порядок добавления: Fill → Bottom → Left → Top
            this.Controls.Add(_picSketch);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(leftPanel);
            this.Controls.Add(topPanel);
        }

        // Фабрика кнопки
        private Button MakeButton(string text, int x, int y, int w, int h, EventHandler handler)
        {
            var b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.Click += handler;
            return b;
        }

        // Короткая подпись
        private void AddLabel(Panel parent, string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.AutoSize = true;
            parent.Controls.Add(l);
        }

        // Отрисовка строки списка
        private void OnDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            string s = (string)_lstInputs.Items[e.Index];
            e.DrawBackground();
            e.Graphics.DrawString(s, e.Font, Brushes.Black, e.Bounds);
        }

        // Фоновый воркер
        private void SetupWorker()
        {
            _worker = new BackgroundWorker();
            _worker.DoWork += OnDoWork;
            _worker.RunWorkerCompleted += OnWorkerDone;
        }

        // --- Обработчики кнопок ---

        private void OnAddFiles(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "3D-файлы|*.stl;*.obj;*.3mf|STL|*.stl|OBJ|*.obj|3MF|*.3mf|Все|*.*";
                dlg.Multiselect = true;
                if (!string.IsNullOrEmpty(_lastFolder) && Directory.Exists(_lastFolder))
                    dlg.InitialDirectory = _lastFolder;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (string f in dlg.FileNames)
                    {
                        if (!_lstInputs.Items.Contains(f))
                            _lstInputs.Items.Add(f);
                    }
                    _lastFolder = Path.GetDirectoryName(dlg.FileName);
                }
            }
        }

        private void OnAddFolder(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (!string.IsNullOrEmpty(_lastFolder) && Directory.Exists(_lastFolder))
                    dlg.SelectedPath = _lastFolder;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _lastFolder = dlg.SelectedPath;
                    string dir = dlg.SelectedPath;
                    foreach (string file in SafeGetFiles(dir))
                    {
                        if (!_lstInputs.Items.Contains(file))
                            _lstInputs.Items.Add(file);
                    }
                }
            }
        }

        private void OnBrowseOut(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (!string.IsNullOrEmpty(_lastFolder) && Directory.Exists(_lastFolder))
                    dlg.SelectedPath = _lastFolder;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _txtOutDir.Text = dlg.SelectedPath;
                    _lastFolder = dlg.SelectedPath;
                }
            }
        }

        private void OnConvert(object sender, EventArgs e)
        {
            if (_lstInputs.Items.Count == 0)
            {
                SetStatus("Добавьте хотя бы один входной файл.", false);
                return;
            }

            string outDir = _txtOutDir.Text.Trim();
            if (string.IsNullOrEmpty(outDir))
            {
                SetStatus("Укажите папку вывода.", false);
                return;
            }

            // Собираем список входов
            var inputs = new List<string>();
            foreach (string item in _lstInputs.Items)
            {
                if (Directory.Exists(item))
                {
                    foreach (string f in SafeGetFiles(item))
                        inputs.Add(f);
                }
                else
                {
                    inputs.Add(item);
                }
            }

            if (inputs.Count == 0)
            {
                SetStatus("Нет поддерживаемых файлов (.stl/.obj/.3mf).", false);
                return;
            }

            // Проверка на смешанные входы 3MF + STL/OBJ
            if (HasMixedInputs(inputs))
            {
                MessageBox.Show(this,
                    "Смешанные входы 3MF и STL/OBJ: конвертируйте раздельно",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Если все входы — 3MF, передаём 0 (единицы из файла); иначе — значение из ComboBox
            double unitToMm;
            if (AllAre3Mf(inputs))
                unitToMm = 0.0;
            else
                unitToMm = GetUnitToMm();

            var opts = new PipelineOptions();
            opts.Inputs = inputs;
            opts.OutDir = outDir;
            opts.UnitToMm = unitToMm;
            opts.UnitName = _cmbUnit.SelectedItem.ToString();
            opts.CrystalName = _txtCrystal.Text.Trim();
            opts.FrontSign = _cmbFront.SelectedItem.ToString();
            opts.AllowDefaultMaterial = _chkAllowDefault.Checked;
            opts.DropOpen = _chkDropOpen.Checked;

            SetControlsEnabled(false);
            SetStatus("Конвертация…", true);
            _worker.RunWorkerAsync(opts);
        }

        // --- Фоновая логика ---

        private void OnDoWork(object sender, DoWorkEventArgs e)
        {
            var opts = (PipelineOptions)e.Argument;
            e.Result = Cad2GdmlPipeline.Run(opts);
        }

        private void OnWorkerDone(object sender, RunWorkerCompletedEventArgs e)
        {
            SetControlsEnabled(true);

            if (e.Error != null)
            {
                SetStatus("Ошибка: " + e.Error.Message, false);
                return;
            }

            var result = (PipelineResult)e.Result;

            if (result.ExitCode != 0)
            {
                // Очищаем картинку от предыдущего успешного прогона
                if (_picSketch.Image != null)
                {
                    _picSketch.Image.Dispose();
                    _picSketch.Image = null;
                }

                // Формируем статус: Сбой (код N): ID — сообщение
                int exitCode = result.ExitCode;
                string statusLine;
                if (!string.IsNullOrEmpty(result.MainErrorId))
                {
                    statusLine = "Сбой (код " + exitCode.ToString(CultureInfo.InvariantCulture) + "): "
                        + result.MainErrorId + " — " + result.MainErrorMessage;
                }
                else
                {
                    // MainErrorId пуст — берём сообщение последней диагностики
                    string lastMsg = "";
                    if (result.Diagnostics != null && result.Diagnostics.Count > 0)
                    {
                        Diagnostic last = result.Diagnostics[result.Diagnostics.Count - 1];
                        lastMsg = last.Message;
                    }
                    statusLine = "Сбой (код " + exitCode.ToString(CultureInfo.InvariantCulture) + "): " + lastMsg;
                }
                SetStatus(statusLine, false);

                // Показываем диагностику сбойного прогона в отчёте
                RenderFailureReport(result);
                return;
            }

            SetStatus("Готово → " + result.GdmlPath, true);

            // Показ картинки через MemoryStream
            if (!string.IsNullOrEmpty(result.PngPath) && File.Exists(result.PngPath))
            {
                try
                {
                    if (_picSketch.Image != null)
                        _picSketch.Image.Dispose();
                    var ms = new MemoryStream(File.ReadAllBytes(result.PngPath));
                    _picSketch.Image = new Bitmap(ms);
                }
                catch
                {
                    // Не критично
                }
            }

            RenderReport(result);
        }

        // Отчёт для сбойного прогона
        private void RenderFailureReport(PipelineResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== СБОЙ КОНВЕРТАЦИИ ===");

            if (result.Diagnostics != null && result.Diagnostics.Count > 0)
            {
                sb.AppendLine("Диагностика:");
                for (int i = 0; i < result.Diagnostics.Count; i++)
                {
                    Diagnostic d = result.Diagnostics[i];
                    sb.AppendLine("  [" + d.Code + "] " + d.PartName + ": " + d.Message);
                }
            }
            else
            {
                sb.AppendLine("Диагностика отсутствует.");
            }

            _txtReport.Text = sb.ToString();
        }

        // Формирование текста отчёта (успешный прогон)
        private void RenderReport(PipelineResult result)
        {
            var sb = new StringBuilder();

            if (result.Gdml != null)
            {
                sb.AppendLine("=== GDML ===");
                sb.AppendLine("Файл: " + result.GdmlPath);
                sb.AppendLine("Объёмов: " + result.Gdml.Volumes.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("Тесселлировано: " + result.Gdml.Tessellated.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("Треугольников: " + result.Gdml.Triangles.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("SHA-256: " + result.Gdml.Sha256);
                sb.AppendLine();
            }

            if (result.Diagnostics != null && result.Diagnostics.Count > 0)
            {
                sb.AppendLine("=== Диагностика ===");
                foreach (Diagnostic d in result.Diagnostics)
                {
                    sb.AppendLine("  [" + d.Code + "] " + d.Message);
                }
                sb.AppendLine();
            }

            if (result.Scene != null && result.Scene.Parts != null)
            {
                sb.AppendLine("=== Детали (" + result.Scene.Parts.Count.ToString(CultureInfo.InvariantCulture) + ") ===");
                foreach (Part p in result.Scene.Parts)
                {
                    sb.Append("  ");
                    sb.Append(p.Name);
                    sb.Append(" | ");
                    sb.Append(p.Role.ToString());
                    sb.Append(" | ");
                    sb.Append(p.Material ?? "");
                    sb.Append(" | ");
                    sb.Append(p.VolumeMm3.ToString("0.##", CultureInfo.InvariantCulture));
                    sb.Append(" мм³");
                    sb.AppendLine();
                }
            }

            _txtReport.Text = sb.ToString();
        }

        // --- Вспомогательные ---

        // Единицы → множитель в мм
        private double GetUnitToMm()
        {
            int idx = _cmbUnit.SelectedIndex;
            if (idx == 1) return 10.0;
            if (idx == 2) return 1000.0;
            if (idx == 3) return 25.4;
            return 1.0;
        }

        // Все ли файлы в списке — .3mf
        private static bool AllAre3Mf(List<string> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                string ext = Path.GetExtension(files[i]);
                if (ext == null || ext.ToLowerInvariant() != ".3mf")
                    return false;
            }
            return true;
        }

        // Смешаны ли 3MF и не-3MF в одном списке
        private static bool HasMixedInputs(List<string> files)
        {
            bool has3mf = false;
            bool hasOther = false;
            for (int i = 0; i < files.Count; i++)
            {
                string ext = Path.GetExtension(files[i]);
                if (ext != null && ext.ToLowerInvariant() == ".3mf")
                    has3mf = true;
                else
                    hasOther = true;
            }
            return has3mf && hasOther;
        }

        // Вкл/выкл во время конвертации
        private void SetControlsEnabled(bool en)
        {
            _btnAddFiles.Enabled = en;
            _btnAddFolder.Enabled = en;
            _cmbUnit.Enabled = en;
            _txtCrystal.Enabled = en;
            _cmbFront.Enabled = en;
            _txtOutDir.Enabled = en;
            _btnBrowseOut.Enabled = en;
            _btnConvert.Enabled = en;
            _chkAllowDefault.Enabled = en;
            _chkDropOpen.Enabled = en;
            _lstInputs.Enabled = en;
        }

        // Статус (красный при ошибке)
        private void SetStatus(string text, bool ok)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = ok ? Color.Black : Color.Red;
        }

        // Безопасное перечисление только *.stl, *.obj, *.3mf
        private static string[] SafeGetFiles(string dir)
        {
            try
            {
                var all = Directory.GetFiles(dir);
                var result = new List<string>();
                foreach (string f in all)
                {
                    string ext = Path.GetExtension(f);
                    if (ext == null) continue;
                    ext = ext.ToLowerInvariant();
                    if (ext == ".stl" || ext == ".obj" || ext == ".3mf")
                        result.Add(f);
                }
                return result.ToArray();
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
