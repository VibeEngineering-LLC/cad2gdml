using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Cad2Gdml.Pipeline;

namespace Cad2Gdml.Tests
{
    // Проверки 2a: структура GDML, позиционирование, смещение, источник снизу
    public static class Checks2a
    {
        public static List<KeyValuePair<string, Action>> All(string tempDir)
        {
            var list = new List<KeyValuePair<string, Action>>();
            list.Add(new KeyValuePair<string, Action>("gdml_structure", () => RunStructure(tempDir)));
            list.Add(new KeyValuePair<string, Action>("gdml_placement", () => RunPlacement(tempDir)));
            list.Add(new KeyValuePair<string, Action>("offset_cylinder", () => RunOffset(tempDir)));
            list.Add(new KeyValuePair<string, Action>("source_below", () => RunBelow(tempDir)));
            return list;
        }

        // 1. Структура GDML: child-элементы, ref-ссылки, world
        private static void RunStructure(string tempDir)
        {
            string dir = Path.Combine(tempDir, "gdml_structure");
            CheckUtil.TwoPartScene(dir);
            var r = CheckUtil.Run(dir,
                new[] { Path.Combine(dir, "crystal.stl"), Path.Combine(dir, "water_sample.stl") },
                1.0, "+z", true, false);
            CheckUtil.Assert(r.ExitCode == 0, "Ошибка конвейера: " + r.MainErrorId);

            var doc = CheckUtil.Gdml(r);

            // Собираем имена
            var matNames = new HashSet<string>();
            var solidNames = new HashSet<string>();
            var volNames = new HashSet<string>();

            foreach (XElement e in doc.Descendants())
            {
                XAttribute na = e.Attribute("name");
                if (na == null) continue;
                string n = na.Value;
                string ln = e.Name.LocalName;
                if (ln == "material") matNames.Add(n);
                else if (ln == "tube" || ln == "box" || ln == "polycone" || ln == "trd" || ln == "trap" || ln == "prism" || ln == "cone" || ln == "sphere") solidNames.Add(n);
                else if (ln == "volume") volNames.Add(n);
            }

            // Каждый <volume>: дочерние materialref/solidref, без атрибутов
            foreach (XElement v in doc.Descendants())
            {
                if (v.Name.LocalName != "volume") continue;

                bool hasMat = false, hasSol = false;
                foreach (XElement c in v.Elements())
                {
                    if (c.Name.LocalName == "materialref") hasMat = true;
                    if (c.Name.LocalName == "solidref") hasSol = true;
                }
                CheckUtil.Assert(hasMat, "Volume не имеет <materialref>");
                CheckUtil.Assert(hasSol, "Volume не имеет <solidref>");
                CheckUtil.Assert(v.Attribute("materialref") == null, "Volume имеет атрибут materialref");
                CheckUtil.Assert(v.Attribute("solidref") == null, "Volume имеет атрибут solidref");

                foreach (XElement c in v.Elements())
                {
                    if (c.Name.LocalName == "materialref")
                    {
                        XAttribute ra = c.Attribute("ref");
                        CheckUtil.Assert(ra != null, "materialref без ref");
                        CheckUtil.Assert(matNames.Contains(ra.Value) || ra.Value.StartsWith("G4_"), "Материал не найден: " + ra.Value);
                    }
                    if (c.Name.LocalName == "solidref")
                    {
                        XAttribute ra = c.Attribute("ref");
                        CheckUtil.Assert(ra != null, "solidref без ref");
                        CheckUtil.Assert(solidNames.Contains(ra.Value), "Тело не найдено: " + ra.Value);
                    }
                }
            }

            // Каждый <physvol>: дочерний volumeref, без атрибута ref
            foreach (XElement pv in doc.Descendants())
            {
                if (pv.Name.LocalName != "physvol") continue;
                bool hasVr = false;
                foreach (XElement c in pv.Elements())
                {
                    if (c.Name.LocalName == "volumeref")
                    {
                        hasVr = true;
                        XAttribute ra = c.Attribute("ref");
                        CheckUtil.Assert(ra != null, "volumeref без ref");
                        CheckUtil.Assert(volNames.Contains(ra.Value), "Volume не найден: " + ra.Value);
                    }
                }
                CheckUtil.Assert(hasVr, "Physvol не имеет <volumeref>");
                CheckUtil.Assert(pv.Attribute("ref") == null, "Physvol имеет атрибут ref");
            }

            // <setup> → <world ref="...">
            bool worldOk = false;
            foreach (XElement s in doc.Descendants())
            {
                if (s.Name.LocalName != "setup") continue;
                foreach (XElement c in s.Elements())
                {
                    if (c.Name.LocalName == "world")
                    {
                        XAttribute ra = c.Attribute("ref");
                        CheckUtil.Assert(ra != null, "world без ref");
                        CheckUtil.Assert(volNames.Contains(ra.Value), "World не volume: " + ra.Value);
                        worldOk = true;
                    }
                }
            }
            CheckUtil.Assert(worldOk, "Не найден <world> в <setup>");
        }

