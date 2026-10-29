using System;
using System.Collections.Generic;

namespace Cad2Gdml.Materials
{
    // Справочник: id вещества <-> русское имя
    public static class MaterialNames
    {
        private static readonly List<string> _ids = new List<string>
        {
            "G4_AIR", "G4_Galactic", "G4_WATER", "G4_Al", "G4_Fe",
            "G4_STAINLESS-STEEL", "G4_Cu", "G4_Pb", "G4_Si", "G4_Sn",
            "G4_W", "G4_Ti", "G4_Ni", "G4_Au", "G4_Ag", "G4_Ge",
            "G4_C", "G4_POLYETHYLENE", "G4_PLEXIGLASS", "G4_TEFLON",
            "G4_POLYSTYRENE", "G4_KAPTON", "G4_MYLAR", "G4_CONCRETE",
            "NaI", "NaI_Tl", "CsI", "LaBr3"
        };

        private static readonly List<string> _ru = new List<string>
        {
            "Воздух", "Вакуум", "Вода", "Алюминий", "Железо",
            "Нержавеющая сталь", "Медь", "Свинец", "Кремний", "Олово",
            "Вольфрам", "Титан", "Никель", "Золото", "Серебро", "Германий",
            "Углерод (графит)", "Полиэтилен", "Оргстекло", "Фторопласт (тефлон)",
            "Полистирол", "Каптон", "Майлар", "Бетон",
            "Иодид натрия NaI", "Иодид натрия NaI(Tl)", "Иодид цезия CsI", "Бромид лантана LaBr3"
        };

        private static readonly Dictionary<string, string> _idToRu;
        private static readonly Dictionary<string, string> _ruToId;
        private static readonly HashSet<string> _knownIds;

        static MaterialNames()
        {
            _idToRu = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _ruToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _ids.Count; i++)
            {
                _idToRu[_ids[i]] = _ru[i];
                _ruToId[_ru[i]] = _ids[i];
                _knownIds.Add(_ids[i]);
            }
        }

        // Русское имя по id; неизвестный — как есть; null → ""
        public static string ToRu(string id)
        {
            if (id == null) return "";
            string ru;
            if (_idToRu.TryGetValue(id, out ru))
                return ru;
            return id;
        }

        // Id по тексту (русское имя или id); неизвестное — обрезаем и возвращаем
        public static string ToId(string text)
        {
            if (text == null) return "";
            text = text.Trim();
            string id;
            if (_ruToId.TryGetValue(text, out id))
                return id;
            if (_idToRu.TryGetValue(text, out id))
                return id;
            return text;
        }

        // Все русские имена таблицы + дополнительные id (вне таблицы) без дублей
        public static List<string> RuLabels(IEnumerable<string> extraIds)
        {
            var result = new List<string>(_ru.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _ru.Count; i++)
            {
                result.Add(_ru[i]);
                seen.Add(_ru[i]);
            }
            if (extraIds != null)
            {
                foreach (string eid in extraIds)
                {
                    if (eid == null) continue;
                    if (_knownIds.Contains(eid)) continue;
                    if (seen.Contains(eid)) continue;
                    result.Add(eid);
                    seen.Add(eid);
                }
            }
            return result;
        }
    }
}
