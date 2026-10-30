using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Cad2Gdml.Materials;

namespace Cad2Gdml.App
{
    public sealed partial class SceneViewerForm : Form
    {
        // Вещества, заданные вручную, общие для всей сессии окна (MainForm передаёт их в конвейер)
        public static readonly List<MaterialDef> SessionCustom = new List<MaterialDef>();
        public List<MaterialDef> NewMaterials { get { return SessionCustom; } }

        // Вставляет готовые вещества в библиотеку и комбобокс
        public void SetInitialCustom(IEnumerable<MaterialDef> defs)
        {
            if (defs == null) return;
            foreach (var def in defs)
            {
                _lib.AddCustom(new List<MaterialDef> { def });
                if (!_colMat.Items.Contains(def.Name))
                    _colMat.Items.Add(def.Name);
            }
        }

        // Обработчик кнопки «Своё вещество…»
        private void OnCustomMaterial(object sender, EventArgs e)
        {
            using (var dlg = new CustomMaterialDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result == null)
                    return;

                var def = dlg.Result;

                // В библиотеку
                _lib.AddCustom(new List<MaterialDef> { def });

                // Замена или добавление в NewMaterials (без учёта регистра)
                bool found = false;
                for (int i = 0; i < NewMaterials.Count; i++)
                {
                    if (string.Equals(NewMaterials[i].Name, def.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        NewMaterials[i] = def;
                        found = true;
                        break;
                    }
                }
                if (!found) NewMaterials.Add(def);

                // Имя в комбобокс, если ещё нет
                if (!_colMat.Items.Contains(def.Name))
                    _colMat.Items.Add(def.Name);

                // Заполняем текущую строку, если это строка сцены
                if (_grid.CurrentRow != null)
                {
                    int idx = _grid.CurrentRow.Index;
                    if (idx >= 0 && idx < _isScenePart.Count && _isScenePart[idx])
                    {
                        _suppressEvents = true;
                        _grid.Rows[idx].Cells[3].Value = def.Name;
                        _grid.Rows[idx].Cells[4].Value = def.DensityGcm3.ToString("0.####", CultureInfo.InvariantCulture);
                        _suppressEvents = false;
                    }
                }
            }
        }
    }
}