        // 2. Позиционирование: координаты и размеры
        private static void RunPlacement(string tempDir)
        {
            string dir = Path.Combine(tempDir, "gdml_placement");
            CheckUtil.TwoPartScene(dir);
            var r = CheckUtil.Run(dir,
                new[] { Path.Combine(dir, "crystal.stl"), Path.Combine(dir, "water_sample.stl") },
                1.0, "+z", true, false);
            CheckUtil.Assert(r.ExitCode == 0, "Ошибка конвейера: " + r.MainErrorId);

            var doc = CheckUtil.Gdml(r);

            XElement crVol = CheckUtil.Vol(doc, "crystal");
            XElement waVol = CheckUtil.Vol(doc, "water");
            XElement crPhys = CheckUtil.Phys(doc, crVol);
            XElement waPhys = CheckUtil.Phys(doc, waVol);

            // Позиции (центр)
            CheckUtil.Near(-25.4, PosZ(crPhys), 0.01, "Crystal pos z");
            CheckUtil.Near(29.2, PosZ(waPhys), 0.01, "Water pos z");

            // Размеры тел
            XElement crSol = SolidOf(doc, crVol);
            XElement waSol = SolidOf(doc, waVol);

            CheckUtil.Near(25.4, CheckUtil.Num(crSol, "rmax"), 0.001, "Crystal rmax");
            CheckUtil.Near(30.0, CheckUtil.Num(waSol, "rmax"), 0.001, "Water rmax");
            CheckUtil.Near(50.8, CheckUtil.Num(crSol, "z"), 0.001, "Crystal solid z");
            CheckUtil.Near(40.0, CheckUtil.Num(waSol, "z"), 0.001, "Water solid z");
        }

