Напиши один C#-файл (C# 7.3, .NET Framework 4.8, без внешних пакетов). Верни ТОЛЬКО код файла, без пояснений и без markdown-ограждений. Думай коротко и сразу пиши код. Не больше 260 строк.

using System; System.Collections.Generic; System.Globalization; System.IO; System.Xml.Linq; Cad2Gdml.Geometry; Cad2Gdml.Meshes; Cad2Gdml.Scene.
namespace Cad2Gdml.Import
`public static class GdmlImporter`. Известные типы (не определяй): `Vec3d` (`new Vec3d(x,y,z)`, поля X,Y,Z), `Mesh` (`new Mesh()`, `AddTriangle`, `Clone()`, `Transformed(Affine3 t)`), `Part` (свойства Name, OrigName, Mesh, Material, SourceMaterial, SourceFile). Построение сеток уже есть, его вызывай, НЕ пиши:
 `GdmlMeshing.BuildBox(double x,double y,double z)`, `GdmlMeshing.BuildTube(double rmin,double rmax,double z,double startPhi,double deltaPhi)`, `GdmlMeshing.BuildPolycone(double startPhi,double deltaPhi,IList<double[]> planes)` (planes[i]={z,rmin,rmax}), все возвращают Mesh в мм, углы в радианах. Преобразование вершин делай сам: свой метод `Vec3d Apply(double[] m, Vec3d v, double px, double py, double pz)` (m — матрица 3x3 по строкам, 9 чисел) и построй новый Mesh через `AddTriangle`, вершины читать из `mesh.GetTriangle(i, out Vec3d a, out Vec3d b, out Vec3d c)`, число треугольников `mesh.TriangleCount`.

API:
 `public static IList<Part> Load(string path, double unitToMm)` — unitToMm игнорируй; вызывает `Load(path, out var skipped)` и возвращает список.
 `public static IList<Part> Load(string path, out IList<string> skipped)` — skipped: имена тел неподдерживаемых типов.
Ошибка разбора (невалидный XML, нет solids/structure/setup/world, число не читается, NaN/Infinity) → `InvalidDataException` с русским сообщением.

Алгоритм:
1. Загрузка `XDocument.Load(path)`; имена элементов сравнивай по `LocalName` (пространство имён игнорируй).
2. Единицы длины lunit: mm=1, cm=10, m=1000, um=0.001, пусто → mm. Угол aunit: deg (по умолчанию) или rad. Числа: `double.Parse(s, CultureInfo.InvariantCulture)`; отсутствующий атрибут → 0, кроме deltaphi (по умолчанию 360 градусов).
3. `solids`: словарь имя → `Func<Mesh>` или готовая Mesh в системе тела: `box` (x,y,z), `tube` (rmin,rmax,z; startphi, deltaphi), `polycone` (startphi, deltaphi, дочерние `zplane` z,rmin,rmax). Единицы в мм после умножения на lunit. Остальные виды твёрдых тел — в skipped.
4. `structure`: словарь имя volume → (materialref ref, solidref ref, список physvol с volumeref ref, position x/y/z с unit (по умолчанию mm), rotation x/y/z с unit (по умолчанию rad!)). Позиция и поворот могут быть заданы и как `positionref`/`rotationref` на `<define>` — поддержать: прочитать `define/position` и `define/rotation` в словари по имени.
5. Корень: `setup/world ref`. Обход рекурсивный (глубина не более 64, иначе InvalidDataException). Мировой объём в результат не входит. У каждого physvol: преобразование Geant4 (G4GDMLReadStructure): вершина тела v ↦ R⁻¹·v + p, где R = Rz·Ry·Rx по углам rotation (x,y,z), p = position. Матрица R⁻¹ = Rx(-ax)·Ry(-ay)·Rz(-az)... проверь порядок: R⁻¹ = (Rz·Ry·Rx)⁻¹ = Rx⁻¹·Ry⁻¹·Rz⁻¹. Накопление для вложенных physvol: полное преобразование = внешнее ∘ внутреннее (вершина сначала внутренним, затем внешним), т.е. новая матрица M = M_outer · M_inner, смещение p = M_outer·p_inner + p_outer.
6. Каждый physvol, чей volume ссылается на тело и не мировой — одна Part: Name = имя volume (при повторе добавь суффикс `_2`, `_3`), OrigName = имя volume, Material = materialref ref, SourceMaterial = то же, SourceFile = path, Mesh = преобразованная сетка. Если у volume есть вложенные physvol — обойти и их. Part без треугольников пропустить.
Комментарии по-русски. Без Console, без статического состояния.
