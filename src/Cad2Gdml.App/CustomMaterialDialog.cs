using System;
using System.Drawing;
using System.Windows.Forms;
using Cad2Gdml.Materials;

namespace Cad2Gdml.App
{
    // Диалог «Своё вещество» (#CG-32)
    public sealed class CustomMaterialDialog : Form
    {
        private TextBox _txtName;
        private TextBox _txtDensity;
        private TextBox _txtComp;
        private Label _lblError;
        private Button _btnOk;

        public MaterialDef Result { get; private set; }

        public CustomMaterialDialog()
        {
            Text = "Своё вещество";
            ClientSize = new Size(460, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);

            int left = 12;
            int width = ClientSize.Width - left * 2;

            // Имя
            var lblName = new Label
            {
                Text = "Имя (латиница, например MyScint):",
                Location = new Point(left, 10),
                AutoSize = true
            };
            _txtName = new TextBox
            {
                Location = new Point(left, 32),
                Width = width
            };

            // Плотность
            var lblDensity = new Label
            {
                Text = "Плотность, г/см³:",
                Location = new Point(left, 62),
                AutoSize = true
            };
            _txtDensity = new TextBox
            {
                Location = new Point(left, 84),
                Width = width
            };

            // Состав
            var lblComp = new Label
            {
                Text = "Состав: элемент=массовая доля, через «;» или с новой строки.\nСумма = 1 ± 0,0001. Пример: La=0.36688; Br=0.63312",
                Location = new Point(left, 114),
                Width = width,
                Height = 34,
                AutoSize = false
            };
            _txtComp = new TextBox
            {
                Location = new Point(left, 150),
                Width = width,
                Height = 110,
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical
            };

            // Ошибка (красная)
            _lblError = new Label
            {
                Text = string.Empty,
                Location = new Point(left, 266),
                Width = width,
                Height = 36,
                ForeColor = Color.Red,
                AutoSize = false
            };

            // Кнопки
            int btnW = 80;
            int btnH = 28;
            int btnY = 304;
            int okX = ClientSize.Width - left - btnW;
            int cancelX = okX - 10 - btnW;

            _btnOk = new Button
            {
                Text = "ОК",
                Location = new Point(okX, btnY),
                Size = new Size(btnW, btnH)
            };
            _btnOk.Click += OnOkClick;

            var btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(cancelX, btnY),
                Size = new Size(btnW, btnH),
                DialogResult = DialogResult.Cancel
            };

            // Собираем форму
            Controls.Add(lblName);
            Controls.Add(_txtName);
            Controls.Add(lblDensity);
            Controls.Add(_txtDensity);
            Controls.Add(lblComp);
            Controls.Add(_txtComp);
            Controls.Add(_lblError);
            Controls.Add(_btnOk);
            Controls.Add(btnCancel);

            AcceptButton = _btnOk;
            CancelButton = btnCancel;
        }

        // Обработчик ОК: валидация, ошибка → остаёмся, успех → закрываем
        private void OnOkClick(object sender, EventArgs e)
        {
            MaterialDef def;
            string err;
            if (CustomMaterialParser.TryParse(_txtName.Text, _txtDensity.Text, _txtComp.Text, out def, out err))
            {
                Result = def;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _lblError.Text = err;
            }
        }
    }
}
