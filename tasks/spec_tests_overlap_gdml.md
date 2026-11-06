Напиши один C#-файл `ChecksOverlap.cs` (C# 7.3, .NET Framework 4.8, без внешних пакетов). Верни ТОЛЬКО код файла, без пояснений и без markdown-ограждений. Думай коротко, сразу пиши код. Не больше 320 строк.

using: System, System.Collections.Generic, System.Globalization, System.IO, System.Linq, Cad2Gdml.Geometry, Cad2Gdml.Import, Cad2Gdml.Meshes, Cad2Gdml.Scene.
namespace Cad2Gdml.Tests
Публичный статический класс `ChecksOverlap` с методом `public static List<KeyValuePair<string, Action>> All(string tempDir)` — точно по образцу:
  var list = new List<KeyValuePair<string, Action>>();
  list.Add(new KeyValuePair<string, Action>("имя_теста", () => Метод(tempDir)));  ... return list;
Тест падает исключением: `throw new Exception("имя_теста: описание")`.

Известные типы (не определяй): `Vec3d` (`new Vec3d(x,y,z)`, поля X, Y, Z только для чтения), `Mesh` (`new Mesh()`, `AddTriangle(Vec3d a, Vec3d b, Vec3d c)`, `int TriangleCount`, `void GetTriangle(int i, out Vec3d a, out Vec3d b, out Vec3d c)`, `void FlipWinding()`, `void Append(Mesh other)`), `MeshWelder.Weld(Mesh src, double tolerance)` возвращает Mesh, `MeshTopology.Analyze(Mesh welded)` возвращает объект с `bool IsClosedManifold`, `Part` (свойства Name, Mesh, MeshOpen), `SceneModel` (`List<Part> Parts`, `List<Diagnostic> Diagnostics`; у `Diagnostic` свойства `Code`, `PartName`, `Message`), `OverlapCheck.Run(SceneModel scene)`, `GdmlImporter.Load(string path, out IList<string> skipped)` возвращает `IList<Part>`.

Вспомогательные методы внутри класса:
- `Mesh Box(double x0, double y0, double z0, double x1, double y1, double z1)` — замкнутая коробка, 8 вершин, 12 треугольников, нормали НАРУЖУ (против часовой при взгляде снаружи). Сам проверь порядок вершин векторным произведением.
- `SceneModel Scene(params Part[] parts)` и `Part P(string name, Mesh m)`.
- `int Count(SceneModel s, string code)` — число диагностик с кодом.
- `double Volume(Mesh m)` — объём по сумме знаковых объёмов тетраэдров (a·(b×c))/6 по всем треугольникам, берётся модуль результата.

Тесты (имена точно такие):
1. `overlap_boxes_flagged`: A = Box(0,0,0,10,10,10), B = Box(7,2,2,17,8,8) (проникновение 3). После `OverlapCheck.Run`: `Count(s,"W_OVERLAP") == 1`, `Count(s,"W_INSIDE") == 0`, и в PartName диагностики W_OVERLAP есть оба имени.
2. `overlap_touch_face_ok`: A = Box(0,0,0,10,10,10), B = Box(10,0,0,20,10,10) (общая грань). W_OVERLAP == 0 и W_INSIDE == 0.
3. `overlap_far_ok`: коробки далеко друг от друга — оба кода 0.
4. `overlap_inside_flagged`: A = Box(0,0,0,100,100,100), B = Box(40,40,40,60,60,60). W_OVERLAP == 0, W_INSIDE == 1, PartName этой диагностики равен имени B.
5. `overlap_cavity_ok`: тело с полостью: Mesh outer = Box(0,0,0,100,100,100); Mesh cav = Box(20,20,20,80,80,80); cav.FlipWinding(); outer.Append(cav) (одна деталь A). B = Box(40,40,40,60,60,60) лежит в полости. Оба кода 0.
6. `overlap_open_mesh_skipped`: как в тесте 1, но у B `MeshOpen = true` — оба кода 0.
7. `overlap_gap_vs_penetration` (точечная проверка границы): A = Box(0,0,0,10,10,10); B1 = Box(9.5,2,2,19.5,8,8) (проникновение 0.5) → W_OVERLAP == 1; B2 = Box(10.5,2,2,20.5,8,8) (зазор 0.5) → W_OVERLAP == 0. Две отдельные сцены.
8. GDML-тесты: во временной папке `tempDir` (создать `Directory.CreateDirectory`) записать файл `ov_test.gdml` (текст GDML, строка в коде с `\"`). Содержимое: `<gdml><define/><materials/><solids>` с `<tube name="T1" lunit="mm" aunit="deg" rmin="0" rmax="10" z="20" startphi="0" deltaphi="360"/>`, `<polycone name="PC" lunit="mm" aunit="deg" startphi="0" deltaphi="360">` с двумя `zplane` (z=-10, rmin=5, rmax=10) и (z=10, rmin=5, rmax=10), `<box name="W" lunit="mm" x="200" y="200" z="200"/>`, затем `<structure>` с `<volume name="T1v"><materialref ref="G4_Pb"/><solidref ref="T1"/></volume>`, `<volume name="PCv">` аналогично с solidref PC, `<volume name="World"><materialref ref="G4_AIR"/><solidref ref="W"/>` и внутри два `<physvol name="a"><volumeref ref="T1v"/><position x="0" y="0" z="0" unit="mm"/></physvol>` и `<physvol name="b"><volumeref ref="PCv"/><position x="6" y="0" z="0" unit="mm"/></physvol>`, `</volume></structure><setup name="Default" version="1.0"><world ref="World"/></setup></gdml>`. Тесты:
  a) `gdml_import_counts_and_closed`: `GdmlImporter.Load` возвращает ровно 2 детали (T1v и PCv), у каждой `MeshTopology.Analyze(MeshWelder.Weld(mesh, 1e-6)).IsClosedManifold == true`.
  b) `gdml_polycone_volume`: объём детали PCv (поликонус-труба rmin 5, rmax 10, высота 20) близок к точному π·(10²−5²)·20 ≈ 4712.39 с поправкой на многоугольник из 48 сегментов (множитель sin(2π/48)·48/(2π) ≈ 0.99715): допуск ±0.5 % от 4712.39·0.99715. Объём T1v (цилиндр r=10, h=20): π·100·20·0.99715, тот же допуск.
  c) `gdml_overlap_detected`: детали из Load положить в SceneModel, `OverlapCheck.Run` → ровно один W_OVERLAP (цилиндр и труба со смещением 6 мм пересекаются стенкой).
Комментарии по-русски.
