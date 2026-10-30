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
    public sealed partial class SceneViewerForm : Form
    {
        private DataGridViewComboBoxColumn _colMat;
        private readonly SceneModel _scene;
        private MeshView _meshView;
        private DataGridView _grid;
        private readonly List<string> _autoMats;
        private readonly List<string> _autoRoles;
        private readonly List<string> _rowNames;
        private readonly List<bool> _isScenePart;
        private readonly List<string> _initialExcluded;
        private readonly List<string> _autoDens;
        private readonly MaterialLibrary _lib;
        private double _bmnX, _bmnY, _bmnZ, _bmxX, _bmxY, _bmxZ;
        private bool _suppressEvents;

        private CheckBox _chkSection;
        private ComboBox _cmbAxis;
        private TrackBar _trkPos;
        private Label _lblPos;
        private Button _btnClose;

        // Перекрывания веществ (заполняется при ОК)
        public List<KeyValuePair<string, string>> Overrides { get; }
        // Перекрывания ролей (заполняется при ОК)
        public List<KeyValuePair<string, string>> RoleOverrides { get; }
        // Перекрывания плотностей (заполняется при ОК)
        public List<KeyValuePair<string, double>> DensityOverrides { get; }
        // Имена исключаемых строк (заполняется при ОК)
        public List<string> Excluded { get; }
        // Изменился ли набор исключений по сравнению с начальным
        public bool ExclusionChanged { get; private set; }

        public SceneViewerForm(SceneModel scene) : this(scene, null) { }

        public SceneViewerForm(SceneModel scene, IEnumerable<string> excludedNames)
        {
            _scene = scene;
            _lib = MaterialLibrary.Default();
            Overrides = new List<KeyValuePair<string, string>>();
            RoleOverrides = new List<KeyValuePair<string, string>>();
            DensityOverrides = new List<KeyValuePair<string, double>>();
            Excluded = new List<string>();
            _autoMats = new List<string>();
            _autoRoles = new List<string>();
            _autoDens = new List<string>();
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

        // Плотность по id из библиотеки; пустая строка если не найдено
        private static string DensityText(MaterialLibrary lib, string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            MaterialDef md = lib.Find(id);
            if (md == null) return "";
            return md.DensityGcm3.ToString("0.####", CultureInfo.InvariantCulture);
        }

        // Разделение производного имени (id_rho1.23) на базу и плотность
        private static void SplitDerived(MaterialLibrary lib, string id, out string baseId, out string rhoText)
        {
            if (string.IsNullOrEmpty(id))
            {
                baseId = id;
                rhoText = "";
                return;
            }
            int pos = id.LastIndexOf("_rho", StringComparison.Ordinal);
            if (pos > 0)
            {
                string b = id.Substring(0, pos);
                string r = id.Substring(pos + 4);
                if (lib.Find(b) != null)
                {
                    baseId = b;
                    rhoText = r;
                }
                else if (lib.Find("G4_" + b) != null)
                {
                    baseId = "G4_" + b;
                    rhoText = r;
                }
                else
                {
                    baseId = id;
                    rhoText = DensityText(lib, id);
                }
            }
            else
            {
                baseId = id;
                rhoText = DensityText(lib, id);
            }
        }

        // Установка начальных плотностей извне (вызывается до ShowDialog)
        public void SetInitialDensities(List<KeyValuePair<string, double>> dens)
        {
            if (dens == null) return;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (!_isScenePart[i]) continue;
                string matText = _grid.Rows[i].Cells[3].Value as string;
                string id = MaterialNames.ToId(matText);
                foreach (KeyValuePair<string, double> kv in dens)
                {
                    if (string.Equals(kv.Key, id, StringComparison.OrdinalIgnoreCase))
                    {
                        _grid.Rows[i].Cells[4].Value = kv.Value.ToString("0.####", CultureInfo.InvariantCulture);
                        break;
                    }
                }
            }
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

            var btnApply = new Button { Text = "Применить и пересчитать", Width = 165, Height = 32, FlatStyle = FlatStyle.System };
            btnApply.Click += OnApply;

            var btnCustom = new Button { Text = "Своё вещество…", Width = 125, Height = 32, FlatStyle = FlatStyle.System };
            btnCustom.Click += OnCustomMaterial;

            var btnReset = new Button { Text = "Сбросить", Width = 90, Height = 32, FlatStyle = FlatStyle.System };
            btnReset.Click += OnReset;

            _btnClose = new Button { Text = "Закрыть", Width = 90, Height = 32, FlatStyle = FlatStyle.System };
            _btnClose.DialogResult = DialogResult.Cancel;
            _btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            flow.Controls.Add(btnApply);
            flow.Controls.Add(btnCustom);
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

            // Колонка "Роль"
            var colRole = new DataGridViewComboBoxColumn
            {
                HeaderText = "Роль",
                FillWeight = 20,
                FlatStyle = FlatStyle.Flat
            };
            colRole.Items.AddRange(new object[] { "кристалл", "источник", "контейнер", "защита", "корпус", "прочее", "—" });

            var colMat = _colMat = new DataGridViewComboBoxColumn
            {
                HeaderText = "Вещество",
                FillWeight = 35,
                FlatStyle = FlatStyle.Flat
            };

            // Колонка "Плотность, г/см³"
            var colDens = new DataGridViewTextBoxColumn
            {
                HeaderText = "Плотность, г/см³",
                FillWeight = 15,
                ReadOnly = false
            };

            // Заполнение Items
            var items = MaterialNames.RuLabels(MaterialLibrary.Default().Materials.Keys);
            foreach (string item in items)
                colMat.Items.Add(item);
            SetInitialCustom(SessionCustom);

            _grid.Columns.Add(colScene);
            _grid.Columns.Add(colName);
            _grid.Columns.Add(colRole);
            _grid.Columns.Add(colMat);
            _grid.Columns.Add(colDens);

            _suppressEvents = true;

            // Заполнение строк: сначала детали сцены, затем дополнительные
            var sceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dupSeen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (_scene != null)
            {
                foreach (Part q in _scene.Parts)
                {
                    int c;
                    dupSeen.TryGetValue(q.Name, out c);
                    dupSeen[q.Name] = c + 1;
                }
                foreach (Part p in _scene.Parts)
                {
                    sceneNames.Add(p.Name);

                    // Разделение производного имени
                    string baseId, rhoText;
                    SplitDerived(_lib, p.Material, out baseId, out rhoText);

                    string matRu = MaterialNames.ToRu(baseId);
                    if (!colMat.Items.Contains(matRu))
                        colMat.Items.Add(matRu);

                    string roleRu = RoleToRu(p.Role);

                    _rowNames.Add(p.Name);
                    _autoMats.Add(matRu);
                    _autoRoles.Add(roleRu);
                    _autoDens.Add(DensityText(_lib, baseId));
                    _isScenePart.Add(true);
                    int total;
                    dupSeen.TryGetValue(p.Name, out total);
                    string shown = total > 1 ? p.Name + " [одноимённых: " + total + "; действие для всех]" : p.Name;
                    _grid.Rows.Add(true, shown, roleRu, matRu, rhoText);
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
                    _autoRoles.Add("—");
                    _autoDens.Add("");
                    _isScenePart.Add(false);
                    int rowIdx = _grid.Rows.Add(false, name, "—", "", "");
                    _grid.Rows[rowIdx].Cells[2].ReadOnly = true;
                    _grid.Rows[rowIdx].Cells[3].ReadOnly = true;
                    _grid.Rows[rowIdx].Cells[4].ReadOnly = true;
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

        // Перевод русского имени роли обратно в enum-имя
        private static string RuToRoleName(string ru)
        {
            if (ru == "кристалл") return "Crystal";
            if (ru == "источник") return "Source";
            if (ru == "контейнер") return "Container";
            if (ru == "защита") return "Shield";
            if (ru == "корпус") return "Housing";
            if (ru == "прочее") return "Other";
            return null;
        }

        // Мгновенная отправка чекбокса
        private void OnCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell.ColumnIndex == 0)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // Реакция на изменение ячейки
        private void OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppressEvents) return;
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;

            // Изменение вещества — обновляем плотность по умолчанию
            if (e.ColumnIndex == 3)
            {
                if (_isScenePart[e.RowIndex])
                {
                    string newMat = _grid.Rows[e.RowIndex].Cells[3].Value as string;
                    if (newMat != null)
                    {
                        string matId = MaterialNames.ToId(newMat);
                        _grid.Rows[e.RowIndex].Cells[4].Value = DensityText(_lib, matId);
                    }
                }
                return;
            }

            // Чекбокс "В сцене"
            if (e.ColumnIndex != 0) return;

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

        // Режим ввода для combo (роль + вещество)
        private void OnEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            ComboBox cmb = e.Control as ComboBox;
            if (cmb == null) return;
            cmb.DropDownStyle = ComboBoxStyle.DropDown;
            cmb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmb.AutoCompleteSource = AutoCompleteSource.ListItems;
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

        // Сброс значений веществ, ролей и плотностей
        private void OnReset(object sender, EventArgs e)
        {
            _suppressEvents = true;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (!_isScenePart[i]) continue;
                _grid.Rows[i].Cells[2].Value = _autoRoles[i];
                _grid.Rows[i].Cells[3].Value = _autoMats[i];
                _grid.Rows[i].Cells[4].Value = _autoDens[i];
            }
            _suppressEvents = false;
        }

        // Применение перекрываний, исключений и плотностей
        private void OnApply(object sender, EventArgs e)
        {
            Excluded.Clear();
            Overrides.Clear();
            RoleOverrides.Clear();
            DensityOverrides.Clear();

            var excludedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var densDict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                object val = _grid.Rows[i].Cells[0].Value;
                bool isChecked = val != null && (bool)val;

                if (!isChecked)
                {
                    Excluded.Add(_rowNames[i]);
                    excludedSet.Add(_rowNames[i]);
                    continue;
                }

                // Только детали сцены участвуют в overrides
                if (!_isScenePart[i]) continue;

                // Перекрывание вещества
                string cur = _grid.Rows[i].Cells[3].Value as string;
                if (!string.IsNullOrEmpty(cur) && cur != _autoMats[i])
                    Overrides.Add(new KeyValuePair<string, string>("=" + _rowNames[i], MaterialNames.ToId(cur)));

                // Перекрывание роли
                string roleText = _grid.Rows[i].Cells[2].Value as string;
                if (!string.IsNullOrEmpty(roleText) && roleText != _autoRoles[i])
                {
                    string roleEng = RuToRoleName(roleText);
                    if (roleEng != null)
                        RoleOverrides.Add(new KeyValuePair<string, string>(_rowNames[i], roleEng));
                }

                // Плотность
                string matText = _grid.Rows[i].Cells[3].Value as string;
                string id = MaterialNames.ToId(matText);
                string dtxt = _grid.Rows[i].Cells[4].Value as string;
                if (!string.IsNullOrEmpty(dtxt))
                {
                    dtxt = dtxt.Replace(',', '.');
                    double dv;
                    if (double.TryParse(dtxt, NumberStyles.Float, CultureInfo.InvariantCulture, out dv) && dv > 0.0)
                    {
                        MaterialDef md = _lib.Find(id);
                        if (md == null || Math.Abs(md.DensityGcm3 - dv) > 1e-9)
                            densDict[id] = dv;
                    }
                }
            }

            foreach (KeyValuePair<string, double> kv in densDict)
                DensityOverrides.Add(kv);

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
