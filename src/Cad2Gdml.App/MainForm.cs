using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        private Button _btnRemove;
        private ComboBox _cmbUnit;
        private TextBox _txtCrystal;
        private ComboBox _cmbFront;
        private TextBox _txtOutDir;
        private Button _btnBrowseOut;
        private Button _btnConvert;
        private Button _btnOpenFolder;
        private CheckBox _chkAllowDefault;
        private CheckBox _chkNoSplit;
        private CheckBox _chkDropOpen;

        // Левый список, картинка, отчёт, статус
        private ListBox _lstInputs;
        private PictureBox _picSketch;
        private TextBox _txtReport;
        private Label _lblStatus;

        // Фоновая работа
        private BackgroundWorker _worker;

        // Последняя папка
        private string _lastFolder;

        // Тултип для списка
        private ToolTip _tipList;

        public MainForm()
        {
            BuildUi();
            SetupWorker();
        }

        // Построение всех контролов кодом
        private void BuildUi()
        {
            this.Text = "CAD → GDML";
            this.ClientSize = new Size(1120, 740);
            this.MinimumSize = new Size(1100, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // --- Верхняя панель: TableLayoutPanel ---
            var topTable = new TableLayoutPanel();
            topTable.Dock = DockStyle.Top;
            topTable.AutoSize = true;
            topTable.ColumnCount = 9;
            topTable.RowCount = 3;
            topTable.Padding = new Padding(8);
            topTable.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;

            // Столбцы — все авто, последний процентный
            for (int i = 0; i < 9; i++)
                topTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topTable.ColumnStyles[7] = new ColumnStyle(SizeType.Percent, 100f);

            // Строки
            topTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // === Ряд 0: кнопки + единицы + кристалл + фронт ===
            _btnAddFiles = MakeButton("Добавить файлы…", OnAddFiles);
            _btnAddFiles.Size = new Size(130, 30);

            _btnAddFolder = MakeButton("Добавить папку…", OnAddFolder);
            _btnAddFolder.Size = new Size(130, 30);

            _btnRemove = MakeButton("Убрать", OnRemoveSelected);
            _btnRemove.Size = new Size(130, 30);

            var lblUnit = MakeLabel("Единицы:");
            _cmbUnit = new ComboBox();
            _cmbUnit.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbUnit.Items.AddRange(new object[] { "мм", "см", "м", "дюйм" });
            _cmbUnit.SelectedIndex = 0;
            _cmbUnit.Width = 90;
            _cmbUnit.Margin = new Padding(4);

            var lblCrystal = MakeLabel("Кристалл:");
            _txtCrystal = new TextBox();
            _txtCrystal.Width = 130;
            _txtCrystal.Margin = new Padding(4);

            var lblFront = MakeLabel("Фронт:");
            _cmbFront = new ComboBox();
            _cmbFront.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFront.Items.AddRange(new object[] { "auto", "+z", "-z" });
            _cmbFront.SelectedIndex = 0;
            _cmbFront.Width = 70;
            _cmbFront.Margin = new Padding(4);

            topTable.Controls.Add(_btnAddFiles, 0, 0);
            topTable.Controls.Add(_btnAddFolder, 1, 0);
            topTable.Controls.Add(_btnRemove, 2, 0);
            topTable.Controls.Add(lblUnit, 3, 0);
            topTable.Controls.Add(_cmbUnit, 4, 0);
            topTable.Controls.Add(lblCrystal, 5, 0);
            topTable.Controls.Add(_txtCrystal, 6, 0);
            topTable.Controls.Add(lblFront, 7, 0);
            topTable.Controls.Add(_cmbFront, 8, 0);

            // === Ряд 1: папка вывода + обзор + конвертировать (rowspan 2) ===
            var lblOutDir = MakeLabel("Папка вывода:");
            _txtOutDir = new TextBox();
            _txtOutDir.Margin = new Padding(4);
            _txtOutDir.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            _btnBrowseOut = MakeButton("Обзор…", OnBrowseOut);
            _btnBrowseOut.Size = new Size(60, 26);

            _btnConvert = new Button();
            _btnConvert.Text = "Конвертировать";
            _btnConvert.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnConvert.Size = new Size(150, 34);
            _btnConvert.Margin = new Padding(4);
            _btnConvert.Click += OnConvert;

            topTable.Controls.Add(lblOutDir, 0, 1);
            topTable.SetColumnSpan(_txtOutDir, 6);
            topTable.Controls.Add(_txtOutDir, 1, 1);
            topTable.Controls.Add(_btnBrowseOut, 7, 1);
            topTable.SetRowSpan(_btnConvert, 2);
            topTable.Controls.Add(_btnConvert, 8, 1);

            // === Ряд 2: чекбоксы + открыть папку ===
            _chkAllowDefault = new CheckBox();
            _chkAllowDefault.Text = "Разрешить вещество по умолчанию";
            _chkAllowDefault.AutoSize = true;
            _chkAllowDefault.Margin = new Padding(4);

            _chkDropOpen = new CheckBox();
            _chkDropOpen.Text = "Пропускать открытые детали";
            _chkDropOpen.AutoSize = true;
            _chkDropOpen.Margin = new Padding(4);

            _btnOpenFolder = new Button();
            _btnOpenFolder.Text = "Открыть папку";
            _btnOpenFolder.Size = new Size(110, 28);
            _btnOpenFolder.Enabled = false;
            _btnOpenFolder.Margin = new Padding(4);
            _btnOpenFolder.Click += OnOpenFolder;

            topTable.SetColumnSpan(_chkAllowDefault, 3);
            topTable.Controls.Add(_chkAllowDefault, 0, 2);
            _chkNoSplit = new CheckBox();
            _chkNoSplit.Text = "Не разбивать на тела";
            _chkNoSplit.AutoSize = true;
            _chkNoSplit.Margin = new Padding(4);

            var flowChecks = new FlowLayoutPanel();
            flowChecks.AutoSize = true;
            flowChecks.WrapContents = false;
            flowChecks.Margin = new Padding(0);
            flowChecks.Controls.Add(_chkDropOpen);
            flowChecks.Controls.Add(_chkNoSplit);
            topTable.SetColumnSpan(flowChecks, 3);
            topTable.Controls.Add(flowChecks, 3, 2);
            topTable.SetColumnSpan(_btnOpenFolder, 2);
            topTable.Controls.Add(_btnOpenFolder, 6, 2);

            // --- Подпись про 3MF ---
            var lblUnitNote = new Label();
            lblUnitNote.Text = "для 3MF единицы берутся из файла (только если все входы 3MF)";
            lblUnitNote.Font = new Font("Segoe UI", 9f, FontStyle.Italic);
            lblUnitNote.AutoSize = true;
            lblUnitNote.Dock = DockStyle.Top;
            lblUnitNote.Padding = new Padding(8, 0, 8, 4);
            lblUnitNote.ForeColor = Color.Gray;

            // --- Левая панель: список входов ---
            var leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Left;
            leftPanel.Width = 240;
            _lstInputs = new ListBox();
            _lstInputs.Dock = DockStyle.Fill;
            _lstInputs.DrawMode = DrawMode.OwnerDrawFixed;
            _lstInputs.DrawItem += OnDrawItem;
            _lstInputs.SelectionMode = SelectionMode.MultiExtended;
            _lstInputs.KeyDown += OnLstKeyDown;
            _lstInputs.SelectedIndexChanged += OnLstSelectionChanged;
            leftPanel.Controls.Add(_lstInputs);

            // Тултип для списка
            _tipList = new ToolTip();
            _tipList.InitialDelay = 400;

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

            // Порядок: Fill → Bottom → Left → Top (topTable последним)
            this.Controls.Add(_picSketch);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(leftPanel);
            this.Controls.Add(lblUnitNote);
            this.Controls.Add(topTable);
        }

        // Фабрика кнопки
        private Button MakeButton(string text, EventHandler handler)
        {
            var b = new Button();
            b.Text = text;
            b.Margin = new Padding(4);
            b.Click += handler;
            return b;
        }

        // Фабрика подписи
        private Label MakeLabel(string text)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(4);
            return l;
        }

        // Отрисовка строки списка (короткое имя)
        private void OnDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            string s = (string)_lstInputs.Items[e.Index];
            e.DrawBackground();
            string display = Path.GetFileName(s);
            e.Graphics.DrawString(display, e.Font, Brushes.Black, e.Bounds);
        }

        // Тултип: полный путь выбранного элемента
        private void OnLstSelectionChanged(object sender, EventArgs e)
        {
            if (_lstInputs.SelectedIndex >= 0)
            {
                string path = (string)_lstInputs.Items[_lstInputs.SelectedIndex];
                _tipList.Show(path, _lstInputs, _lstInputs.ClientSize.Width, 0, 3000);
            }
        }

        // Delete key — удалить выбранные
        private void OnLstKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                RemoveSelectedItems();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // Удаление выбранных элементов списка
        private void RemoveSelectedItems()
        {
            var toRemove = new List<int>();
            for (int i = 0; i < _lstInputs.Items.Count; i++)
            {
                if (_lstInputs.GetSelected(i))
                    toRemove.Add(i);
            }
            if (toRemove.Count == 0) return;

            for (int i = toRemove.Count - 1; i >= 0; i--)
                _lstInputs.Items.RemoveAt(toRemove[i]);

            _lstInputs.SelectedIndex = -1;
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
                dlg.Filter = "3D-файлы|*.stl;*.obj;*.3mf;*.step;*.stp|STL|*.stl|OBJ|*.obj|3MF|*.3mf|STEP|*.step;*.stp|Все|*.*";
                dlg.Multiselect = true;
                if (!string.IsNullOrEmpty(_lastFolder) && Directory.Exists(_lastFolder))
                    dlg.InitialDirectory = _lastFolder;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    bool firstAdded = _lstInputs.Items.Count == 0;
                    foreach (string f in dlg.FileNames)
                    {
                        if (!_lstInputs.Items.Contains(f))
                        {
                            _lstInputs.Items.Add(f);
                            CheckStepWarning(f);
                        }
                    }
                    if (firstAdded)
                        AutoFillOutDir();
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
                    bool firstAdded = _lstInputs.Items.Count == 0;
                    foreach (string file in SafeGetFiles(dir))
                    {
                        if (!_lstInputs.Items.Contains(file))
                        {
                            _lstInputs.Items.Add(file);
                            CheckStepWarning(file);
                        }
                    }
                    if (firstAdded)
                        AutoFillOutDir();
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

        // Кнопка «Убрать» — удалить выбранные
        private void OnRemoveSelected(object sender, EventArgs e)
        {
            RemoveSelectedItems();
        }

        // Кнопка «Открыть папку»
        private void OnOpenFolder(object sender, EventArgs e)
        {
            string outDir = _txtOutDir.Text.Trim();
            if (string.IsNullOrEmpty(outDir)) return;
            try
            {
                Process.Start("explorer.exe", "\"" + outDir + "\"");
            }
            catch
            {
                SetStatus("Не удалось открыть папку.", false);
            }
        }

        private void OnConvert(object sender, EventArgs e)
        {
            if (_lstInputs.Items.Count == 0)
            {
                SetStatus("Добавьте хотя бы один входной файл.", false);
                return;
            }

            // STEP — блокируем конвертацию
            if (HasStepInput())
            {
                SetStatus("STEP пока не поддерживается в этой версии", false);
                return;
            }

            // Папка вывода: автозаполнение если пуста
            string outDir = _txtOutDir.Text.Trim();
            if (string.IsNullOrEmpty(outDir))
            {
                outDir = ComputeDefaultOutDir();
                _txtOutDir.Text = outDir;
            }

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

            // Единицы
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
            opts.NoSplit = _chkNoSplit.Checked;

            _btnOpenFolder.Enabled = false;
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
                if (_picSketch.Image != null)
                {
                    _picSketch.Image.Dispose();
                    _picSketch.Image = null;
                }

                int exitCode = result.ExitCode;
                string statusLine;
                if (!string.IsNullOrEmpty(result.MainErrorId))
                {
                    statusLine = "Сбой (код " + exitCode.ToString(CultureInfo.InvariantCulture) + "): "
                        + result.MainErrorId + " — " + result.MainErrorMessage;
                }
                else
                {
                    string lastMsg = "";
                    if (result.Diagnostics != null && result.Diagnostics.Count > 0)
                    {
                        Diagnostic last = result.Diagnostics[result.Diagnostics.Count - 1];
                        lastMsg = last.Message;
                    }
                    statusLine = "Сбой (код " + exitCode.ToString(CultureInfo.InvariantCulture) + "): " + lastMsg;
                }
                SetStatus(statusLine, false);
                RenderFailureReport(result);
                return;
            }

            SetStatus("Готово → " + result.GdmlPath, true);
            _btnOpenFolder.Enabled = true;

            if (!string.IsNullOrEmpty(result.PngPath) && File.Exists(result.PngPath))
            {
                try
                {
                    if (_picSketch.Image != null)
                        _picSketch.Image.Dispose();
                    var ms = new MemoryStream(File.ReadAllBytes(result.PngPath));
                    _picSketch.Image = new Bitmap(ms);
                }
                catch (Exception pex)
                {
                    SetStatus("Не удалось показать эскиз: " + pex.Message, false);
                }
            }
            else
            {
                SetStatus("Эскиз не найден: [" + result.PngPath + "]", false);
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

        private double GetUnitToMm()
        {
            int idx = _cmbUnit.SelectedIndex;
            if (idx == 1) return 10.0;
            if (idx == 2) return 1000.0;
            if (idx == 3) return 25.4;
            return 1.0;
        }

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

        // Есть ли STEP в списке входов
        private bool HasStepInput()
        {
            for (int i = 0; i < _lstInputs.Items.Count; i++)
            {
                string item = (string)_lstInputs.Items[i];
                string ext = Path.GetExtension(item);
                if (ext != null)
                {
                    string le = ext.ToLowerInvariant();
                    if (le == ".step" || le == ".stp")
                        return true;
                }
            }
            return false;
        }

        // Предупреждение о STEP при добавлении
        private void CheckStepWarning(string file)
        {
            string ext = Path.GetExtension(file);
            if (ext == null) return;
            string le = ext.ToLowerInvariant();
            if (le == ".step" || le == ".stp")
            {
                SetStatus("STEP пока не поддерживается в этой версии", false);
            }
        }

        // Автозаполнение папки вывода при первом входе
        private void AutoFillOutDir()
        {
            if (!string.IsNullOrEmpty(_txtOutDir.Text.Trim()))
                return;

            string dir = ComputeDefaultOutDir();
            if (!string.IsNullOrEmpty(dir))
                _txtOutDir.Text = dir;
        }

        // Вычисление папки вывода по умолчанию из первого входа
        private string ComputeDefaultOutDir()
        {
            if (_lstInputs.Items.Count == 0) return "";

            string first = (string)_lstInputs.Items[0];
            string baseDir;
            if (Directory.Exists(first))
            {
                baseDir = first.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            else
            {
                baseDir = Path.GetDirectoryName(first);
            }

            if (string.IsNullOrEmpty(baseDir))
                return "";

            return Path.Combine(baseDir, "cad2gdml_out");
        }

        private void SetControlsEnabled(bool en)
        {
            _btnAddFiles.Enabled = en;
            _btnAddFolder.Enabled = en;
            _btnRemove.Enabled = en;
            _cmbUnit.Enabled = en;
            _txtCrystal.Enabled = en;
            _cmbFront.Enabled = en;
            _txtOutDir.Enabled = en;
            _btnBrowseOut.Enabled = en;
            _btnConvert.Enabled = en;
            _chkAllowDefault.Enabled = en;
            _chkNoSplit.Enabled = en;
            _chkDropOpen.Enabled = en;
            _lstInputs.Enabled = en;
        }

        private void SetStatus(string text, bool ok)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = ok ? Color.Black : Color.Red;
        }

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
                    if (ext == ".stl" || ext == ".obj" || ext == ".3mf" || ext == ".step" || ext == ".stp")
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
