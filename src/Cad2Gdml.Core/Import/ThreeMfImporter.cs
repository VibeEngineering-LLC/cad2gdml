using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Cad2Gdml.Geometry;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Import
{
    // Импортёр 3MF
    public static class ThreeMfImporter
    {
        // Загрузка 3MF файла
        public static IList<Part> Load(string path, double unitOverrideToMm)
        {
            List<Part> parts = new List<Part>();

            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
                // Определяем путь модели внутри ZIP
                string modelPath = FindModelPath(zip);
                ZipArchiveEntry entry = zip.GetEntry(modelPath);
                if (entry == null)
                {
                    throw new FileNotFoundException("Модель не найдена в 3MF: " + modelPath, path);
                }

                using (Stream s = entry.Open())
                {
                    XDocument doc = XDocument.Load(s);

                    // Определяем множитель единиц на мм
                    double unitToMm;
                    if (unitOverrideToMm > 0.0)
                    {
                        unitToMm = unitOverrideToMm;
                    }
                    else
                    {
                        XElement modelEl = FindElement(doc.Root, "model");
                        if (modelEl == null)
                        {
                            modelEl = doc.Root;
                        }
                        string unitStr = GetAttr(modelEl, "unit");
                        unitToMm = ParseUnit(unitStr);
                    }

                    // Собираем словарь объектов
                    Dictionary<string, XElement> objects = ParseObjects(doc);

                    // Собираем build items
                    List<XElement> buildItems = GetBuildItems(doc);

                    // Счётчики повторов имён
                    Dictionary<string, int> nameCounters = new Dictionary<string, int>();

                    for (int i = 0; i < buildItems.Count; i++)
                    {
                        XElement item = buildItems[i];
                        string objectId = GetAttr(item, "objectid");
                        if (string.IsNullOrEmpty(objectId))
                        {
                            continue;
                        }

                        string transformStr = GetAttr(item, "transform");
                        Affine3 itemTf = ParseTransform(transformStr);

                        XElement objEl;
                        if (!objects.TryGetValue(objectId, out objEl))
                        {
                            continue;
                        }

                        // Формируем имя детали
                        string baseName = GetAttr(objEl, "name");
                        if (string.IsNullOrEmpty(baseName))
                        {
                            baseName = "obj" + objectId;
                        }

                        int cnt;
                        string finalName;
                        if (nameCounters.TryGetValue(baseName, out cnt))
                        {
                            finalName = baseName + "_" + cnt.ToString(CultureInfo.InvariantCulture);
                            nameCounters[baseName] = cnt + 1;
                        }
                        else
                        {
                            finalName = baseName;
                            nameCounters[baseName] = 1;
                        }

                        // Рекурсивно разрешаем объект в меш
                        Mesh mesh = ResolveObject(objects, objectId, itemTf, 0);

                        Part part = new Part();
                        part.Name = finalName;
                        part.OrigName = finalName;
                        part.Mesh = mesh;
                        part.SourceFile = path;

                        parts.Add(part);
                    }
                }
            }

            return parts;
        }

        // Поиск пути модели в ZIP-архиве
        private static string FindModelPath(ZipArchive zip)
        {
            // Стандартный путь
            if (zip.GetEntry("3D/3dmodel.model") != null)
            {
                return "3D/3dmodel.model";
            }

            // Читаем _rels/.rels
            ZipArchiveEntry relsEntry = zip.GetEntry("_rels/.rels");
            if (relsEntry != null)
            {
                using (Stream s = relsEntry.Open())
                {
                    XDocument doc = XDocument.Load(s);
                    foreach (XElement el in doc.Descendants())
                    {
                        if (el.Name.LocalName != "Relationship")
                        {
                            continue;
                        }
                        string type = GetAttr(el, "Type");
                        if (type != null && type.IndexOf("3dmodel", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string target = GetAttr(el, "Target");
                            if (!string.IsNullOrEmpty(target))
                            {
                                if (target.StartsWith("/"))
                                {
                                    target = target.Substring(1);
                                }
                                return target;
                            }
                        }
                    }
                }
            }

            // Возврат к стандартному пути
            return "3D/3dmodel.model";
        }

        // Поиск прямого дочернего элемента по локальному имени
        private static XElement FindElement(XElement parent, string localName)
        {
            if (parent == null)
            {
                return null;
            }
            foreach (XElement child in parent.Elements())
            {
                if (child.Name.LocalName == localName)
                {
                    return child;
                }
            }
            return null;
        }

        // Чтение атрибута; null если нет
        private static string GetAttr(XElement el, string name)
        {
            if (el == null)
            {
                return null;
            }
            XAttribute attr = el.Attribute(name);
            if (attr == null)
            {
                return null;
            }
            return attr.Value;
        }

        // Разбор единицы измерения -> мм
        private static double ParseUnit(string unit)
        {
            if (string.IsNullOrEmpty(unit))
            {
                return 1.0;
            }
            string u = unit.ToLowerInvariant();
            if (u == "micron")
            {
                return 0.001;
            }
            if (u == "millimeter")
            {
                return 1.0;
            }
            if (u == "centimeter")
            {
                return 10.0;
            }
            if (u == "inch")
            {
                return 25.4;
            }
            if (u == "foot")
            {
                return 304.8;
            }
            if (u == "meter")
            {
                return 1000.0;
            }
            return 1.0;
        }

        // Сборка словаря объектов из секции ресурсов
        private static Dictionary<string, XElement> ParseObjects(XDocument doc)
        {
            Dictionary<string, XElement> objects = new Dictionary<string, XElement>();
            foreach (XElement el in doc.Descendants())
            {
                if (el.Name.LocalName != "object")
                {
                    continue;
                }
                // Убеждаемся, что родитель — <resources>
                if (el.Parent == null || el.Parent.Name.LocalName != "resources")
                {
                    continue;
                }
                string id = GetAttr(el, "id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                objects[id] = el;
            }
            return objects;
        }

        // Сборка списка build items
        private static List<XElement> GetBuildItems(XDocument doc)
        {
            List<XElement> items = new List<XElement>();
            foreach (XElement el in doc.Descendants())
            {
                if (el.Name.LocalName != "item")
                {
                    continue;
                }
                if (el.Parent == null || el.Parent.Name.LocalName != "build")
                {
                    continue;
                }
                items.Add(el);
            }
            return items;
        }

        // Разбор строки трансформации (row-vector, 12 чисел)
        private static Affine3 ParseTransform(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return Affine3.Identity;
            }

            string[] parts = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 12)
            {
                return Affine3.Identity;
            }

            double m00 = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double m01 = double.Parse(parts[1], CultureInfo.InvariantCulture);
            double m02 = double.Parse(parts[2], CultureInfo.InvariantCulture);
            double m10 = double.Parse(parts[3], CultureInfo.InvariantCulture);
            double m11 = double.Parse(parts[4], CultureInfo.InvariantCulture);
            double m12 = double.Parse(parts[5], CultureInfo.InvariantCulture);
            double m20 = double.Parse(parts[6], CultureInfo.InvariantCulture);
            double m21 = double.Parse(parts[7], CultureInfo.InvariantCulture);
            double m22 = double.Parse(parts[8], CultureInfo.InvariantCulture);
            double m30 = double.Parse(parts[9], CultureInfo.InvariantCulture);
            double m31 = double.Parse(parts[10], CultureInfo.InvariantCulture);
            double m32 = double.Parse(parts[11], CultureInfo.InvariantCulture);

            // Row-vector -> column-vector: транспонируем 3x3
            return new Affine3(
                m00, m10, m20,
                m01, m11, m21,
                m02, m12, m22,
                new Vec3d(m30, m31, m32));
        }

        // Рекурсивное разрешение объекта в меш
        private static Mesh ResolveObject(Dictionary<string, XElement> objects, string objectId, Affine3 transform, int depth)
        {
            // Защита от бесконечной рекурсии
            if (depth > 128)
            {
                return new Mesh();
            }

            XElement objEl;
            if (!objects.TryGetValue(objectId, out objEl))
            {
                return new Mesh();
            }

            // Если есть <mesh> — строим меш
            XElement meshEl = FindElement(objEl, "mesh");
            if (meshEl != null)
            {
                return BuildMeshFromElement(meshEl, transform);
            }

            // Если есть <components> — рекурсивно
            XElement componentsEl = FindElement(objEl, "components");
            if (componentsEl != null)
            {
                Mesh result = new Mesh();
                foreach (XElement comp in componentsEl.Elements())
                {
                    if (comp.Name.LocalName != "component")
                    {
                        continue;
                    }
                    string childId = GetAttr(comp, "objectid");
                    if (string.IsNullOrEmpty(childId))
                    {
                        continue;
                    }
                    string childTfStr = GetAttr(comp, "transform");
                    Affine3 childTf = ParseTransform(childTfStr);
                    // Применяем childTf, затем текущий transform
                    Affine3 effective = transform.Multiply(childTf);

                    Mesh childMesh = ResolveObject(objects, childId, effective, depth + 1);
                    result.Append(childMesh);
                }
                return result;
            }

            return new Mesh();
        }

        // Построение меша из элемента <mesh> с применением трансформации
        private static Mesh BuildMeshFromElement(XElement meshEl, Affine3 transform)
        {
            // Вершины
            List<Vec3d> vertices = new List<Vec3d>();
            XElement verticesEl = FindElement(meshEl, "vertices");
            if (verticesEl != null)
            {
                foreach (XElement vEl in verticesEl.Elements())
                {
                    if (vEl.Name.LocalName != "vertex")
                    {
                        continue;
                    }
                    string xStr = GetAttr(vEl, "x");
                    string yStr = GetAttr(vEl, "y");
                    string zStr = GetAttr(vEl, "z");
                    if (xStr == null || yStr == null || zStr == null)
                    {
                        continue;
                    }
                    double x = double.Parse(xStr, CultureInfo.InvariantCulture);
                    double y = double.Parse(yStr, CultureInfo.InvariantCulture);
                    double z = double.Parse(zStr, CultureInfo.InvariantCulture);
                    vertices.Add(new Vec3d(x, y, z));
                }
            }

            // Индексы треугольников
            List<int> indices = new List<int>();
            XElement trianglesEl = FindElement(meshEl, "triangles");
            if (trianglesEl != null)
            {
                foreach (XElement tEl in trianglesEl.Elements())
                {
                    if (tEl.Name.LocalName != "triangle")
                    {
                        continue;
                    }
                    string s1 = GetAttr(tEl, "v1");
                    string s2 = GetAttr(tEl, "v2");
                    string s3 = GetAttr(tEl, "v3");
                    if (s1 == null || s2 == null || s3 == null)
                    {
                        continue;
                    }
                    int v1 = int.Parse(s1, CultureInfo.InvariantCulture);
                    int v2 = int.Parse(s2, CultureInfo.InvariantCulture);
                    int v3 = int.Parse(s3, CultureInfo.InvariantCulture);
                    indices.Add(v1);
                    indices.Add(v2);
                    indices.Add(v3);
                }
            }

            // Применяем трансформацию к вершинам
            Mesh result = new Mesh();
            for (int i = 0; i < vertices.Count; i++)
            {
                result.Vertices.Add(transform.Apply(vertices[i]));
            }
            result.Indices.AddRange(indices);

            return result;
        }
    }
}
