Напиши один C#-файл (C# 7.3, .NET Framework 4.8, без внешних пакетов). Верни ТОЛЬКО код файла, без пояснений и без markdown-ограждений. Думай коротко и сразу пиши код. Не больше 190 строк.

using System; System.Collections.Generic; System.Globalization; System.IO; System.Xml.Linq; Cad2Gdml.Geometry; Cad2Gdml.Meshes.
namespace Cad2Gdml.Import
В файле три типа:

A. `internal sealed class GdmlSkipBodyException : InvalidDataException` — конструктор `(string message)`. Означает: тело нельзя построить из-за плохой позиции или единицы; вызывающий пропустит это тело, остальной файл читается.

B. `internal sealed class TessPositions` — позиции `<define>` файла, РАЗБИРАЮТСЯ ЛЕНИВО.
 - `internal static TessPositions Read(XElement root)`: один проход по всем элементам `position` (по `Name.LocalName`), лежащим внутри элементов `define` на любой глубине под root. Позиции без атрибута `name` пропусти. Ничего не разбирай — только запомни сам XElement по имени. Если имя встретилось второй раз — добавь имя в множество дублей (первый элемент оставь). Словарь создай с ёмкостью по числу позиций (счёт за один проход в список, потом заполнение).
 - `internal double[] Get(string name)`: если имя в множестве дублей → `throw new GdmlSkipBodyException("позиция «NAME» определена в <define> более одного раза")`; если имени нет → вернуть `null`; иначе при первом обращении разобрать и кэшировать, при повторном вернуть кэш. Разбор: массив {x,y,z} в МИЛЛИМЕТРАХ; каждая координата — атрибут x, y, z (нет атрибута → 0), число `double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)`, умноженное на коэффициент единицы ЭТОЙ позиции (атрибут `unit`: нет/пусто/"mm" → 1; "cm" → 10; "m" → 1000; "um" → 0.001; "nm" → 1e-6; "km" → 1e6). Ошибка числа (в том числе выражение вроде `2*cm`, ссылка на константу, NaN, бесконечность) → `GdmlSkipBodyException("позиция «NAME»: значение «TEXT» не число (выражения и константы GDML не поддержаны)")`; неизвестная единица → `GdmlSkipBodyException("позиция «NAME»: неизвестная единица длины «UNIT»")`.

C. `internal static class GdmlTessellated` с одним методом `internal static Mesh Build(XElement solid, TessPositions positions)` — строит сетку тела `<tessellated name=...>` по правилам Geant4 (G4GDMLReadSolids::TriangularRead/QuadrangularRead, G4TriangularFacet, G4QuadrangularFacet):
 - Атрибуты `lunit` и `aunit` САМОГО тела НЕ читаются и НЕ применяются (в Geant4 TessellatedRead читает у тела только name).
 - Дочерние элементы по `LocalName`: `triangular` (vertex1, vertex2, vertex3) и `quadrangular` (vertex1..vertex4). Прочие дочерние элементы игнорируй.
 - Коэффициент `lunit` берётся С КАЖДОЙ ГРАНИ (атрибут lunit этого `triangular`/`quadrangular`): нет/пусто/"mm" → 1; "cm" → 10; "m" → 1000; "um" → 0.001; "nm" → 1e-6; "km" → 1e6; иное → `GdmlSkipBodyException("тело tessellated «SOLID»: неизвестная единица грани «UNIT»")`. Он умножает КАЖДУЮ вершину этой грани (поверх единицы позиции).
 - Атрибут vertexN — ИМЯ позиции. Координаты вершины = `positions.Get(имя)` × lunit грани (мм). Атрибута нет → `InvalidDataException("Тело tessellated «SOLID»: не задан атрибут vertexN.")`; `Get` вернул null → `InvalidDataException("Тело tessellated «SOLID»: вершина «VERTEX» не найдена среди позиций <define>.")`. Исключения из `Get` (GdmlSkipBodyException) не перехватывай.
 - Атрибут `type` ГРАНИ: значение "RELATIVE" (регистр как есть) → вершины 2, 3 (и 4) — смещения от вершины 1: итоговая vertex_k = vertex1 + vertex_k (k = 2,3,4), складываются уже масштабированные значения. Иное или нет атрибута → ABSOLUTE.
 - Треугольник (v1,v2,v3) — `mesh.AddTriangle(v1, v2, v3)`. Четырёхугольник (v1,v2,v3,v4) делится как в G4QuadrangularFacet на (v1,v2,v3) и (v1,v3,v4).
 - `Vec3d` — конструктор `new Vec3d(x, y, z)`; `Mesh` — `new Mesh()`, `AddTriangle(Vec3d a, Vec3d b, Vec3d c)`.
Комментарии по-русски. Без Console, без статического состояния, без LINQ в горячем цикле.
