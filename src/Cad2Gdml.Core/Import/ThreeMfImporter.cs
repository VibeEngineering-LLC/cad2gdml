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
        // Безопасный разбор числа: запятая = ошибка, NaN/Inf = ошибка
        private static double SafeParse(string s, string ctx)
        {
            if (s == null)
            {
                throw new InvalidDataException("3MF: bad number (null) " + ctx);
            }
            double v;
            try
            {
                v = double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new InvalidDataException("3MF: bad number '" + s + "' " + ctx);
            }
            catch (OverflowException)
            {
                throw new InvalidDataException("3MF: bad number '" + s + "' " + ctx);
            }
            if (double.IsNaN(v) || double.IsInfinity(v))
            {
                throw new InvalidDataException("3MF: bad number '" + s + "' " + ctx);
            }
            return v;
        }

        // Загрузка 3MF файла
        public static IList<Part> Load(string path, double unitOverrideToMm)
        {
            List<Part> parts = new List<Part>();

            // Не-ZIP или отсутствующий файл -> InvalidDataException
            ZipArchive zip;
            try
            {
                zip = ZipFile.OpenRead(path);
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException("3MF: not a valid ZIP archive: " + path);
            }
            catch (FileNotFoundException)
            {
                throw new InvalidDataException("3MF: file not found: " + path);
            }
            catch (IOException ex)
            {
                throw new InvalidDataException("3MF: I/O error opening archive: " + path, ex);
            }

            using (zip)
            {
                string modelPath = FindModelPath(zip);
                ZipArchiveEntry entry = zip.GetEntry(modelPath);
                if (entry == null)
                {
                    throw new InvalidDataException("3MF: model part missing: " + modelPath);
                }

                using (Stream s = entry.Open())
                {
                    XDocument doc = XDocument.Load(s);

                    // Множитель единиц -> мм
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

                    Dictionary<string, XElement> objects = ParseObjects(doc);
                    List<XElement> buildItems = GetBuildItems(doc);

                    // Пустая модель — ошибка
                    if (buildItems.Count == 0)
                    {
                        throw new InvalidDataException("3MF: empty model (no build items)");
                    }

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

                        Mesh mesh = ResolveObject(objects, objectId, itemTf, 0, unitToMm);

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
            if (zip.GetEntry("3D/3dmodel.model") != null)
            {
                return "3D/3dmodel.model";
            }

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

            return "3D/3dmodel.model";
        }

        // Поиск прямого дочернего элемента
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

        // Единица -> мм
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

        // Словарь объектов из <resources>
        private static Dictionary<string, XElement> ParseObjects(XDocument doc)
        {
            Dictionary<string, XElement> objects = new Dictionary<string, XElement>();
            foreach (XElement el in doc.Descendants())
            {
                if (el.Name.LocalName != "object")
                {
                    continue;
                }
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

        // Список build items
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

        // Разбор строки трансформации (12 чисел, row-vector)
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

            double m00 = SafeParse(parts[0], "transform[0]");
            double m01 = SafeParse(parts[1], "transform[1]");
            double m02 = SafeParse(parts[2], "transform[2]");
            double m10 = SafeParse(parts[3], "transform[3]");
            double m11 = SafeParse(parts[4], "transform[4]");
            double m12 = SafeParse(parts[5], "transform[5]");
            double m20 = SafeParse(parts[6], "transform[6]");
            double m21 = SafeParse(parts[7], "transform[7]");
            double m22 = SafeParse(parts[8], "transform[8]");
            double m30 = SafeParse(parts[9], "transform[9]");
            double m31 = SafeParse(parts[10], "transform[10]");
            double m32 = SafeParse(parts[11], "transform[11]");

            // Row-vector -> column-vector: транспонируем 3x3
            return new Affine3(
                m00, m10, m20,
                m01, m11, m21,
                m02, m12, m22,
                new Vec3d(m30, m31, m32));
        }

        // Рекурсивное разрешение объекта в меш (с масштабом на мм)
        private static Mesh ResolveObject(Dictionary<string, XElement> objects, string objectId, Affine3 transform, int depth, double unitToMm)
        {
            if (depth > 128)
            {
                return new Mesh();
            }

            XElement objEl;
            if (!objects.TryGetValue(objectId, out objEl))
            {
                return new Mesh();
            }

            XElement meshEl = FindElement(objEl, "mesh");
            if (meshEl != null)
            {
                return BuildMeshFromElement(meshEl, transform, unitToMm);
            }

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
                    Affine3 effective = transform.Multiply(childTf);

                    Mesh childMesh = ResolveObject(objects, childId, effective, depth + 1, unitToMm);
                    result.Append(childMesh);
                }
                return result;
            }

            return new Mesh();
        }

        // Построение меша из <mesh>; итоговые координаты масштабируются на unitToMm
        private static Mesh BuildMeshFromElement(XElement meshEl, Affine3 transform, double unitToMm)
        {
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
                    double x = SafeParse(xStr, "vertex.x");
                    double y = SafeParse(yStr, "vertex.y");
                    double z = SafeParse(zStr, "vertex.z");
                    vertices.Add(new Vec3d(x, y, z));
                }
            }

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
                    int v1 = (int)SafeParse(s1, "triangle.v1");
                    int v2 = (int)SafeParse(s2, "triangle.v2");
                    int v3 = (int)SafeParse(s3, "triangle.v3");
                    indices.Add(v1);
                    indices.Add(v2);
                    indices.Add(v3);
                }
            }

            // Применяем трансформацию и масштабируем на мм
            Mesh result = new Mesh();
            for (int i = 0; i < vertices.Count; i++)
            {
                Vec3d p = transform.Apply(vertices[i]);
                // Перенос и координаты в модельных единицах -> умножаем на unitToMm
                result.Vertices.Add(new Vec3d(p.X * unitToMm, p.Y * unitToMm, p.Z * unitToMm));
            }
            result.Indices.AddRange(indices);

            return result;
        }
    }
}
