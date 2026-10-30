using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Cad2Gdml.Pipeline;

namespace Cad2Gdml.App
{
    public sealed partial class MainForm : Form
    {
        // Кнопка «Заменить…», #CG-37
        private void OnReplaceSelected(object sender, EventArgs e)
        {
            // Сбор индексов выбранных элементов
            var sel = new List<int>();
            for (int i = 0; i < _lstInputs.Items.Count; i++)
            {
                if (_lstInputs.GetSelected(i))
                    sel.Add(i);
            }

            // Проверка: ровно один выбранный
            int idx;
            string hint;
            if (!InputListOps.TryPickOne(_lstInputs.Items.Count, sel, out idx, out hint))
            {
                SetStatus(hint, false);
                return;
            }

            // Диалог выбора нового файла
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "3D-файлы|*.stl;*.obj;*.3mf;*.step;*.stp;*.blend|Все|*.*";
                if (!string.IsNullOrEmpty(_lastFolder) && Directory.Exists(_lastFolder))
                    dlg.InitialDirectory = _lastFolder;

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                // Копируем элементы и пробую заменить
                var items = new List<string>(_lstInputs.Items.Count);
                for (int i = 0; i < _lstInputs.Items.Count; i++)
                    items.Add((string)_lstInputs.Items[i]);

                if (!InputListOps.TryReplace(items, idx, dlg.FileName, out hint))
                {
                    SetStatus(hint, false);
                    return;
                }

                // Применяем результат
                _lstInputs.Items[idx] = dlg.FileName;
                _lastFolder = Path.GetDirectoryName(dlg.FileName);
                InvalidateOverrides();
                SetStatus("Заменено: " + Path.GetFileName(dlg.FileName) + ". Нажмите «Конвертировать».", true);
            }
        }
    }
}
