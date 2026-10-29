using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Cad2Gdml.Geometry;
using Cad2Gdml.Materials;
using Cad2Gdml.Scene;

namespace Cad2Gdml.App
{
    // Форма просмотра сцены и назначения веществ
    public sealed class SceneViewerForm : Form
    {
        private readonly SceneModel _scene;
        private MeshView _meshView;
        private DataGridView _grid;
        private readonly List<string> _autoMats;
        private readonly List<string> _rowNames;
        private readonly List<bool> _isScenePart;
        private readonly List<string> _initialExcluded;
        private double _bmnX, _bmnY, _bmnZ, _bmxX, _bmxY, _bmxZ;
        private bool _suppressEvents;

        private CheckBox _chkSection;
        private ComboBox _cmbAxis;
        private TrackBar _trkPos;
        private Label _lblPos;
        private Button _btnClose;

        // Перекрывания веществ (заполняется при ОК)
        public List<KeyValuePair<string, string>> Overrides { get; }
        // Имена исключаемых строк (заполняется при ОК)
        public List<string> Excluded { get; }
        // Изменился ли набор исключений по сравнению с начальным
        public bool ExclusionChanged { get; private set; }

        public SceneViewerForm(SceneModel scene) : this(scene, null) { }

        public SceneViewerForm(SceneModel scene, IEnumerable<string> excludedNames)
        {
            _scene = scene;
            Overrides = new List<KeyValuePair<string, string>>();
            Excluded = new List<string>();
            _autoMats = new List<string>();
            _rowNames = new List<string>();
            _isScenePart = new List<bool>();
            _initialExcluded = excludedNames != null ? new List<string>(excludedNames) : new List<string>();

            Text = "Сцена и вещества";
            ClientSize = new Size(1200, 720);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            MinimumSize = new Size(900, 560);

            ComputeBbox();

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 520
            };

            BuildLeft(split.Panel1);
            BuildRight(split.Panel2);
            Controls.Add(split);

            CancelButton = _btnClose;

            // Начальное состояние скрытых
            UpdateHiddenFromGrid();
        }

        // Вычисление габаритов сцены
        private void ComputeBbox()
        {
            double minX = 0, minY = 0, minZ = 0;
            double maxX = 0, maxY = 0, maxZ = 0;
            bool has = false;

            if (_scene != null)
            {
                foreach (Part p in _scene.Parts)
                {
                    if (p.Mesh == null) continue;
                    Vec3d lo, hi;
                    p.Mesh.GetBounds(out lo, out hi);
                    if (!has)
                    {
                        minX = lo.X; minY = lo.Y; minZ = lo.Z;
                        maxX = hi.X; maxY = hi.Y; maxZ = hi.Z;
                        has = true;
                    }
                    else
                    {
                        if (lo.X < minX) minX = lo.X;
                        if (lo.Y < minY) minY = lo.Y;
                        if (lo.Z < minZ) minZ = lo.Z;
                        if (hi.X > maxX) maxX = hi.X;
                        if (hi.Y > maxY) maxY = hi.Y;
                        if (hi.Z > maxZ) maxZ = hi.Z;
                    }
                }
            }

            if (!has)
            {
                minX = -1; minY = -1; minZ = -1;
                maxX = 1; maxY = 1; maxZ = 1;
            }

            _bmnX = minX; _bmnY = minY; _bmnZ = minZ;
            _bmxX = maxX; _bmxY = maxY; _bmxZ = maxZ;
        }

