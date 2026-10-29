using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Cad2Gdml.Pipeline;

namespace Cad2Gdml.Tests
{
    // Вспомогательные проверки для тестов
    public static class CheckUtil
    {
        // Запуск конвейера с заданными параметрами
        public static PipelineResult Run(string dir, string[] inputs, double unit, string front, bool allowDefault, bool dropOpen)
        {
            Directory.CreateDirectory(dir);
            var opt = new PipelineOptions();
            opt.Inputs = new List<string>();
            foreach (string inp in inputs)
                opt.Inputs.Add(Path.IsPathRooted(inp) ? inp : Path.Combine(dir, inp));
            opt.UnitToMm = unit;
            opt.FrontSign = front;
            opt.AllowDefaultMaterial = allowDefault;
            opt.DropOpen = dropOpen;
            opt.OutDir = dir;
            opt.BaseName = "o";
            return Cad2GdmlPipeline.Run(opt);
        }

        // Загрузка GDML как XDocument
        public static XDocument Gdml(PipelineResult r)
        {
            return XDocument.Load(r.GdmlPath);
        }

        // Поиск <volume> по подстроке имени
        public static XElement Vol(XDocument d, string nameContains)
        {
            foreach (XElement v in d.Descendants())
            {
                if (v.Name.LocalName != "volume") continue;
                XAttribute a = v.Attribute("name");
                if (a != null && a.Value != null && a.Value.Contains(nameContains))
                    return v;
            }
            throw new InvalidOperationException("Volume не найден: " + nameContains);
        }

        // Поиск <physvol> с <volumeref ref> = имя объёма
        public static XElement Phys(XDocument d, XElement volume)
        {
            XAttribute na = volume.Attribute("name");
            if (na == null) throw new InvalidOperationException("У volume нет атрибута name.");
            string volName = na.Value;

            foreach (XElement pv in d.Descendants())
            {
                if (pv.Name.LocalName != "physvol") continue;
                XElement vr = Child(pv, "volumeref");
                if (vr == null) continue;
                XAttribute refA = vr.Attribute("ref");
                if (refA != null && refA.Value == volName)
                    return pv;
            }
            throw new InvalidOperationException("Physvol не найден для: " + volName);
        }

        // Чтение числового атрибута
        public static double Num(XElement e, string attr)
        {
            XAttribute a = e.Attribute(attr);
            if (a == null) throw new InvalidOperationException("Атрибут не найден: " + attr);
            return double.Parse(a.Value, CultureInfo.InvariantCulture);
        }

        // Утверждение: бросает при false
        public static void Assert(bool ok, string msg)
        {
            if (!ok) throw new InvalidOperationException(msg);
        }

        // Проверка близости чисел
        public static void Near(double expected, double actual, double tol, string what)
        {
            if (Math.Abs(expected - actual) > tol)
            {
                throw new InvalidOperationException(what
                    + ": ожидаемо " + expected.ToString(CultureInfo.InvariantCulture)
                    + ", получено " + actual.ToString(CultureInfo.InvariantCulture));
            }
        }

        // Двухдетная сцена: кристалл + вода
        public static string TwoPartScene(string dir)
        {
            Directory.CreateDirectory(dir);
            Fixtures.WriteBinaryStl(Path.Combine(dir, "crystal.stl"),
                Fixtures.Cylinder(25.4, 50.8, 64, 10.0));
            Fixtures.WriteBinaryStl(Path.Combine(dir, "water_sample.stl"),
                Fixtures.Cylinder(30.0, 40.0, 64, 70.0));
            return dir;
        }

        // Поиск дочернего элемента по локальному имени (без учёта namespace)
        private static XElement Child(XElement e, string localName)
        {
            foreach (XElement c in e.Elements())
            {
                if (c.Name.LocalName == localName) return c;
            }
            return null;
        }
    }
}
