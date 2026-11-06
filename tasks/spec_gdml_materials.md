Напиши один C#-файл (C# 7.3, .NET Framework 4.8, без внешних пакетов). Верни ТОЛЬКО код файла, без пояснений и без markdown-ограждений. Думай коротко и сразу пиши код. Не больше 120 строк.

using System; System.Collections.Generic; System.Globalization; System.IO; System.Xml.Linq; Cad2Gdml.Materials.
namespace Cad2Gdml.Import
`public static class GdmlMaterials` с одним методом:
 `public static List<MaterialDef> Load(string path, out List<string> notes)`
Известный тип (не определяй): `Cad2Gdml.Materials.MaterialDef` — публичные поля `string Name; double DensityGcm3; List<KeyValuePair<string,double>> MassFractions; bool IsNist;` (конструктор без параметров; MassFractions надо создать самому).

Что делает: читает GDML (XDocument.Load, элементы сравнивать по `Name.LocalName`) и возвращает определения веществ из `<materials>`, КРОМЕ тех, имя которых начинается с "G4_" (это справочные NIST-вещества Geant4, они известны программе).
Для каждого `<material name=...>`:
 - плотность: дочерний `<D unit="g/cm3" value="..."/>`; unit g/cm3 → как есть, mg/cm3 → /1000, kg/m3 → /1000; иное или нет D → в `notes` добавить строку по-русски «Вещество X пропущено: нет плотности» и пропустить;
 - состав: дочерние `<fraction n="..." ref="..."/>`; ref указывает на `<element name=...>` из того же файла: берётся символ элемента — значение атрибута `formula`, если оно есть, иначе имя элемента (например "Cu"). Если ref указывает на другое `<material>` (смесь из смесей) — вещество пропустить с пояснением в notes. Дробные доли `n` складывать по символу (если один символ встретился дважды). Если сумма долей отличается от 1 больше чем на 1e-3 — нормировать на 1 и добавить строку в notes; если сумма равна 0 или состава нет — пропустить с пояснением;
 - `<composite n="..." ref="..."/>` (число атомов) → вещество пропустить с пояснением «состав по числу атомов не поддержан»;
 - `IsNist = false`; Name = имя вещества как в файле; числа читать `double.Parse(s, CultureInfo.InvariantCulture)`.
Невалидный XML → `InvalidDataException` с русским сообщением. Без Console, без статического состояния, комментарии по-русски. `notes` всегда создавать (пустой список, если замечаний нет).