        // ЛЕВАЯ панель: таблица + кнопки
        private void BuildLeft(Panel panel)
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4)
            };

            var btnApply = new Button { Text = "Применить и пересчитать", Width = 190, Height = 32, FlatStyle = FlatStyle.System };
            btnApply.Click += OnApply;

            var btnReset = new Button { Text = "Сбросить", Width = 90, Height = 32, FlatStyle = FlatStyle.System };
            btnReset.Click += OnReset;

            _btnClose = new Button { Text = "Закрыть", Width = 90, Height = 32, FlatStyle = FlatStyle.System };
            _btnClose.DialogResult = DialogResult.Cancel;
            _btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            flow.Controls.Add(btnApply);
            flow.Controls.Add(btnReset);
            flow.Controls.Add(_btnClose);
            panel.Controls.Add(flow);

            // Таблица
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // Колонка "В сцене"
            var colScene = new DataGridViewCheckBoxColumn
            {
                HeaderText = "В сцене",
                Width = 60,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = false
            };

            var colName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Деталь",
                FillWeight = 45,
                ReadOnly = true
            };
            var colRole = new DataGridViewTextBoxColumn
            {
                HeaderText = "Роль",
                FillWeight = 20,
                ReadOnly = true
            };
            var colMat = new DataGridViewComboBoxColumn
            {
                HeaderText = "Вещество",
                FillWeight = 35,
                FlatStyle = FlatStyle.Flat
            };

            // Заполнение Items
            var items = MaterialNames.RuLabels(MaterialLibrary.Default().Materials.Keys);
            foreach (string item in items)
                colMat.Items.Add(item);

            _grid.Columns.Add(colScene);
            _grid.Columns.Add(colName);
            _grid.Columns.Add(colRole);
            _grid.Columns.Add(colMat);

            _suppressEvents = true;

            // Заполнение строк: сначала детали сцены, затем дополнительные
            var sceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dupSeen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (_scene != null)
            {
                foreach (Part p in _scene.Parts)
                {
                    sceneNames.Add(p.Name);
                    string matRu = MaterialNames.ToRu(p.Material);
                    if (!colMat.Items.Contains(matRu))
                        colMat.Items.Add(matRu);

                    _rowNames.Add(p.Name);
                    _autoMats.Add(matRu);
                    _isScenePart.Add(true);
                    int seen;
                    dupSeen.TryGetValue(p.Name, out seen);
                    dupSeen[p.Name] = seen + 1;
                    string shown = seen == 0 ? p.Name : p.Name + " (" + (seen + 1) + ")";
                    _grid.Rows.Add(true, shown, RoleToRu(p.Role), matRu);
                }
            }

            // Дополнительные строки для исключаемых имён (не в сцене)
            if (_initialExcluded != null)
            {
                foreach (string name in _initialExcluded)
                {
                    if (sceneNames.Contains(name)) continue;
                    _rowNames.Add(name);
                    _autoMats.Add("");
                    _isScenePart.Add(false);
                    int rowIdx = _grid.Rows.Add(false, name, "—", "");
                    _grid.Rows[rowIdx].Cells[3].ReadOnly = true;
                    // Серый цвет для невыбранных
                    for (int c = 0; c < _grid.Rows[rowIdx].Cells.Count; c++)
                        _grid.Rows[rowIdx].Cells[c].Style.ForeColor = Color.Gray;
                }
            }

            _suppressEvents = false;

            // События
            _grid.CurrentCellDirtyStateChanged += OnCurrentCellDirtyStateChanged;
            _grid.CellValueChanged += OnCellValueChanged;
            _grid.EditingControlShowing += OnEditingControlShowing;
            _grid.DataError += OnDataError;
            _grid.SelectionChanged += OnSelectionChanged;
            _grid.CurrentCellChanged += OnSelectionChanged;

            panel.Controls.Add(_grid);
            panel.Controls.SetChildIndex(_grid, 0);
            panel.Controls.SetChildIndex(flow, 1);
        }

        // ПРАВАЯ панель: панель управления + MeshView
        private void BuildRight(Panel panel)
        {
            _meshView = new MeshView { Dock = DockStyle.Fill };
            _meshView.SetScene(_scene);

            var top = new Panel { Dock = DockStyle.Top, Height = 64 };

            _chkSection = new CheckBox { Text = "Сечение", Location = new Point(8, 10), AutoSize = true };
            _chkSection.CheckedChanged += OnSectionToggled;

            _cmbAxis = new ComboBox
            {
                Location = new Point(88, 10),
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbAxis.Items.AddRange(new object[] { "по оси X", "по оси Y", "по оси Z" });
            _cmbAxis.SelectedIndex = 1;
            _cmbAxis.SelectedIndexChanged += OnAxisChanged;

            _trkPos = new TrackBar
            {
                Location = new Point(206, 4),
                Width = 260,
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                TickFrequency = 10
            };
            _trkPos.Scroll += OnTrackChanged;

            _lblPos = new Label
            {
                Location = new Point(476, 12),
                AutoSize = true
            };
            UpdateLabel();

            var btnResetView = new Button { Text = "Сброс вида", Location = new Point(590, 8), Width = 88, Height = 28 };
            btnResetView.Click += (s, e) => _meshView.ResetView();

            var hint = new Label
            {
                Location = new Point(8, 42),
                AutoSize = true,
                ForeColor = Color.Gray,
                Text = "Мышь: левая — вращение, правая — сдвиг, колесо — масштаб"
            };

            top.Controls.Add(_chkSection);
            top.Controls.Add(_cmbAxis);
            top.Controls.Add(_trkPos);
            top.Controls.Add(_lblPos);
            top.Controls.Add(btnResetView);
            top.Controls.Add(hint);

            panel.Controls.Add(_meshView);
            panel.Controls.Add(top);
        }

        // Перевод роли на русский
        private static string RoleToRu(PartRole r)
        {
            switch (r)
            {
                case PartRole.Crystal: return "кристалл";
                case PartRole.Source: return "источник";
                case PartRole.Container: return "контейнер";
                case PartRole.Shield: return "защита";
                case PartRole.Housing: return "корпус";
                default: return "прочее";
            }
        }

        // Мгновенная отправка чекбокса
        private void OnCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell.ColumnIndex == 0)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // Реакция на переключение чекбокса
        private void OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppressEvents) return;
            if (e.ColumnIndex != 0) return;
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;

            bool isChecked = (bool)_grid.Rows[e.RowIndex].Cells[0].Value;
            Color fc = isChecked ? Color.Black : Color.Gray;

            for (int c = 0; c < _grid.Rows[e.RowIndex].Cells.Count; c++)
                _grid.Rows[e.RowIndex].Cells[c].Style.ForeColor = fc;

            UpdateHiddenFromGrid();
        }

        // Обновление Hidden в MeshView по состоянию чекбоксов
        private void UpdateHiddenFromGrid()
        {
            if (_meshView == null) return;
            _meshView.Hidden.Clear();
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                object val = _grid.Rows[i].Cells[0].Value;
                bool isChecked = val != null && (bool)val;
                if (!isChecked)
                    _meshView.Hidden.Add(_rowNames[i]);
            }
            _meshView.RefreshHidden();
        }

        // Режим ввода для combo
        private void OnEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            ComboBox cmb = e.Control as ComboBox;
            if (cmb != null)
                cmb.DropDownStyle = ComboBoxStyle.DropDown;
        }

        // Игнорирование ошибок данных
        private void OnDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        // Подсветка при выборе строки
        private void OnSelectionChanged(object sender, EventArgs e)
        {
            if (_grid.CurrentRow != null)
            {
                int idx = _grid.CurrentRow.Index;
                if (idx >= 0 && idx < _rowNames.Count && _isScenePart[idx])
                    _meshView.Highlight = _rowNames[idx];
                else
                    _meshView.Highlight = null;
            }
            else
            {
                _meshView.Highlight = null;
            }
        }

        // Сброс значений веществ
        private void OnReset(object sender, EventArgs e)
        {
            _suppressEvents = true;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (!_isScenePart[i]) continue;
                _grid.Rows[i].Cells[3].Value = _autoMats[i];
            }
            _suppressEvents = false;
        }

        // Применение перекрываний и исключений
        private void OnApply(object sender, EventArgs e)
        {
            Excluded.Clear();
            Overrides.Clear();

            var excludedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                object val = _grid.Rows[i].Cells[0].Value;
                bool isChecked = val != null && (bool)val;

                if (!isChecked)
                {
                    Excluded.Add(_rowNames[i]);
                    excludedSet.Add(_rowNames[i]);
                }
                else
                {
                    // Только детали сцены участвуют в overrides
                    if (!_isScenePart[i]) continue;
                    string cur = _grid.Rows[i].Cells[3].Value as string;
                    if (string.IsNullOrEmpty(cur)) continue;
                    if (cur != _autoMats[i])
                    {
                        Overrides.Add(new KeyValuePair<string, string>("=" + _rowNames[i], MaterialNames.ToId(cur)));
                    }
                }
            }

            // Сравнение с начальным набором исключений
            var initialSet = new HashSet<string>(_initialExcluded, StringComparer.OrdinalIgnoreCase);
            ExclusionChanged = !excludedSet.SetEquals(initialSet);

            DialogResult = DialogResult.OK;
            Close();
        }

        // Событие чекбокса сечения
        private void OnSectionToggled(object sender, EventArgs e)
        {
            _meshView.SectionEnabled = _chkSection.Checked;
            UpdateLabel();
        }

        // Событие выбора оси
        private void OnAxisChanged(object sender, EventArgs e)
        {
            _meshView.SectionAxis = _cmbAxis.SelectedIndex;
            UpdateLabel();
        }

        // Событие ползунка
        private void OnTrackChanged(object sender, EventArgs e)
        {
            _meshView.SectionPos = _trkPos.Value / 100.0;
            UpdateLabel();
        }

        // Обновление подписи позиции в мм
        private void UpdateLabel()
        {
            int axis = _cmbAxis.SelectedIndex;
            double frac = _trkPos.Value / 100.0;
            double mm;
            char ch;
            if (axis == 0)
            {
                mm = _bmnX + frac * (_bmxX - _bmnX);
                ch = 'x';
            }
            else if (axis == 1)
            {
                mm = _bmnY + frac * (_bmxY - _bmnY);
                ch = 'y';
            }
            else
            {
                mm = _bmnZ + frac * (_bmxZ - _bmnZ);
                ch = 'z';
            }
            _lblPos.Text = ch + " = " + mm.ToString("F1", CultureInfo.InvariantCulture) + " мм";
        }
    }
}