        // 3. Смещённый цилиндр: ось найдена, позиции x,y = 0
        private static void RunOffset(string tempDir)
        {
            string dir = Path.Combine(tempDir, "offset_cylinder");
            Directory.CreateDirectory(dir);

            var crT = ShiftXY(Fixtures.Cylinder(25.4, 50.8, 64, 10.0), 100.0, 100.0);
            var waT = ShiftXY(Fixtures.Cylinder(30.0, 40.0, 64, 70.0), 100.0, 100.0);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), crT);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), waT);

            var r = CheckUtil.Run(dir,
                new[] { Path.Combine(dir, "crystal.stl"), Path.Combine(dir, "water_sample.stl") },
                1.0, "+z", true, false);
            CheckUtil.Assert(r.ExitCode == 0, "Ошибка конвейера: " + r.MainErrorId);

            var doc = CheckUtil.Gdml(r);
            XElement crVol = CheckUtil.Vol(doc, "crystal");
            XElement waVol = CheckUtil.Vol(doc, "water");
            XElement crPhys = CheckUtil.Phys(doc, crVol);
            XElement waPhys = CheckUtil.Phys(doc, waVol);

            // Rmax кристалла
            XElement crSol = SolidOf(doc, crVol);
            CheckUtil.Near(25.4, CheckUtil.Num(crSol, "rmax"), 0.01, "Offset crystal rmax");

            // X,Y позиций обоих physvol = 0
            CheckUtil.Near(0.0, PosX(crPhys), 0.01, "Crystal pos x");
            CheckUtil.Near(0.0, PosY(crPhys), 0.01, "Crystal pos y");
            CheckUtil.Near(0.0, PosX(waPhys), 0.01, "Water pos x");
            CheckUtil.Near(0.0, PosY(waPhys), 0.01, "Water pos y");
        }

        // 4. Источник снизу: позиция z > 0
        private static void RunBelow(string tempDir)
        {
            string dir = Path.Combine(tempDir, "source_below");
            Directory.CreateDirectory(dir);

            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"), Fixtures.Cylinder(25.4, 50.8, 64, 10.0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"), Fixtures.Cylinder(30.0, 40.0, 64, -60.0));

            var r = CheckUtil.Run(dir,
                new[] { Path.Combine(dir, "crystal.stl"), Path.Combine(dir, "water_sample.stl") },
                1.0, "auto", true, false);
            CheckUtil.Assert(r.ExitCode == 0, "Ошибка конвейера: " + r.MainErrorId);

            var doc = CheckUtil.Gdml(r);
            XElement waVol = CheckUtil.Vol(doc, "water");
            XElement waPhys = CheckUtil.Phys(doc, waVol);
            double z = PosZ(waPhys);
            CheckUtil.Assert(z > 0.0, "Water pos z должен быть > 0, получено: " + z.ToString(CultureInfo.InvariantCulture));
        }

        // --- Вспомогательные методы ---

        // Сдвиг всех вершин по x,y
        private static IList<double[]> ShiftXY(IList<double[]> tris, double dx, double dy)
        {
            var res = new List<double[]>(tris.Count);
            for (int i = 0; i < tris.Count; i++)
            {
                double[] t = tris[i];
                double[] s = new double[9];
                for (int j = 0; j < 9; j++) s[j] = t[j];
                s[0] += dx; s[3] += dx; s[6] += dx;
                s[1] += dy; s[4] += dy; s[7] += dy;
                res.Add(s);
            }
            return res;
        }

        // З-компонента позиции из physvol
        private static double PosZ(XElement phys)
        {
            foreach (XElement c in phys.Elements())
            {
                if (c.Name.LocalName == "position") return CheckUtil.Num(c, "z");
            }
            return 0.0;
        }

        // X-компонента позиции из physvol
        private static double PosX(XElement phys)
        {
            foreach (XElement c in phys.Elements())
            {
                if (c.Name.LocalName == "position") return CheckUtil.Num(c, "x");
            }
            return 0.0;
        }

        // Y-компонента позиции из physvol
        private static double PosY(XElement phys)
        {
            foreach (XElement c in phys.Elements())
            {
                if (c.Name.LocalName == "position") return CheckUtil.Num(c, "y");
            }
            return 0.0;
        }

        // Найти solid по solidref в volume
        private static XElement SolidOf(XDocument doc, XElement vol)
        {
            string name = null;
            foreach (XElement c in vol.Elements())
            {
                if (c.Name.LocalName == "solidref")
                {
                    XAttribute ra = c.Attribute("ref");
                    if (ra != null) name = ra.Value;
                }
            }
            CheckUtil.Assert(name != null, "Нет solidref ref");

            foreach (XElement e in doc.Descendants())
            {
                XAttribute na = e.Attribute("name");
                if (na != null && na.Value == name)
                {
                    string ln = e.Name.LocalName;
                    if (ln == "tube" || ln == "box" || ln == "polycone" || ln == "trd"
                        || ln == "trap" || ln == "prism" || ln == "cone" || ln == "sphere")
                        return e;
                }
            }
            throw new InvalidOperationException("Тело не найдено: " + name);
        }
    }
}
