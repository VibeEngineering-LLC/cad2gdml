Напиши один C#-файл (C# 7.3, .NET Framework 4.8, без внешних пакетов): класс `GdmlImporter` в пространстве имён `Cad2Gdml.Import`.
Верни ТОЛЬКО код файла, без пояснений и без markdown-ограждений.

using: System, System.Collections.Generic, System.Globalization, System.IO, System.Xml.Linq, Cad2Gdml.Geometry, Cad2Gdml.Meshes, Cad2Gdml.Scene.

Известные типы (не определяй их заново):
- `Cad2Gdml.Geometry.Vec3d` — конструктор `new Vec3d(double x, double y, double z)`.
- `Cad2Gdml.Meshes.Mesh` — `new Mesh()`, `void AddTriangle(Vec3d a, Vec3d b, Vec3d c)`.
- `Cad2Gdml.Scene.Part` — свойства `Name`, `OrigName`, `Mesh`, `Material` (string), `SourceFile`, `SourceMaterial`.

Публичный API: `public static IList<Part> Load(string path, double unitToMm)`. Параметр unitToMm игнорируй: единицы берутся из атрибутов `lunit`/`aunit` GDML. Исключения при ошибке разбора: `InvalidDataException` с русским сообщением.

Разбор GDML (XDocument.Load, имена элементов без пространства имён):
1. `<solids>`: поддержать `box` (x,y,z — полные длины), `tube` (rmin,rmax,z — полная длина, startphi,deltaphi), `polycone` (startphi,deltaphi, дочерние `zplane` с z,rmin,rmax). Единицы lunit: mm=1, cm=10, m=1000 (по умолчанию mm). aunit: deg (по умолчанию) или rad. Остальные виды твёрдых тел пропускай и считай их в списке пропущенных.
2. `<structure>`: `<volume name>` с `materialref ref` и `solidref ref`, внутри `physvol` с `volumeref ref`, необязательными `position` (x,y,z, unit) и `rotation` (x,y,z, unit; углы по правилу Geant4: поворот пассивный, применять обратное вращение Rz*Ry*Rx к вершинам, как делает G4GDMLReadStructure).
3. Корень — `<setup><world ref=...>`. Обойти дерево от мирового объёма рекурсивно с накоплением смещения и поворота. Сам мировой объём в результат НЕ включать (его тело — коробка-воздух). Каждый физический экземпляр (physvol) = одна `Part`: `Name` = имя volume (при повторах добавлять `_2`, `_3`…), `OrigName` = имя volume, `Material` = значение `materialref ref`, `SourceMaterial` = то же, `SourceFile` = path. Если у volume есть своё `physvol` внутри (вложенность), обработать их тоже (с накоплением преобразования).
4. Сетка каждой детали — замкнутая, треугольники с наружными нормалями (обход против часовой стрелки при взгляде снаружи): коробка — 12 треугольников; tube/polycone — поверхность вращения: внешняя стенка, внутренняя стенка (если rmin>0, нормали внутрь полости), торцевые кольца/диски; если deltaphi<360° — ещё две боковые плоскости. Сегментов по окружности: 48 при полном обороте, пропорционально при неполном (минимум 3). Для polycone рёбра профиля по соседним zplane: внешний профиль (rmax,z) и внутренний (rmin,z); участки с нулевой длиной пропускай; вырожденные треугольники (нулевая площадь) не добавляй.
5. Координаты в мм, оси Z вдоль оси вращения, ось tube по Z, центр tube в нуле (z от -z/2 до +z/2).

Ограничения: без Console вывода; без static-состояния; культура — CultureInfo.InvariantCulture при чтении чисел; числа отсутствующих атрибутов = 0 (кроме deltaphi: по умолчанию 360). Файл не должен превышать 400 строк.
