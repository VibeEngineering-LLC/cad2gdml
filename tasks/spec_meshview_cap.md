Напиши ФРАГМЕНТ C# (C# 7.3) — блок кода, вставляемый внутрь метода (внутрь тела `try` метода рисования), без объявления методов и классов. Верни ТОЛЬКО код блока, без пояснений и без markdown-ограждений. Думай коротко, сразу пиши код. Не больше 60 строк.

Задача: нарисовать «крышки» среза. В режиме сечения сетки деталей обрезаны плоскостью, поэтому внутри разреза видны обратные стенки и срез выглядит прозрачным. Крышку определяем по чётности: для детали пиксель принадлежит сечению, если число перекрывающих его обрезанных треугольников этой детали нечётно. Такие пиксели закрашиваются плоским цветом детали на глубине плоскости сечения.

Доступно в месте вставки (имена и типы фиксированы, не объявляй заново):
- `bool _sectionEnabled; int _sectionAxis;` (0 = X, 1 = Y, 2 = Z мира); `double planePos` — координата плоскости вдоль этой оси в мировой системе; `Vec3d _centre` (поля X, Y, Z типа double); `double _radius`.
- `double yawR, pitchR, scale, cx, cyy;` `int w, h;` (размеры буфера).
- Метод экземпляра `void RotatePoint(double x, double y, double z, double yawR, double pitchR, out double rx, out double ry, out double rz)` — поворот точки в систему вида (ry — глубина, меньше = ближе).
- `List<Part> _parts;` у `Part`: `string Name`, `bool MeshOpen`. `HashSet<string> Hidden`; `string _highlight` (может быть null).
- Для треугольников: `int total` (их число), `float[] triPts` (по 6 чисел на треугольник: x0,y0,x1,y1,x2,y2 в пикселях), `int[] triPart` (индекс детали в `_parts`).
- `float[] partBias` (по одному на деталь), `bool hasHi`.
- `ZBuffer zb` с методами: `void XorTriangle(float x0,float y0,float x1,float y1,float x2,float y2, byte[] mask)` и `void StampMask(byte[] mask, float a, float b, float c, float bias, int argb)` (глубина пикселя = a*(px+0.5)+b*(py+0.5)+c − bias).
- `Color GetPartColor(Part part)`; статический `int Clamp255(int v)`.

Что сделать (только если `_sectionEnabled`):
1. Единичный вектор оси сечения (ux,uy,uz) по `_sectionAxis`; `RotatePoint(ux,uy,uz,yawR,pitchR,out nvx,out nvy,out nvz)`; `D = planePos - (компонента _centre по этой оси)`. Если `Math.Abs(nvy) < 1e-3` — ничего не рисовать (плоскость ребром к зрителю).
2. Коэффициенты глубины плоскости: `a = -nvx/(nvy*scale)`, `b = nvz/(nvy*scale)`, `c = D/nvy + nvx*cx/(nvy*scale) - nvz*cyy/(nvy*scale)` (все в float).
3. Один раз создать `byte[] capMask = new byte[w * h]`.
4. Для каждой детали pi: пропустить, если `Hidden.Contains(Name)` или `MeshOpen`. Обнулить маску (`Array.Clear`). Для всех треугольников i с `triPart[i] == pi` вызвать `zb.XorTriangle(...)` по `triPts`. Цвет: если деталь подсвечена (`_highlight != null && Name == _highlight`) — оранжевый `Color.FromArgb(255, 217, 119, 0)`, иначе `GetPartColor(part)`, умноженный на 0.85 по каналам через `Clamp255`; при `hasHi` и не подсвеченной — смешать с белым на 35 % (`v + (255 - v) * 0.35`). Затем `zb.StampMask(capMask, a, b, c, partBias[pi] + (подсвечена ? (float)(_radius * 1e-3) : 0f), цвет.ToArgb())`.
Имена локальных переменных начинай с префикса `cap`, чтобы не совпасть с существующими (`nx`, `ny`, `nz`, `i`, `pi`, `g`, `e`, `a`, `b`, `c` уже могут быть заняты — для a, b, c используй capA, capB, capC). Нужные `using` (System, System.Drawing, System.Collections.Generic) уже есть. Комментарии по-русски.
