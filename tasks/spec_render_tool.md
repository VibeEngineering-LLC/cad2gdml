Напиши один C#-файл `Program.cs` (C# 7.3, .NET Framework 4.8, WinForms). Верни ТОЛЬКО код файла, без пояснений и без markdown-ограждений. Не больше 45 строк. Думай коротко, сразу пиши код.

Консольно-оконная утилита `Render.exe <файл.gdml> <выход.png> <ось 0|1|2> <положение 0..1>` — рисует трёхмерный просмотр сцены с сечением в PNG.

using: System, System.Drawing, System.Globalization, System.Windows.Forms, Cad2Gdml.App, Cad2Gdml.Import, Cad2Gdml.Scene.
Известные типы (не определяй): `Cad2Gdml.Scene.SceneModel` (конструктор без параметров; `List<Part> Parts`), `Cad2Gdml.Import.GdmlImporter.Load(string path, double unitToMm)` возвращает `IList<Part>`, `Cad2Gdml.App.MeshView` (класс Control; конструктор без параметров; методы и свойства: `SetScene(SceneModel)`, `bool SectionEnabled`, `int SectionAxis`, `double SectionPos`).

Действия: загрузить детали в SceneModel; создать `MeshView { Width = 700, Height = 900 }`; `SetScene`; включить сечение, ось и положение из аргументов (положение разобрать через `double.Parse(..., CultureInfo.InvariantCulture)`); поместить в `Form` с `ClientSize` 700×900, показать `form.Show()`, вызвать `Application.DoEvents()`; затем `view.DrawToBitmap(bmp, new Rectangle(0, 0, 700, 900))` в `Bitmap` и сохранить по пути из второго аргумента; вернуть 0. Метод `Main` с атрибутом `[STAThread]` в статическом классе `RenderProgram`. Если аргументов меньше четырёх — вывести справку в `MessageBox` не нужно, просто вернуть 2. Комментарии по-русски.
