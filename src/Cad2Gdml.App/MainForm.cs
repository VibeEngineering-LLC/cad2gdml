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

            _txtCrystal = new TextBox();
            _txtCrystal.Location = new Point(214, 6);
            _txtCrystal.Size = new Size(110, 24);
            AddLabel(topPanel, "Кристалл:", 214, 34);

            _cmbFront = new ComboBox();
            _cmbFront.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFront.Items.AddRange(new object[] { "auto", "+z", "-z" });
            _cmbFront.SelectedIndex = 0;
            _cmbFront.Location = new Point(336, 6);
            _cmbFront.Size = new Size(56, 24);
            AddLabel(topPanel, "Фронт:", 336, 34);

            _txtOutDir = new TextBox();
            _txtOutDir.Location = new Point(406, 6);
            _txtOutDir.Size = new Size(210, 24);
            AddLabel(topPanel, "Папка вывода:", 406, 34);

            _btnBrowseOut = MakeButton("Обзор…", 628, 6, 56, 26, OnBrowseOut);

            _btnConvert = new Button();
            _btnConvert.Text = "Конвертировать";
            _btnConvert.Location = new Point(700, 6);
            _btnConvert.Size = new Size(130, 56);
            _btnConvert.Font = new Font(this.Font, FontStyle.Bold);
            _btnConvert.Click += OnConvert;

            topPanel.Controls.Add(_btnAddFiles);
            topPanel.Controls.Add(_btnAddFolder);
            topPanel.Controls.Add(_cmbUnit);
            topPanel.Controls.Add(_txtCrystal);
            topPanel.Controls.Add(_cmbFront);
            topPanel.Controls.Add(_txtOutDir);
            topPanel.Controls.Add(_btnBrowseOut);
            topPanel.Controls.Add(_btnConvert);

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
                    // Перечисляем поддерживаемые файлы
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

            // Собираем список входов (если папка — расширяем)
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

            var opts = new PipelineOptions();
            opts.Inputs = inputs;
            opts.OutDir = _txtOutDir.Text.Trim();
            opts.UnitToMm = GetUnitToMm();
            opts.UnitName = _cmbUnit.SelectedItem.ToString();
            opts.CrystalName = _txtCrystal.Text.Trim();
            opts.FrontSign = _cmbFront.SelectedItem.ToString();
            opts.AllowDefaultMaterial = true;

            SetControlsEnabled(false);
            SetStatus("Конвертация…", true);
            _picSketch.Image = null;
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
                string msg = "Сбой (код " + result.ExitCode.ToString(CultureInfo.InvariantCulture) + ")";
                if (result.Diagnostics != null && result.Diagnostics.Count > 0)
                {
                    Diagnostic last = result.Diagnostics[result.Diagnostics.Count - 1];
                    msg += ": " + last.Message;
                }
                SetStatus(msg, false);
                RenderReport(result);
                return;
            }

            SetStatus("Готово → " + result.GdmlPath, true);

            // Показ картинки
            if (!string.IsNullOrEmpty(result.PngPath) && File.Exists(result.PngPath))
            {
                try
                {
                    if (_picSketch.Image != null)
                        _picSketch.Image.Dispose();
                    _picSketch.Image = Image.FromFile(result.PngPath);
                }
                catch
                {
                    // Не критично — картинка не обязательна
                }
            }

            RenderReport(result);
        }

        // Формирование текста отчёта
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
            _lstInputs.Enabled = en;
        }

        // Статус (красный при ошибке)
        private void SetStatus(string text, bool ok)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = ok ? Color.Black : Color.Red;
        }

        // Безопасное перечисление файлов
        private static string[] SafeGetFiles(string dir)
        {
            try
            {
                return Directory.GetFiles(dir);
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
