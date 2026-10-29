# #BM-23 Ф0b — проект приложения `cad2gdml` (CAD → GDML, C#/.NET, MIT)

Дата: 2026-10-29. Статус: **проект на согласование оператору**. Код не пишется, пока оператор не скажет «да» на этот документ.
Заменяет набросок на Python из `DESIGN-2-gdml2in.md` §9 (`:410-430`). Выход `cad2gdml` должен сразу читаться утилитой `gdml2in`
(DESIGN-2 §3–§5) и Geant4 11.2 (контур GEANT4).

Обозначения. **D2** — `DESIGN-2-gdml2in.md`; **BM** — `«локальный путь»`; **EM** — `BM\EfficiencyMaker\`;
**GS** — `EM\GeometrySketch.cs`; **BLD** — `«локальный путь» папка ИИ\GEANT4\scripts\build_gs2020_gdml.py`; **WATER** —
`…\GEANT4\results\water_eff\gdml\water_r1.0_v1000.gdml`; **V3** — `C:\g4work\gs2020\GS2020_device_v3.gdml`; **STEP-GS** —
`«локальный путь» GS2020 PRO MAX\Референсы\ATOM-2020-SIPM2 v2 (2).step`; **PV3** —
`«локальный путь» GS2020 PRO MAX\model\geant4\parts_stl_v3\` (41 STL + `_manifest.json`, вход BLD для V3).

---

## 0. Цель, ограничения, опорные факты

**Цель.** Отдельная программа (библиотека-ядро + командная строка + окно WinForms), которая из CAD-сборки (STEP из Fusion 360 /
Компаса или сеток STL/3MF/OBJ) — одним файлом «детектор + источник» или двумя файлами с взаимным размещением — строит GDML для
Geant4 11.2 с пометкой ролей объёмов, рисует осевой разрез сцены в стиле вкладки «Эффективность» BecqMoni и выдаёт текстовое
и JSON-описание сцены (объёмы, вещества, габариты, массы). Ядро спроектировано так, чтобы позже его вызывал BecqMoni
(.NET Framework 4.8, WinForms).

**Ограничения (обязательные).**
- Решения оператора 29.10 (бриф): сначала CAD → GDML; отдельная программа; C#/.NET; окно + командная строка; STEP + STL/3MF/OBJ;
  Blender/glTF/FBX — не в v1; лицензия MIT, без кода BecqMoni (В3, D2 `:461-463`); чужие библиотеки — только после #FC-1 (§35).
- `AGENTS.md` (`«локальный путь» папка ИИ\AGENTS.md`) §2: local-only — без push/тегов/релизов без явного «да»; у каждого
  утверждения о файле — `file:line`.
- Глобальные правила: §12 — зона GEANT4 и Конструктора только читаем, просьбы — письмом; §20 — хранение D: + git + GitHub, push по
  «да»; #LOC-5/#LOC-7 — код от 25 строк через `gen_code.py` с моделью `qwen3.8:27b` пятым аргументом; #SA-3/#SA-3a — мутационная
  приёмка на боевых образцах; #PS-1 — `.ps1` только ASCII.
- Граница GPL/MIT (§12.3 ниже): ни одной строки исходников BecqMoni в проект; BecqMoni — только образец поведения и формат данных.

**Опорные факты (проверено при написании, команды — в таблице).**

| факт | команда / место | результат |
|---|---|---|
| BM — .NET Framework 4.8, старый формат проекта | `BecquerelMonitor.csproj:2,12` | `ToolsVersion="12.0"`, `<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>` |
| BM — AnyCPU | `BecquerelMonitor.csproj:45,57` | `<PlatformTarget>AnyCPU</PlatformTarget>` |
| BM — язык C# 7.3 | `git show origin/master:tools/effmaker/probes/build_all.ps1` стр. 859; `LangVersion` в csproj — 0 совпадений | `csc … /langversion:7.3`; старый csproj на net48 по умолчанию тоже 7.3 (по документации MSBuild — не перепроверено) |
| В BM нет JSON-библиотеки | `BecquerelMonitor.csproj:1391-1414` | пакеты: DockPanelSuite(+Theme), InTheHand×2, MathNet.Numerics, Microsoft.Data.Sqlite, SQLitePCLRaw×2, System.IO.Ports |
| Лицензия BM | `git show origin/master:LICENSE` строки 1-2 | GPL v2 |
| Вещество BM | `EM\GeometryModel.cs:43-51` | `GeometryMaterial { string Name; double Density /*г/см3*/; Dictionary<int,double> Fractions /*Z→массовая доля*/ }` |
| Скетч BM | GS:26 (`class GeometrySketch : Control`), :46-61 палитра, :214-217 `OnPaint` + `AntiAlias`, :301-303 поля Overview 10/8/8, :319-323 единый масштаб `min(sx,sy)`, :479-487 `Dims` (`"G4"`, `InvariantCulture`, `" × "`, единица), :494-546 строки подписи, :555-575 ужатие шрифта и подложка | см. §8 |
| Строки подписи BM (ru) | `BM\Properties\Resources.ru.resx` :1257-1258 «мм», :1506-1507 «Детектор», :1512-1513 «Стакан Маринелли», :1494-1495 «Цилиндр», :1497-1498 «Кювета», :1509-1510 «До детектора» | — |
| Список подписей последней отрисовки доступен снаружи | GS:134-137 `public SketchLabel[] Labels` | основа оракула подписи (§11 г) |
| Проба-снимок скетча из `.in` | `git show origin/master:tools/effmaker/probes/SketchShot.cs` (65 строк): :27 `GeometryModel.Load`, :31-33 режимы, :50-55 `OnPaint` отражением | образец оракула (GPL, только запуск) |
| Образец GDML-писателя | BLD :111-146 разбор binary STL; :153-170 ключ вершины — округление до 1e-5 мм; :176-203 отброс вырожденных (площадь < 1e-8 мм²); :211-218 знаковый объём, переворот всех граней при V<0; :219-222 < 4 граней — деталь отброшена; :225-230 `<position … unit="mm"/>` 6 знаков; :233 `<tessellated>`; :236 имена LV ASCII; :265-266 World `box`; :269-271 режим DEVICE; :282-297 плоская структура; :300 `setup`; :303-304 шапка со схемой | см. §7 |
| Формат `materials.json` | BLD :36-40 ключи `custom`, `profile_parts`, `stl_rules`, `stl_default`, `world_material`; :43-47 первое совпадение `re.search` | боевой экземпляр: `…\Atom GS2020 PRO MAX\model\geant4\materials.json` (10 правил `stl_rules`, 5 своих веществ) |
| Пример выхода | WATER: 11 826 105 байт; `grep -c`: `<tessellated` 41, `<triangular` 90 266, `<position` 45 084, `auxiliary` 0; `:45098` `genericPolycone`; `:135506-135512` три `polycone` сосуда; `:135556-135563` проба и пустота — дочерние `LV_VesselWall`; `:135565` `setup` | эталон структуры для gdml2in |
| V3 | 11 887 938 байт; `<tessellated` 41, `<triangular` 89 818, `<volume ` 50, `genericPolycone name` 7, `auxiliary` 0; имена `LV_Shell_1_00`, `LV_Foam_pad_1_01`, `LV_Open_CASCADE_STEP_translator_7_5_*`, `Crystal_log`, `RC103_device_log` | оракул (а3) |
| PV3 | `grep -c '"file"' _manifest.json` → 41; первая запись `"name": "Shell:1"`, `"tri": 2876`, `"z_mm": [5.0, 83.0]` | исходные имена деталей Fusion («Имя:экземпляр») |
| STEP-GS | 9 494 058 байт; `originating_system 'Autodesk Translation Framework v13.14.0.145'`; схема `AUTOMOTIVE_DESIGN` (AP214); `SI_UNIT(.MILLI.,.METRE.)` (стр. 182713); `grep -o -F … \| wc -l`: `MANIFOLD_SOLID_BREP(` 94, `SHELL_BASED_SURFACE_MODEL(` 1, `NEXT_ASSEMBLY_USAGE_OCCURRENCE(` 632, `PRODUCT('` 384 (все имена уникальны), `ADVANCED_FACE(` 4767, `=PLANE(` 3338, `CYLINDRICAL_SURFACE(` 1247, `CONICAL_SURFACE(` 67, `SPHERICAL_SURFACE(` 64, `TOROIDAL_SURFACE(` 12, `B_SPLINE_SURFACE` 59, `COLOUR_RGB(` 5, `MATERIAL_DESIGNATION` 0 | имена `crystal`, `Shell`, `Foam pad` есть; веществ в файле нет |
| Макрос проверки Geant4 | `GEANT4\scripts\geomtest_only.mac:4-6` | `/geometry/test/tolerance 0 mm`, `/geometry/test/resolution 100000`, `/geometry/test/run` |
| `gen_code.py` | `«локальный путь»` | выход любого расширения; синтаксис проверяется ТОЛЬКО для `.py` (`ast.parse`) → для `.cs` нужен свой шлюз компиляции |
| Окружение сборки | `ls` | `.NET SDK 9.0.320`; Desktop runtime 8.0.22…8.0.31; эталонные сборки `.NETFramework\v4.8`; `…\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe`; MSVC `14.44.35207` с `include\msclr`; `dotnet\packs\NETStandard.Library.Ref\2.1.0` (только 2.1); `~\.nuget\packages` — 23 пакета, NUnit/xUnit/MSTest/Newtonsoft/System.Text.Json/Clipper — нет |

## 0.1. Ступень −1 (§33): что уже есть

**(а) BecqMoni.** `rg -i 'stl|3mf|\.obj|step|gdml' --type cs BecquerelMonitor` → 70 файлов, все — слово `Step` в идентификаторах;
уточнённый `rg -i '\bgdml\b|\.stl\b|3mf|wavefront|\.step\b|\.stp\b' --type cs` → 13 строк в 4 файлах (`AmplitudaSerialLine.cs:372`
`job.Step`, `EfficiencySimulator.cs:6119` `relax.Step`, `FsaCascadeSummer.cs:3122-3558` `EnsdfWalk.Step`, `MaterialDatabase.cs:874`
комментарий) — **чтения сеток/STEP и записи GDML в C# нет**. `git ls-tree -r --name-only origin/master | rg -i 'stl|step|gdml|mesh'` (из 6708
файлов, HEAD `cc27a0de`) → журналы/скрипты со словом step и единственный GDML-разборщик `handover/p338-amber234/gdml_summary.py`
(Python, GPL, только чтение GDML). **Свой контур:** `rg --no-ignore -i -l 'tessellated|3dmodel\.model|solid ascii|facet normal|ReadBinaryStl|StlReader' -g '*.cs'`
по `«локальный путь» папка ИИ` (1544 `.cs`) — 0; по `«локальный путь»` (24 757 `.cs`) с `STEPControl_Reader` — 0. Python-доноры
есть: BLD (STL→GDML), `GEANT4\cadmesh-check\stl_bin2ascii.py`, `GEANT4\gs2020-lce\scripts\gdml_bbox.py` — зона GEANT4, образец формата.
**Конструктор** (чужая зона, только чтение): `Конструктор\step2glb.py` (87 строк) — STEP через OCP: `:10` `STEPCAFControl_Reader`, `:62`
`XCAFDoc_DocumentTool.ShapeTool_s`, `:70` `BRepMesh_IncrementalMesh`; пакет `cadquery-ocp-novtk==7.9.3.1.1` (`Конструктор\DECISIONS.md:6`),
остаток #FC-1 принят оператором 16.10 для Linux-образа: «подозрительных 1 (антивирус на `cadquery-ocp-proxy`), не проверено 99»
(`DECISIONS.md:12`). В глобальном Python есть `trimesh` и `cascadio` (`importlib.util.find_spec`) — статус #FC-1 не найден.

**(б) STEP в .NET — кандидаты** (WebSearch/WebFetch 29.10; страницы — в §15 «Источники»):

| кандидат | версия, дата | цели | лицензия | STEP | размер | вывод |
|---|---|---|---|---|---|---|
| OCCSharp (SWIG, OCCT 7.8) | 7.8.0-preview1, 08.04.2025 | только `net8.0` | LGPL-2.1-only | по странице NuGet в списке тулкитов есть `TKDE`, `TKDESTEP`, `TKXSBase` (первый поиск их не показал — расхождение, проверить в репозитории `gitee.com/GDIKodiak/occsharp`) | nupkg 82,37 МБ | предварительная версия, 2,4 тыс. загрузок, репозиторий на gitee; .NET 8 → в процесс BM (4.8) не грузится |
| Inlay.CAD.OCCT | 1.6.0, 20.01.2026 | `net8.0-windows7.0` | LGPL-2.1-only | «IO для STEP и B-rep» к Inlay.CAD | 119 КБ + зависимости `Inlay.CAD ≥1.6.0`, `OCCSharp ≥7.8.0-preview1` | надстройка над OCCSharp, 201 загрузка, репозиторий не указан |
| OcctNet.Wrapper (мост C ABI) | 0.1.1, 23.04.2026 | только `net10.0` | MIT (обёртка), OCCT — своя | `OcctShape.ImportStep`, `Triangulate(linearDeflection)` (README на странице) | nupkg 174,73 МБ, нативные в `runtimes/<rid>/native` | net10 — SDK 10 на машине нет (есть 9.0.320); репозиторий не указан |
| DotNetCore.OCCTProxy (C++/CLI, OCCT 7.9.3) | 1.0.5, 09.02.2026 | `net8.0`, только win-x64 | MIT (обёртка), OCCT LGPL | STEP/IGES → **только GLB** | 27,22 МБ (≈70 МБ распак.) | выход GLB → нужен свой читатель glTF, имена/экземпляры под вопросом; репозиторий не указан |
| Официальный C# Wrapper Open Cascade | — | не указано | коммерческий — **не подтверждено** (страница occt3d.com о цене и лицензии молчит, CHECK-DESIGN-3 F-5) | — | — | отвергнут: нет net48-сборки в открытом доступе, условия не опубликованы |
| Официальный пример OCCT `samples/CSharp` (прокси `OCCTProxy` на C++/CLI + WinForms) | в дереве OCCT | коммит 2022 г.: умолчание примеров переведено на **.NET Framework 4.8** | OCCT LGPL-2.1 + исключение | импорт/экспорт STEP, IGES, BREP в прокси | сборка OCCT + прокси своими руками | единственный путь в процесс .NET 4.8; нужна сборка C++/CLI (MSVC 14.44 есть, `msclr` есть; полнота компонента C++/CLI не проверена) |
| Python `cadquery-ocp(-novtk)` (pybind11, OCCT 7.9) | 7.9.3.1.1 (у Конструктора) | внешний процесс | Apache-2.0 (обёртка; Cloudsmith-листинг для 7.8.1.1), OCCT LGPL | полный (`STEPCAFControl_Reader`, XCAF имена/цвета, `BRepMesh`, `BRepGProp`) | на диске у Конструктора `OCP` 91 МБ + `cadquery_ocp.libs` 63 МБ (`du -sh`) | готовый донор приёма (`step2glb.py`); #FC-1 для Windows-колеса заново |
| Свой разбор STEP Part 21 на C# + своя тесселяция | — | net48 | MIT | AP214 аналитика — да; B-сплайны (59 в STEP-GS), торы, обрезка граней — нет | ≈3–5 тыс. строк | отвергнут для v1: объём и риск больше всех; частичный вариант (только распознавание осей по `CYLINDRICAL_SURFACE`) не даёт сетку |
| IxMilia.Step (чистый C#) | — | — | — | поиск не нашёл страницу пакета | — | не подтверждено (непроверенное №1) |

**Оценка трёх путей (вопрос координатора).**
1. *Ядро + STEP-адаптер отдельным процессом на .NET 8 (OCCSharp/OcctNet/OCCTProxy).* Плюсы: весь код на C#. Минусы: всё равно
   отдельный процесс (BM 4.8 не загрузит net8-сборку) — то есть ровно та же архитектура, что (2); добавляет пользователю второй
   рантайм (.NET 8 Desktop); #FC-1 на 27–175 МБ нативного кода из пакетов без указанного репозитория (кроме OCCSharp — gitee),
   у двух из четырёх — предварительные версии. **Отклонить для v1.**
2. *Внешний Python-процесс с OCP.* Плюсы: есть работающий образец приёма STEP→сетка с именами (`step2glb.py:10-70`); полный API OCCT,
   включая `BRepGProp` — независимый от сетки объём B-rep (оракул §11 а1); Apache-2.0 + отдельный процесс — совместимо с MIT и не
   «объединение» с GPL BM (D2 `:47-48`). Минусы: на машине пользователя BM нужен Python + ≈150 МБ OCP — для конечных пользователей BM
   неприемлемо; #FC-1 Windows-колеса (Linux-аудит Конструктора не переносится: другой бинарник; в его остатке 1 подозрительная находка).
   **Рекомендую для v1** (внутреннее применение: оператор, Amber, GEANT4).
3. *Своя сборка прокси C++/CLI по образцу `samples/CSharp` под .NET 4.8 x64.* Плюсы: в процессе BM, без Python; OCCT LGPL-2.1 с GPL-2
   совместима при динамической связи. Минусы: сборка OCCT (или аудит официальных бинарников OCCT ~сотни МБ), C++/CLI, BM должен уйти с
   AnyCPU на x64 (`csproj:45`) либо грузить прокси в отдельном x64-процессе. **Рекомендую как v2 — путь встраивания в BM**, тем же
   протоколом (§2.3), после «да» Amber.
Итого: интерфейс `IStepReader` в `Cad2Gdml.Step` с двумя реализациями — `OcpProcessStepReader` (v1) и `OcctClrStepReader` (v2);
ядро об OCCT не знает. Объём #FC-1 для v1 — одно колесо `cadquery-ocp-novtk 7.9.3.1.1 win_amd64` (+ его зависимости по `METADATA`).

**(в) Чтение сеток — свой код** (чужих пакетов не нужно; всё в BCL .NET 4.8):
- STL: binary — 80 байт заголовка, `uint32` число граней, по 50 байт на грань (нормаль + 3 вершины `float32` + `uint16`), как BLD :111-146;
  признак binary — длина файла `= 84 + 50·n` (слово `solid` в заголовке ненадёжно); иначе ASCII (`solid/facet normal/outer loop/vertex/endloop/endfacet/endsolid`,
  несколько `solid` — несколько деталей). Единиц в STL нет. ≈150 строк.
- OBJ: `v x y z`, `f a[/b[/c]] …` (многоугольник → веер треугольников, отрицательные индексы — от конца), `o`/`g` — границы деталей,
  `usemtl` — подсказка вещества; прочее игнорируется. Единиц нет. ≈150 строк.
- 3MF: ZIP (`System.IO.Compression.ZipArchive`), путь модели — из `_rels/.rels` (`Relationship` типа `…/3dmodel`, обычно `3D/3dmodel.model`);
  XML пространства `http://schemas.microsoft.com/3dmanufacturing/core/2015/02`: `<model unit>`, `<object id type="model" name>` →
  `<mesh><vertices><vertex x y z/></vertices><triangles><triangle v1 v2 v3/></triangles>`, `<components><component objectid transform>`,
  `<build><item objectid transform>` (матрица 3×4 из 12 чисел). Единицы `unit` ∈ {micron, millimeter, centimeter, inch, foot, meter},
  умолчание millimeter, множители 0.001/1/10/25.4/304.8/1000 (по коду OrcaSlicer и разбору спецификации 1.4.0 — норматив не открыт,
  непроверенное №2). Внешние модели (расширение Production, `p:path`) → отказ `E_FORMAT` в v1. ≈200 строк.
- Итого импорт сеток ≈500 строк + общий «сварщик» вершин и проверки (§4.4) ≈450.

**(г) Запись GDML — свой писатель** (`System.Xml.XmlWriter`, потоковый), структура как BLD (§7); ≈380 строк.

---

## 1. Назначение, границы, лицензия, репозиторий, платформа

- **Что делает.** CAD → (сцена в памяти) → GDML + описание (TXT/JSON) + скетч (PNG/SVG). Два режима входа: «сборка» (один файл с
  детектором и источником) и «раздельно» (файл детектора + файл источника + размещение).
- **Чего не делает (v1):** Blender/glTF/FBX/IGES/PLY; чтение GDML (это `gdml2in`, D2); правка сеток (кроме переворота целиком
  вывернутой детали и выкидывания вырожденных граней); физический расчёт; запись `.in` BecqMoni (это `gdml2in`); 3D-вид.
- **Лицензия.** MIT, файл `LICENSE` в корне; `NOTICE-THIRD-PARTY.md` — OCP (Apache-2.0) и OCCT (LGPL-2.1 + исключение) как внешний
  процесс, не распространяемый в составе (пользователь ставит сам по `step\README.md`). Код BecqMoni не входит (§12.3).
- **Репозиторий (вопрос В2).** Рекомендую **отдельный репозиторий** `cad2gdml` (локально
  `«локальный путь»` (решение оператора 29.10), git; на GitHub `VibeEngineering-LLC/cad2gdml` — только по «да», L1–L3 + #SEC-4).
  **§20 (тройная избыточность, CHECK-DESIGN-3 F-4):** `«локальный путь»` — вне облачная папка, поэтому до первого push единственная копия —
  локальный git. Мера до push: после каждого принятого этапа `git bundle create` в `Програмист\backup\cad2gdml-<дата>.bundle` (облачная папка);
  после «да» на GitHub — три копии.
  Причина: дерево fork BM — GPL-2 (`LICENSE:1-2`); MIT-каталог внутри GPL-репозитория путает лицензию каждого файла и тянет к
  «случайному» импорту GPL-кода. Встраивание в BM позже — ссылкой на сборку `Cad2Gdml.Core.dll` (MIT → GPL допустимо) или копией
  исходников ядра в BM с сохранением шапки MIT. Альтернатива (б): `tools/cad2gdml/` в fork со своим `LICENSE` (как предлагал D2 `:43`).
- **Решение `cad2gdml.sln`:** проекты в формате SDK (`dotnet build`, SDK 9.0.320 есть) с целью `net48`; запасной путь — `build.ps1`
  (ASCII, #PS-1) прямым `csc.exe` BuildTools, как BM собирает пробы (`build_all.ps1:859`).
- **Платформа (вопрос В3).** Рекомендую: **все проекты — `net48`, язык C# 7.3** (`<LangVersion>7.3</LangVersion>`), ядро без
  `System.Drawing`/`System.Windows.Forms`. Обоснование: (1) BM — net48 и C# 7.3 (факты §0) — ядро встраивается и как DLL, и исходниками
  без переделки; (2) ни одной загрузки пакетов (эталонные сборки 4.8 стоят локально). **netstandard2.0 для ядра — можно** (всё, что
  нужно ядру — `XmlWriter`, `ZipArchive`, `Process`, `Regex` — есть в netstandard2.0, а BM 4.8 ссылается на netstandard2.0-сборки), но
  для этого нужен пакет `NETStandard.Library 2.0.3` (в кэше нет, в `dotnet\packs` только 2.1.0) → загрузка + #FC-1; поэтому — вторым
  шагом, одной строкой `<TargetFrameworks>net48;netstandard2.0</TargetFrameworks>`, когда понадобится .NET 8. .NET 8 как основная цель —
  нет: BM его не загрузит.

## 2. Архитектура

### 2.1. Проекты решения

| проект | цель | зависит от | содержимое |
|---|---|---|---|
| `Cad2Gdml.Core` | net48, библиотека | только BCL (`System`, `System.Xml`, `System.IO.Compression`) | модель сцены, геометрия, сетки, импорт STL/OBJ/3MF, кадр, распознавание, оболочка сосуда, вещества, роли, размещение, пересечения, GDML-писатель, геометрия скетча, SVG, описание, проектный файл, конвейер |
| `Cad2Gdml.Step` | net48, библиотека | Core | `IStepReader`, протокол обмена (§2.3), `OcpProcessStepReader` (запуск внешнего процесса), `StepImporter : ISceneImporter`; каталог `python\step_export.py` (MIT, импортирует OCP) |
| `Cad2Gdml.Sketch.Gdi` | net48, библиотека | Core, `System.Drawing`, `System.Windows.Forms` | `GdiSketchRenderer`, `SceneSketchControl : Control`, PNG |
| `Cad2Gdml.Cli` | net48, exe (AnyCPU) | Core, Step, Sketch.Gdi | `cad2gdml.exe`: разбор ключей, коды возврата |
| `Cad2Gdml.App` | net48, WinExe | Core, Step, Sketch.Gdi | `MainForm` и панели (§10.2) |
| `Cad2Gdml.Tests` | net48, exe | все | свой мини-раннер (атрибут `[Check]`, отражение, ≈80 строк), генераторы фикстур, проверки и мутанты (§11) — без NUnit/xUnit (вопрос В5) |
| `tests\oracle\` | Python-скрипты | numpy | независимые оракулы объёмов (§11 а), сверка подписи |
| `tools\similarity_gate.py` | Python | stdlib | шлюз границы GPL (§12.3) |

Будущая `v2`: `Cad2Gdml.Step.Occt` (C++/CLI, net48 x64) — вторая реализация `IStepReader` по образцу `samples/CSharp` OCCT.

### 2.2. Интерфейсы ядра (то, что потом вызовет BecqMoni; C# 7.3)

```csharp
namespace Cad2Gdml.Core
{
    public enum LengthUnit { Unknown, Micron, Millimeter, Centimeter, Meter, Inch, Foot }
    public enum PartRole { None, Crystal, Reflector, Window, Housing, Shield, Vessel, Source, Headspace, Other }
    public enum Severity { Info, Warning, Error }
    public sealed class Diagnostic { public string Id; public Severity Severity; public string PartId; public string Text; }

    public struct Vec3d { public double X, Y, Z; /* +,-,*,Dot,Cross,Length,Normalized */ }
    public sealed class Affine3 { public double[] M = new double[12]; /* 3×4, строки; Apply, Inverse, Multiply, Identity */ }
    public sealed class Mesh { public double[] Xyz; public int[] Tri; /* мм, вершины тройками, грани тройками индексов */ }

    public enum SurfaceKind { Plane, Cylinder, Cone, Sphere, Torus, BSpline, Other }
    public sealed class FaceInfo { public SurfaceKind Kind; public Vec3d Origin, Dir; public double Radius, Radius2, SemiAngle, Area; }

    public sealed class Part
    {
        public string Id;            // стабильный: "<файл>#<путь в сборке>"
        public string SourceName;    // как в CAD (UTF-8, кириллица допустима)
        public string SafeName;      // ASCII для GDML (§7.4)
        public string[] AssemblyPath;
        public Mesh Mesh;            // мм, в координатах сцены (после кадра и размещения)
        public IList<FaceInfo> Faces;   // только из STEP, иначе null
        public double BrepVolumeMm3;    // NaN, если не из STEP
        public int? ColorRgb; public string Layer;
        public PartRole Role; public string RoleBy;           // "aux|arg|rule#k|structure|ui"
        public MaterialSpec Material; public string MaterialBy;
        public SolidShape Shape;     // итог распознавания (§4)
        public PartStats Stats;      // объём, площадь, bbox, масса, флаги сетки
        public string Origin;        // "detector" | "source"
    }

    public sealed class SceneModel
    {
        public List<Part> Parts; public Frame Frame; public Placement SourcePlacement;
        public List<Diagnostic> Diagnostics; public SceneSummary Summary;
        public string DetectorPath, SourcePath; public string DetectorSha256, SourceSha256;
    }

    public interface ISceneImporter
    {
        string[] Extensions { get; }
        ImportResult Import(string path, ImportOptions options);
    }
    public sealed class ImportOptions
    {
        public LengthUnit UnitOverride = LengthUnit.Unknown;
        public double LinearDeflectionMm = 0.01, AngularDeflectionRad = 0.1;
        public System.Threading.CancellationToken Cancel; public IProgress<string> Progress;
    }
    public sealed class ImportResult { public List<Part> Parts; public LengthUnit FileUnit; public List<Diagnostic> Diagnostics; }
    public static class Importers { public static void Register(ISceneImporter i); public static ISceneImporter For(string path); }

    public sealed class SceneOptions
    {
        public MaterialLibrary Library; public RuleSet Rules; public PlacementRequest Placement;
        public FrontRequest Front; public bool Recognize = true;
        public double RevTolMm = 0.01, SnapAxisMm = 0.01, SnapAxisDeg = 0.01, OverlapMaxMm3 = 1.0;
        public IDictionary<string, PartRole> RoleOverrides; public IDictionary<string, string> MaterialOverrides;
    }
    public sealed class SceneBuilder { public SceneModel Build(ImportResult detector, ImportResult source /* null в режиме «сборка» */, SceneOptions o); }

    public abstract class SolidShape { public abstract double VolumeMm3(); }
    public sealed class TubeShape : SolidShape { public double RMin, RMax, Z; /* полная длина */ public Vec3d Center; }
    public sealed class PolyconeShape : SolidShape { public double[] Z, RMin, RMax; public Vec3d Center; }
    public sealed class GenericPolyconeShape : SolidShape { public double[] R, Z; public Vec3d Center; }
    public sealed class BoxShape : SolidShape { public double X, Y, Z; public Vec3d Center; }
    public sealed class TessellatedShape : SolidShape { public Mesh Mesh; }
    public sealed class EnvelopeShape : SolidShape { public PolyconeShape Outer; /* оболочка сосуда §4.3 */ }

    public sealed class GdmlWriteOptions { public double WorldMarginMm = 10, WorldMarginRel = 0.1; public string Naming = "default"; public bool DeviceBox; }
    public sealed class GdmlWriteResult { public long Bytes; public int Volumes, Tessellated, Triangles, Positions; public string Sha256; }
    public sealed class GdmlWriter { public GdmlWriteResult Write(SceneModel s, System.IO.Stream output, GdmlWriteOptions o); }

    public sealed class SketchOptions { public string Plane = "y=0"; public double OffsetMm; public bool Mirror; }
    public sealed class SketchRegion { public string PartId; public PartRole Role; public string Material; public int Argb; public List<double[]> Loops; /* x0,z0,x1,z1… мм */ }
    public sealed class SketchScene { public List<SketchRegion> Regions; public double MinX, MaxX, MinZ, MaxZ; public List<string> Caption; }
    public sealed class SketchBuilder { public SketchScene Build(SceneModel s, SketchOptions o); }
    public static class SvgSketchWriter { public static void Write(SketchScene s, System.IO.TextWriter w, int widthPx, int heightPx); }

    public sealed class SceneDescriber { public string ToText(SceneModel s); public void WriteJson(SceneModel s, System.IO.TextWriter w); }

    public sealed class PipelineRequest { public string DetectorPath, SourcePath, ProjectPath, GdmlOut, JsonOut, TxtOut, SvgOut; public ImportOptions Import; public SceneOptions Scene; public GdmlWriteOptions Gdml; }
    public sealed class PipelineResult { public int ExitCode; public string ExitId; public SceneModel Scene; public SketchScene Sketch; public GdmlWriteResult Gdml; }
    public static class Cad2GdmlPipeline { public static PipelineResult Run(PipelineRequest r); }   // единственная точка входа для BM
}
namespace Cad2Gdml.Sketch.Gdi
{
    public static class GdiSketchRenderer { public static void Draw(System.Drawing.Graphics g, System.Drawing.Rectangle bounds, Cad2Gdml.Core.SketchScene s, System.Drawing.Font font);
                                            public static void SavePng(Cad2Gdml.Core.SketchScene s, string path, int width, int height); }
    public sealed class SceneSketchControl : System.Windows.Forms.Control { public void SetScene(Cad2Gdml.Core.SketchScene s); }
}
namespace Cad2Gdml.Step
{
    public interface IStepReader { StepReadResult Read(string stepPath, ImportOptions o); }   // реализации: OcpProcessStepReader (v1), OcctClrStepReader (v2)
    public sealed class StepImporter : Cad2Gdml.Core.ISceneImporter { public StepImporter(IStepReader reader); /* .step .stp */ }
}
```

BecqMoni при встраивании вызывает `Cad2GdmlPipeline.Run` и показывает `SceneSketchControl`; преобразование `MaterialSpec → GeometryMaterial`
(`GeometryModel.cs:43-51`) пишется **на стороне BM** (GPL), не в MIT-проекте.

### 2.3. Протокол STEP-адаптера (файловый, одинаковый для v1 и v2)

Запуск: `python -I step_export.py IN.step OUTDIR --deflection 0.01 --angular 0.1` (ключ `-I` — изолированный режим; путь Python и
скрипта — из настроек; таймаут 600 с; stderr → журнал). Выход:
- `OUTDIR\bodies.json` (UTF-8): `{format:"cad2gdml-step/1", occt_version, source:{path, sha256, bytes, schema, originating_system,
  unit_name, unit_to_mm}, deflection:{linear_mm, angular_rad}, bodies:[{index, product_name, instance_path[], transform[12] (мм, 3×4),
  color:"#RRGGBB"|null, layer|null, brep:{volume_mm3, area_mm2, centroid[3], bbox[6], closed}, faces:[{kind, origin[3], dir[3],
  radius, radius2, semi_angle, area}], mesh:{offset, nv, nt}}], not_solids:[{product_name, kind}], warnings:[…]}`;
- `OUTDIR\meshes.bin`: подряд по телам `nv` × 3 `float64` (локальные координаты тела, мм) и `nt` × 3 `int32`, little-endian.
  `float64`, а не `float32` — чтобы не терять точность до ядра (§11 в).
- Экземпляры: каждое размещение тела в дереве XCAF — отдельная запись `bodies[]` со своей матрицей, сетка общая (ссылка `mesh.offset`).
  В STEP-GS тел-определений 94, а вхождений `NEXT_ASSEMBLY_USAGE_OCCURRENCE` 632 — число размещённых экземпляров заранее не известно,
  считается адаптером и печатается (непроверенное №3).
- **Зеркальные экземпляры** (det матрицы < 0, CHECK-DESIGN-3 F-12): матрица применяется к вершинам в ядре, затем обход всех граней этого
  экземпляра обращается (иначе нормали внутрь, V < 0); в отчёт — `I_MIRRORED` с именем экземпляра. Тот же путь — для отрицательного масштаба
  в 3MF `<item transform>`. Мутант не вводим отдельно: M1 уже красит вывернутую сетку через `W_MESH_FLIPPED`; годный образец — зеркальная копия
  детали в фикстуре (ноль красных).
- Единицы: OCCT переводит в мм (по умолчанию `xstep.cascade.unit = MM` — непроверенное №4); `unit_name/unit_to_mm` — для отчёта.
- Код возврата ≠ 0 или нет файлов → `E_STEP_ADAPTER` (код 2, §10.1).
Образец приёма OCP — `Конструктор\step2glb.py:46-70` (чтение XCAF + сетка); код пишется заново (§12 — чужая зона).

## 3. Модель сцены

- **Деталь** (`Part`, §2.2): имя из CAD, безопасное имя, путь в сборке, сетка (мм), аналитика граней (STEP), объём B-rep, цвет/слой,
  роль, вещество, форма тела, статистика, происхождение («detector»/«source»).
- **Режим «сборка».** Один файл; детали детектора и источника различаются ролями (§6): всё, что не `Vessel/Source/Headspace` и не
  дочернее сосуда, — детектор.
- **Режим «раздельно».** Файл детектора и файл источника приводятся каждый к своему кадру (§3.2), затем источник ставится по
  `PlacementRequest`: `Mode = Contact` (умолчание: соосно, зазор `GapMm = 0` между ближайшими поверхностями вдоль оси), `Mode = Explicit`
  (`Dx, Dy, Dz` мм, `FlipSource` — поворот источника на 180° вокруг x, `RotZDeg`). CLI: `--place contact[:gap=MM]` или
  `--place dz=MM[,dx=MM,dy=MM][,flip][,rotz=DEG]`; окно — панель размещения (§10.2).
  Зазор «контакт»: лучи вдоль +z по сетке (x, y) с шагом 1 мм в пересечении проекций; `ztop_det(x,y)` — наибольшее z попадания в детали
  детектора, `zbot_src(x,y)` — наименьшее в деталях источника; сдвиг `Dz = Gap − min(zbot − ztop)`. Если колодец Маринелли уже
  детектора (`r_колодца < R_наружн`), источник «садится» на кромку — предупреждение `W_WELL_NARROW`.
- **Единицы.** STEP — из файла (адаптер, §2.3); 3MF — атрибут `unit` (умолчание мм); STL/OBJ — единиц нет: ключ `--unit` (на каждый файл:
  `--unit`, `--unit2`) или выбор в окне; без него — отказ `E_UNIT_MISSING` (код 2). Подсказка (не решение): наибольший габарит < 1 →
  «похоже на метры», > 3000 → «похоже на микроны» (`W_UNIT_SUSPECT`). Боевой случай: STL в метрах —
  `«локальный путь» Про\geant4\mesh\parts\` (17 файлов; «единицы — МЕТРЫ, ось прибора — Y сетки» —
  `…\model\geant4\audit\CLAIMS-2026-09-24-v2.md:5-6`).

### 3.2. Система координат выхода (кадр)

- Ось z — ось кристалла; **z = 0 — плоскость переднего торца кристалла**; +z — от кристалла к источнику; x = y = 0 на оси кристалла.
  Совместимо с gdml2in: там «перед» = +z от кристалла к сосуду, иное → `E_ORIENT` (D2 `:199-200`); абсолютный z торца gdml2in не важен
  (берёт `z_top` кристалла, D2 `:201`).
- Как определяется: (1) деталь с ролью `Crystal` — ровно одна (иначе `E_ROLE_NONE`/`E_ROLE_AMBIG`, код 4); (2) её ось: у STEP — общая ось
  цилиндрических граней; у сетки — собственный вектор тензора инерции с отличающимся моментом (у цилиндра два момента равны, отн. разница
  < 1e-4); у бруска — три взаимно ортогональных направления нормалей граней (кластеризация нормалей; тензор инерции не годится — у куба он
изотропен, CHECK-DESIGN-3 F-2) (рёбра бруска → x, y, z: z — по ребру, обращённому к источнику); (3) знак «перед»: ключ `--front +z|-z`
  (в координатах файла) или `--front-toward <деталь>`; в режиме «сборка» — к центру масс источника/сосуда; детектор без источника — торец, за
  которым меньше массы прочих деталей (отношение масс ≥ 2), иначе `E_FRONT_AMBIG` (код 4) с просьбой задать `--front`; (4) поворот вокруг z —
  ось x кадра = проекция оси x файла (детерминированно), у бруска — по ребру.
- **Прилипание осей.** Деталь, распознанная телом вращения с осью, отстоящей от оси кадра не больше `SnapAxisMm` (0,01 мм) и
  `SnapAxisDeg` (0,01°), ставится точно на ось (сдвиг/поворот в отчёт). Причина: gdml2in отвергает у ролей сдвиг x/y > 1e-6 мм и любой
  поворот (D2 `:139-142`). Ось источника в режиме «сборка» — так же; дальше допуска → `W_SOURCE_OFFAXIS`.

## 4. Распознавание тел и проверки сеток

### 4.1. Тело вращения
Одинаковый путь для STEP и сеток; STEP добавляет точные радиусы и гарантию типа граней.
1. **Кандидаты оси** (по порядку): ось кадра; ось, параллельная z, через центр масс детали; оси инерции детали; у STEP — оси
   цилиндрических/конических граней.
2. **Условие STEP (необходимое):** все грани ∈ {плоскость ⟂ оси, цилиндр/конус с этой осью, сфера с центром на оси, тор с этой осью};
   иначе — не тело вращения (сразу к §4.2).
3. **Профиль:** сечение сетки полуплоскостью φ = φ₀ (через ось; φ₀ — угол первой вершины, чтобы сечение шло через столбец вершин) →
   отрезки → замкнутые контуры (§8.2, общий код) → многоугольник в (r, z), слияние коллинеарных (допуск 1e-4 мм).
4. **Уточнение («прилипание»):** вертикальный отрезок (цилиндр) — r = наибольший радиус вершин сетки в его полосе z (вершины сетки лежат
   на поверхности, хорда — внутри круга, поэтому истинный радиус — наибольший у вершин, и для наружной, и для внутренней поверхности); у STEP — r
   = радиус цилиндрической грани, если отличие ≤ `RevTolMm`; горизонтальный (плоскость) — z плоскости; наклонный (конус) — по двум концам.
5. **Проверка (обе обязательны):** (а) каждая вершина сетки лежит на профиле: расстояние (r_v, z_v) до границы многоугольника ≤ `RevTolMm`
   (0,01 мм = шаг округления gdml2in 0,001 см, D2 `:74`); (б) объём: у STEP |V_проф − V_brep| ≤ 1e-6·V (аналитика точная) —
   при сферах/торах (дуги дискретизированы с прогибом ≤ `LinearDeflectionMm`) ≤ 1e-3·V; у сетки V_сетки ≤ V_проф и
   (V_проф − V_сетки)/V_проф ≤ 1,5·(2π/n_min)²/6 + 1e-6, где n_min — наименьшее число вершин на кольце (потеря вписанного
   многоугольника ≈ (2π/n)²/6 — D2 `:420`). V_проф — теорема Паппа (D2 `:136`).
   (в) **только для сеток — число граней на кольце:** n_min ≥ `RevMinSegments` (24 по умолчанию), иначе деталь — не тело вращения
   (`I_REV_COARSE`, дальше §4.2: брусок или tessellated). Причина (CHECK-DESIGN-3 F-1): правильная N-гранная призма проходит и (а) (её вершины
   лежат на прямоугольном профиле), и (б) (у шестигранной потеря объёма 17,3 % при допуске 27,4 %) — на уровне сетки N-угольник неотличим от
   грубо разбитого цилиндра, различает только число граней. При n = 24 и R = 25,4 мм прогиб хорды R(1 − cos(π/n)) = 0,217 мм, допуск (б) —
   1,7 %; гайка-шестигранник из CAD честно уходит в tessellated. Мутант — M7 (§11 д).
6. **Вид GDML** (по профилю): прямоугольник r ∈ [r₁, r₂] → `tube`; на каждом z одно кольцо [rmin(z), rmax(z)] → `polycone` (ступень —
   двойная `zplane` с тем же z, как `VesselWall_solid`, WATER `:135506-135508`); иначе → `genericPolycone` (`rzpoint`, как BLD :61-86).
   Кристалл gdml2in принимает только `tube`/прямоугольный поликон (D2 `:275`) — у кристалла предпочитаем `tube`.
7. **Только оси, параллельные z.** Тело вращения с осью под углом к z (винт поперёк) → `tessellated` (`I_REV_OFFAXIS`). Причина: не вводить
   в v1 `<rotation>` у `physvol` (знак поворота GDML — частый источник ошибок); позиция x, y ≠ 0 при оси ∥ z допустима (штырь сбоку).

### 4.2. Брусок и прочее
- `box`: 6 плоских граней (STEP) или все вершины на 6 плоскостях параллелепипеда осей инерции (сетка), объём = X·Y·Z (отн. 1e-6);
  рёбра ∥ осям кадра (≤ 0,01°) — иначе `tessellated`. Нужен для кристалла-бруска (пример подписи BM «Детектор: 10 × 10 × 10 мм») и кювет.
  Внимание: gdml2in v1 брусок-кристалл не сводит (`E_CRYSTAL_SHAPE`, D2 `:275`) — это ограничение gdml2in, не cad2gdml.
- Всё прочее → `tessellated` (вершины в координатах сцены, ABSOLUTE, §7).
- Ключ `--no-recognize` — всё `tessellated` (для сравнения и отладки).

### 4.3. Сосуд и проба: оболочка (иерархия как в WATER)
gdml2in ждёт сосуд **сплошным поликоном-огибающей** с колодцем (rmin > 0 ниже плеча) и пробу/пустоту — дочерними объёмами со сдвигом 0
(D2 `:144`, `:163`, `:230-231`; WATER `:135556-135563`). В CAD же стенка сосуда — оболочка, проба — отдельное тело. Поэтому:
- если сосуд и проба распознаны телами вращения на оси кадра: строится **огибающая** `Envelope = Wall ∪ Sample ∪ полость` методом «интервалы по
  слоям z»: все z-узлы профилей → в каждом слое на каждой высоте множество интервалов r стенки и пробы → огибающая = [min r, max r]
  (одно кольцо → `polycone`), пустота `Headspace = Envelope − Wall − Sample` (интервалы; несколько связных частей → несколько объёмов
  `Headspace_k`). Открытый сверху сосуд (полость не замкнута) — закрывается плоскостью кромки, `W_VESSEL_OPEN`.
- Объём `LV_<сосуд>` = огибающая с веществом стенки, дочерние: проба (вещество пробы), пустота (G4_AIR) со сдвигом 0; сам сосуд — в World
  со сдвигом по z. Проверка: V_огибающей = V_стенки + V_пробы + V_пустоты (отн. 1e-9) и V_стенки по огибающей = V_стенки по CAD (≤ 1e-6·V).
- иначе (кювета-брусок, сетка без симметрии): плоско — стенка, проба, пустота — соседи в World (`I_VESSEL_FLAT`); Geant4 это примет,
  gdml2in откажет `E_NOT_MARINELLI` (D2 `:231`) — ожидаемо.
- ≈200 строк своего кода, без библиотек 2D-булевых операций (Clipper не нужен).

### 4.4. Проверки сеток
| проверка | правило | итог |
|---|---|---|
| сварка вершин | сетка хэширования с шагом 1e-5 мм (как ключ BLD :153-170) | число слитых |
| вырожденные грани | повтор индекса или площадь < 1e-8 мм² (BLD :176-203) | выбрасываются, счёт в отчёт |
| замкнутость | каждое неориентированное ребро — ровно в 2 гранях | нет → `E_MESH_OPEN` (рёбер с 1 гранью > 0) / `E_MESH_NONMANIFOLD` (> 2), код 3 |
| согласованность ориентации | каждое ребро проходится парой граней в противоположных направлениях | нет → `E_MESH_ORIENT`, код 3 |
| вывернутость | знаковый объём < 0 при согласованной ориентации → перевернуть все грани (как BLD :211-218) | `W_MESH_FLIPPED` |
| мало граней | < 4 | `E_MESH_OPEN` |
| пересечение деталей | для каждой пары с пересекающимися bbox — 1e5 случайных точек в пересечении bbox (зерно фиксировано), «внутри» — чётность по 3 лучам, решение большинством (D2 `:426-428`); оценка `V_ov = доля·V_bbox`, σ биномиальная | `V_ov > max(1e-3 мм³, 3σ)` → `W_OVERLAP`; `V_ov > OverlapMaxMm3` (умолч. 1 мм³) → `E_OVERLAP`, код 6 |
| дочерний внутри матери | все вершины пробы/пустоты внутри огибающей (bbox и точечный тест) | нет → `E_CHILD_OUTSIDE`, код 6 |
| объём/площадь/масса | знаковая сумма тетраэдров; площадь; масса = V·ρ | в описание |
| самопересечение внутри детали | v1 — не проверяется (дорого); ловит `/geometry/test` Geant4 | отмечено в отчёте «не проверено» |
Ускорение лучей/точек — своё дерево AABB (≈180 строк). Открытые детали не чинятся (v1): `--drop-open` выкидывает их с `W_PART_DROPPED`.

## 5. Вещества

- **Таблица** `data\materials.default.json` (UTF-8, пишется руками `Write`, у каждой строки `provenance` = `file:line` или URL):
  `{ "format":"cad2gdml-materials/1", "world_material":"G4_AIR", "materials":[ {"name":"G4_SODIUM_IODIDE", "g4":"G4_SODIUM_IODIDE",
  "density_g_cm3":3.667, "fractions":{"11":0.153373,"53":0.846627}, "state":"solid", "bm_name":"Sodium iodide", "provenance":"…"} , …,
  {"name":"MgO_powder", "g4":null, "density_g_cm3":1.8, "fractions_g4":[["G4_Mg",0.603],["G4_O",0.397]], …} ], "rules":[…] }`.
  - NIST-вещества (`g4` = имя Geant4) — плотность и доли по Z для описания/массы (в GDML пишется только `materialref="G4_…"`, Geant4
    знает их сам — как WATER, `materialref ref="G4_STAINLESS-STEEL"`). Плотности — из `G4NistMaterialBuilder.cc` Geant4 11.2 (номер строки
    вписывается при задаче C16; D2 уже ссылается на `:857` воздух, `:1864` сталь, `:1499` поликарбонат).
  - Свои вещества — как `custom` BLD (`density_g_cm3`, `fractions` парами `[ссылка, доля]`, ссылка — элемент `G4_<символ>` или другое
    вещество; BLD :247-256) → в GDML `<material><D/><fraction n ref/>`.
  - Совместимость с BM: доли по Z — тот же вид, что `GeometryMaterial.Fractions` (`GeometryModel.cs:49-51`); поле `bm_name` — имя библиотеки
    BM (`GeometryMaterialLibrary.cs:188` «Sodium iodide», `:219` «Aluminum», `:227` «Polypropylene») — подсказка для gdml2in/BM, не физика.
  - Начальный набор (≈20): воздух, NaI, CsI, LaBr3, CeBr3, BGO, Al, сталь нерж., Cu, Pb, Sn, Si, PP, PE, ПЭ-пена, PTFE, PC, эпоксид, FR4,
    SiO₂ (плавл.), MgO-порошок, вода — с провенансом каждой строки; доли — массовые (атомные веса не нужны).
- **Назначение** (первое сработавшее решает, расхождение — `W_MAT_CONFLICT`): (1) ручное (окно / `--set-material "<имя>=<вещество>"`);
  (2) правила проекта `rules` (как `stl_rules` BLD: `[регэксп, вещество]`, `re.search` по `SourceName`, первое совпадение — BLD :43-47),
  плюс ключи `color:#RRGGBB` и `layer:<имя>` для STEP; (3) правило роли (`Crystal` без вещества — нет умолчания!). Файл GS2020
  `…\model\geant4\materials.json` читается как есть (`stl_rules` → `rules`, `custom` → свои вещества, `stl_default` → `--default-material`).
- **Нет вещества:** `E_MAT_UNASSIGNED` (код 5) со списком деталей; `--default-material X` → назначается явно, `W_MAT_DEFAULT` на каждую. Молча не
  угадываем.

## 6. Роли

| роль (`PartRole`) | значение в GDML | для gdml2in |
|---|---|---|
| Crystal | `BecqMoni:Role = crystal` | обязательна, ровно одна |
| Reflector | `BecqMoni:Role = reflector` | 0 или 1 |
| Source (проба; активный объём источника) | `BecqMoni:Role = source` | ровно одна при наличии источника |
| Vessel | `BecqMoni:Role = vessel` | мать пробы |
| Headspace | `BecqMoni:Role = headspace` | дочерние сосуда |
| Housing / Shield / Window / Other | `Cad2Gdml:Role = housing|shield|window|other` | gdml2in не читает — его словарь ровно пять значений (D2 `:150-151`); отдельный `auxtype`, чтобы не менять gdml2in |

- Запись: `<auxiliary auxtype="BecqMoni:Role" auxvalue="…"/>` внутри `<volume>` (Geant4 читает `auxiliary` внутри `volume`, значения не
  ограничены — D2 `:150-151` со ссылками `G4GDMLReadStructure.cc:792-795`, `G4GDMLRead.cc:328-339`). Плюс
  `<auxiliary auxtype="Cad2Gdml:SourceName" auxvalue="<имя из CAD>"/>` — исходное имя (кириллица допустима, XML в UTF-8).
  Положение `auxiliary` — после всех `physvol` (порядок по схеме GDML — непроверенное №5).
- Определение (первое сработавшее): ручное/`--role "<регэксп>=<роль>"`/`--crystal <имя>` … → правило по имени (те же регэкспы, что D2 §4
  `:154-160`: `crystal|nai|csi|labr|cebr|bgo|sri2|czt|gso`, `reflector`, `source|sample|проба`, `vessel|beaker|marinelli|сосуд|стакан`,
  `headspace|empty`; плюс `housing|shell|корпус`, `shield|защит`, `window|окно`) → структура (дочерние сосуда). Неоднозначно → `E_ROLE_AMBIG`.
- Имена LV для совместимости с регэкспами gdml2in: `LV_<SafeName>`; у роли, чьё безопасное имя не проходит регэксп gdml2in, — без
  переименования (aux достаточно, ступень 1 D2 `:150`).
- Активный объём источника: `physvol` пробы получает имя `PV_LV_<SafeName>` — по нему контур GEANT4 задаёт источник (их макросы — их зона).
  Режим `--naming gs2020-device` повторяет соглашения BLD: кристалл `Crystal_log`, коробка `RC103_device_log` (BLD :81, :267-271) — для
  приложений GEANT4, ждущих эти имена.

## 7. GDML-писатель

### 7.1. Структура (как BLD, с дополнениями)
```
<?xml version="1.0" encoding="UTF-8"?>
<!-- cad2gdml <ver>; detector=<имя> sha256=<16>; source=…; options=<sha16> -->
<gdml xmlns:xsi=… xsi:noNamespaceSchemaLocation=".../gdml.xsd">     (как BLD :303-304)
<define>   <position name="v<i>_<k>" x y z unit="mm"/> …            (вершины tessellated, как BLD :225-230; позиции physvol)
<materials> своё: <material name state><D value unit="g/cm3"/><fraction n ref/>…</material>   (как WATER :45089-45095)
<solids>   <box name="WorldBox" …/>; tube/polycone/genericPolycone (aunit="deg" lunit="mm", startphi="0" deltaphi="360");
           <tessellated name="S_<имя>"><triangular vertex1 vertex2 vertex3 type="ABSOLUTE"/>…</tessellated>
<structure> volume деталей (materialref, solidref, [physvol…], auxiliary…), сосуд с дочерними, World последним
<setup name="Default" version="1.0"><world ref="World"/></setup>
```
- **Иерархия (выбор):** детали детектора — плоско в World (как WATER: 46 деталей прибора плоско, D2 `:144-145`; BLD :282-297);
  сосуд — огибающая с дочерними пробой и пустотой (§4.3; как WATER `:135562-135563`). Обоснование: (1) gdml2in определяет сосуд как
  «мать пробы» (D2 `:163`) и сворачивает Маринелли по поликонам огибающей (D2 §5.3); (2) Geant4 требует, чтобы дочерние лежали внутри
  матери и соседи не пересекались — проверяем сами (§4.4). Режим `--device-box` (как BLD DEVICE :269-271) — по запросу GEANT4.
- **World:** `box` с центром 0, полуребро по каждой оси = max(|bbox сцены|) + max(`WorldMarginMm` = 10 мм, 10 %), вещество `G4_AIR`
  (как BLD :39-40 `world_material`).
- **Порядок:** детали — по `AssemblyPath` (детерминированный выход: одинаковый вход и ключи → побайтно одинаковый GDML; проверка C_det).

### 7.2. Числа
- Только `CultureInfo.InvariantCulture` (каждый вызов `ToString`/`Parse` — с ней; плюс `Thread.CurrentCulture = Invariant` на входе CLI/App
  как страховка; мутация «убрать InvariantCulture» под ru-RU должна краснеть — §11 д).
- Длины: мм, `Math.Round(v, 6)` → формат `"0.######"`, `-0` → `0` (как 6 знаков BLD :226-228); углы — градусы целые (`360`); плотности —
  `"G6"`; доли — нормированы к сумме 1 (|Σ−1| > 1e-6 до нормировки → `W_FRACTION_SUM`), формат `"G6"`.
- Вершины: одна `<position>` на уникальную вершину детали (после сварки §4.4), имена `v<номер детали>_<номер вершины>` (как BLD :206-208, :229).

### 7.3. Совместимость с Geant4 11.2 (что соблюдаем)
Нормали наружу (знаковый объём > 0); `ABSOLUTE`; имена уникальны, ASCII `[A-Za-z0-9_]`, без подстроки `0x` (парсер Geant4 по умолчанию
отрезает имена по `0x` — непроверенное №6); `polycone` — `zplane` по неубыванию z; `genericPolycone` — простой многоугольник, r ≥ 0;
`tube` — `z` полная длина (D2 `:129`); схема в шапке как у файлов, которые GEANT4 уже читает (BLD :304).

### 7.4. Безопасные имена
Кириллица (Компас) → транслитерация по своей таблице (ГОСТ 7.79-2000 Б, без диакритики), прочее → `_`, слияние `_`, обрезка до 64 знаков,
суффикс `_<n>` при совпадении. Исходное имя — в `Cad2Gdml:SourceName` и в описании.

### 7.5. Размер и время (оценка, не измерение — #AH-1)
V3: 11 887 938 байт на 89 818 граней ≈ 132 байт/грань (с вершинами); для 90 тыс. граней ожидается ≈12 МБ. Запись `XmlWriter` в поток —
по оценке < 1 с; распознанные тела вращения уменьшают объём (кристалл, корпус, сосуд — десятки чисел вместо тысяч граней). STEP-GS при
прогибе 0,01 мм может дать сотни тысяч граней (непроверенное №7) → GDML до десятков МБ; меряется при приёмке (§11 а), печатается в отчёт.

## 8. Скетч (осевой разрез)

### 8.1. Что рисуется
- Плоскость разреза по умолчанию `y = 0` (содержит ось z); показываются обе половины x ∈ [−R, R] — как BM (`outerHalf` слева и справа, GS:279-283).
  Ключ `--section y=0|x=0|angle=DEG[,offset=MM]` — произвольная плоскость через ось или сдвинутая.
- **Тела вращения** — профиль аналитически (многоугольник (r, z) и зеркальный); **брусок и tessellated** — сечение граней плоскостью: для каждой грани,
  пересекающей плоскость, — отрезок; вершины на самой плоскости сдвигаются символически на +ε (|y| < 1e-9 мм → y = +1e-9), так исчезают
  вырожденные случаи; отрезки сшиваются в контуры по общим концам (хэш с шагом 1e-6 мм); незамкнутый контур → `W_SECTION_OPEN` (рисуется
  линией). Заливка — правило чёт-нечёт по контурам детали. 90 тыс. граней — один проход, результат кэшируется (как предполагал RESEARCH-1 §8(б)).
- **Цвета** — по роли, значения совпадают с палитрой BM (GS:48-61) ради узнаваемости; хранятся как данные `data\palette.json` с пометкой
  провенанса (числа цвета — данные, не код): Canvas `D6D2C4`, Crystal `35A5AD`, Reflector `8CECEC`, Housing (Cladding) `8290B0`, Gap/Headspace
  `E8EEF6`, Vessel (Wall) `A6D5E8`, Source (Sample) `78808E`, Ink `202020`. Ролей нет в BM (Shield, Window, Other) — свои; детали без роли —
  цвет вещества из фиксированного ряда 12 приглушённых цветов по устойчивому хэшу имени вещества; легенда «цвет — вещество» по ключу `--legend`.
- **Масштаб** — единый по обеим осям, `scale = min(sx, sy)` (GS:319-323), поля как у миниатюры BM 10/8/8 точек (GS:301-303); +z вверх
  (источник над детектором — как BM, GS:13-15); штрихпунктир оси; масштабная линейка 10 мм (по ключу).
- **Подпись** в левом верхнем углу на полупрозрачной подложке (как GS:572-575, `ARGB C8FFFFFF`), шрифт ужимается под ширину (GS:555-569):
  1. `Детектор: ⌀<D> × <H> мм` (кристалл `tube`) или `Детектор: <X> × <Y> × <Z> мм` (брусок);
  2. по типу источника: Маринелли (огибающая с колодцем) — `Стакан Маринелли: ⌀<Dнаруж> × <Hсосуда> мм`; цилиндр — `Цилиндр: ⌀<D> × <Hпробы> мм`
     и `До детектора: <d> мм`; кювета — `Кювета: <X> × <Y> × <H> мм` и `До детектора: <d> мм`; источника нет — строки нет;
  3. своё: `<N> деталей, <M> веществ, масса <m> г` (+ `проба <V> мл, <m> г`, если есть).
  Числа строк 1–2 — `ToString("G4", InvariantCulture)`, разделитель `" × "`, префикс `"⌀"`, единица `" мм"` — как `Dims` BM (GS:479-487);
  слова — из своих ресурсов, совпадающих с `Resources.ru.resx` (:1494-1513, :1257-1258). Порядок строк — как `AnnotateOverview`
  (сначала детектор, потом источник, GS:496-546). Что BM понимает под «До детектора» и под диаметром цилиндра (`BeakerDiameter`,
  GS:533-538), — не прослежено (непроверенное №8); в приёмку v1 входят только строки 1 и Маринелли.
- **Вывод:** PNG (GDI+, `System.Drawing`, `SmoothingMode.AntiAlias` как GS:217) и SVG (текстом, без зависимостей) — одна `SketchScene`.
  В окне — `SceneSketchControl` (двойная буферизация `OptimizedDoubleBuffer|AllPaintingInWmPaint|UserPaint|ResizeRedraw`, как GS:67-68).
- 3D-вида нет (решение оператора).

### 8.2. Сшивка контуров (общий код с §4.1)
Отрезки → карта «конец → отрезки»; обход с выбором единственного продолжения; при развилке (касание контуров в точке) — продолжение с
наименьшим поворотом; длина контура < 1e-6 мм — выброс. ≈200 строк (задача C13).

## 9. Описание сцены

- **JSON** (`*.cad2gdml.json`, UTF-8 без экранирования кириллицы): `tool, version, exit_code, exit_id, inputs[{path, sha256, bytes, format, unit,
  unit_to_mm, adapter{occt_version, deflection}}], frame{crystal_part, axis_file[3], front, origin_file[3], snapped[]}, placement{mode, dx, dy, dz,
  flip, gap_mm}, parts[{id, source_name, safe_name, assembly_path, origin, role, role_by, material, material_by, density_g_cm3, density_source,
  shape{type, params}, volume_mm3, volume_brep_mm3, volume_mesh_mm3, area_mm2, mass_g, bbox_mm[6], triangles, welded, degenerate_removed,
  flipped, open_edges, recognition{tried[], result, max_vertex_dev_mm, vol_rel_dev}}], overlaps[{a, b, volume_mm3, sigma_mm3}], materials[{name, g4,
  density, fractions, used_by}], summary{n_parts, n_materials, mass_total_g, mass_detector_g, mass_source_g, crystal{shape, d_mm, h_mm},
  vessel{type, d_mm, h_mm}, source{volume_ml, mass_g}, gdml{bytes, volumes, tessellated, triangles, positions, sha256}}, warnings[{id, part, text}]`.
- **Текст** (`*.cad2gdml.txt`, по-русски): шапка (входы, единицы, кадр), таблица деталей (имя, роль, вещество, ρ, V см³, m г, тип тела,
  габариты), сводка (как строки подписи + масса), предупреждения списком с ID.

## 10. Командная строка и окно

### 10.1. CLI
```
cad2gdml convert DETECTOR [SOURCE] -o OUT.gdml
         [--unit mm|cm|m|in|um] [--unit2 …] [--place contact[:gap=MM] | dz=MM[,dx=MM,dy=MM][,flip][,rotz=DEG]]
         [--front +z|-z | --front-toward NAME] [--materials FILE.json]... [--set-material "REGEX=MAT"]... [--default-material MAT]
         [--role "REGEX=ROLE"]... [--crystal NAME] [--source-part NAME] [--vessel NAME]
         [--deflection 0.01] [--angular 0.1] [--rev-tol 0.01] [--snap-axis 0.01,0.01] [--overlap-max 1] [--no-recognize] [--drop-open]
         [--naming default|gs2020-device] [--device-box] [--png OUT.png] [--png-size 800x600] [--svg OUT.svg] [--section …] [--legend]
         [--json OUT.json] [--txt OUT.txt] [--project FILE.cad2gdml-project.json] [--step-python PATH] [--strict]
cad2gdml inspect FILE [--unit …]      — список деталей, единицы, проверки сеток; GDML не пишется
```
Печатается одна строка итога `cad2gdml: код N <ID>: <текст>` (как D2 `:80`). Коды:

| код | смысл | ID |
|---|---|---|
| 0 | GDML и описание записаны | — |
| 1 | внутренняя ошибка | `E_INTERNAL` |
| 2 | вход не читается / не поддержан | `E_FORMAT`, `E_UNIT_MISSING`, `E_STEP_ADAPTER`, `E_3MF_EXTERNAL` |
| 3 | сетка непригодна | `E_MESH_OPEN`, `E_MESH_NONMANIFOLD`, `E_MESH_ORIENT` |
| 4 | роли/кадр | `E_ROLE_NONE`, `E_ROLE_AMBIG`, `E_FRONT_AMBIG` |
| 5 | вещества | `E_MAT_UNASSIGNED`, `E_MAT_UNKNOWN` |
| 6 | геометрия сцены | `E_OVERLAP`, `E_CHILD_OUTSIDE` |
| 7 | `--strict` и есть предупреждения | `E_STRICT` |

Проектный файл `*.cad2gdml-project.json` (входы, единицы, размещение, правила, ручные назначения) — общий для окна и CLI: окно его
сохраняет, `cad2gdml convert --project …` воспроизводит тот же GDML (C_det).

### 10.2. Окно (WinForms, без дизайнера — разметка кодом)
Сценарий: **Открыть детектор…** (и при раздельном режиме **Открыть источник…**; для STL/OBJ — диалог единиц) → импорт в фоне с ходом и
отменой → слева таблица деталей (`DataGridView`: имя, роль — список, вещество — список с поиском, ρ, V, m, тип тела, флаги) и список
предупреждений; справа — скетч (`SceneSketchControl`, клик по области выделяет строку, и наоборот) → назначить роли/вещества (правка
таблицы перестраивает скетч и проверки; «Правила…» — редактор правил) → панель размещения (контакт/зазор, dz, dx, dy, переворот,
поворот) → **Проверить** (пересечения) → **Сохранить GDML…** (рядом пишутся `*.cad2gdml.json`, `*.txt`, `*.png`, проект). Строки
интерфейса — русские и английские (свои ресурсы).

---

## 11. Приёмка (числа и оракулы)

**(а) Боевой STEP-GS → GDML.**
- (а1) **B-rep ↔ сетка, по каждому телу-определению (94):** |V_сетки − V_brep| ≤ 1e-3·V_brep при прогибе 0,01 мм (D2 `:420`; V_brep —
  `BRepGProp` в адаптере, интегрирование по поверхностям — путь, независимый от тесселяции и от нашего C#); тела с V < 1 мм³ — |ΔV| ≤ 1e-3 мм³.
  Печать «прошло K/94», не прошедшие — с типами граней. `SHELL_BASED_SURFACE_MODEL` (1 шт.) — в `not_solids`, в GDML не идёт (`W_STEP_SHELL`).
- (а2) **Сходимость:** прогибы 0,01 и 0,005 мм — |V(0,005) − V_brep| ≤ |V(0,01) − V_brep| у ≥ 95 % тел (порог назначен, не измерен).
- (а3) **Независимая тесселяция PV3:** 41 STL из PV3 (вход BLD для V3), объёмы — **свой Python-скрипт** `tests\oracle\stl_volumes.py` (numpy, знаковая
  сумма тетраэдров в float64; не C#, не OCCT). Сопоставление: имя манифеста `"Shell:1"` ↔ (`product_name` `Shell`, номер экземпляра 1);
  при несовпадении имени — по объёму (±1 %) и отсортированным габаритам bbox (±0,1 мм). Критерий: по сопоставленным — медиана
  |ΔV|/V ≤ 2e-3, наибольшее ≤ 1e-2 (тесселяция PV3 другая и её точность неизвестна — порог назначен, не измерен); печать «сопоставлено
  K из 41» и список несопоставленных. Ожидаемо не сопоставятся детали кристального блока: в V3 7 тел — `genericPolycone` из профилей
  (V3 `genericPolycone name` = 7; BLD :61-86), а в STEP-GS кристалл — тело `crystal`.
- (а4) **V3 как третий путь:** V3 построен BLD из PV3 (тот же набор граней без вырожденных, BLD :176-203) → объёмы его tessellated, посчитанные
  `git show origin/master:handover/p338-amber234/gdml_summary.py` (только запуск, GPL не входит в проект; объём — `gdml_summary.py:36-39` по D2 `:134`),
  совпадают с (а3) до 1e-6 отн.; расхождение — дефект оракула, разбор до приёмки.
- (а5) **Наш GDML читается независимо:** `gdml_summary.py` на НАШЕМ GDML даёт объёмы tessellated, равные нашему JSON (отн. 1e-6) — проверка писателя.
- (а6) **Экземпляры:** число размещённых экземпляров в GDML = число записей `bodies[]` адаптера (печать обоих; 632 `NAUO` — справочно).

**(б) Geant4 и gdml2in.**
- (б1) Письмо контуру GEANT4 (#BUS-2, файл в `_interchat\inbox\GEANT4\`; §12 — сами не запускаем): загрузить наш GDML их приложением и
  прогнать `geomtest_only.mac` (`:4-6`); критерий — 0 строк «Overlap is detected», код 0, число объёмов = наше; по желанию — `GetCubicVolume`
  наших тел. Запасной путь, если молчат: свой `E_OVERLAP` (§4.4) + (а5).
- (б2) `gdml2in` (D2, когда Ф0 будет написан) на нашем GDML сцены «STEP-GS + сосуд Маринелли размеров OISN» (сосуд — STEP от оператора, В4,
  либо наша фикстура-поликон по WATER `:135506-135512`): код 0, роли — по `aux` (`roles.*.by = aux` в отчёте gdml2in, D2 `:95`), 19 полей
  записаны. Совпадение DS-полей с Sc-O (D2 §5.4) — **справочно, не критерий**: тождество тел STEP-GS и деталей GS2020-GDML не доказано (D2 `:521`).

**(в) Сетки ↔ STEP.**
- (в1) Наш экспорт STL (`float32`) тела из адаптера ↔ та же сетка `float64`: |ΔV| ≤ max(1e-3 мм³, √3·A·u/2), u — шаг `float32` при наибольшей
  |координате| тела (u = 2^(e−23); при |x| ∈ [64, 128) мм u = 7,63e-6 мм). Обоснование: каждая координата вершины сдвигается ≤ u/2, вектор
  сдвига ≤ √3·u/2 = 6,61e-6 мм → |ΔV| ≤ A·√3·u/2 (первый порядок). При стенке ≥ 1 мм (A/V ≤ 2 мм⁻¹) это ≤ √3·u·V = 1,32e-5·V — **выше**
  фиксированного порога D2 1e-5·V (`:416-419`); поэтому порог считается по телу, а не берётся из D2 (поправка CHECK-DESIGN-3 F-3: прежний
  множитель 1,5·u/2 не был оценкой сверху).
- (в2) 3MF того же тела (наш экспорт, координаты текстом с 6 знаками) — |ΔV| ≤ max(1e-3 мм³, A·5e-7) (округление 1e-6 мм → полушаг 5e-7).
- (в3) STL/3MF, выгруженные **из Fusion/Компаса оператором** (В4) — ↔ V_brep: печать |ΔV|/V и уточнение Fusion; критерий ≤ 1e-3 только при
  «высоком» уточнении экспорта (иначе — справочно).
- (в4) Распознавание: кристалл STEP-GS (`crystal`) → `tube`, |V_tube − V_brep| ≤ 1e-6·V; тот же кристалл из PV3/STL → `tube`, радиус = радиус
  STEP ± 0,01 мм.

**(г) Скетч.** Эталонные сцены: (Э1) NaI ⌀50,8 × 50,8 мм в Al-оболочке + Маринелли размеров Sc-O (D2 §5.3: Ø145, H 109, колодец Ø82 × 55,5);
(Э2) брусок 10 × 10 × 10 мм + Маринелли ⌀59,6 × 48,6 мм (пример оператора). Оракул — **сам BecqMoni**: проба по образцу `SketchShot.cs`
(в fork BM, GPL-сторона; режим `Overview`, вывод `Labels[].Text` — GS:134-137; **доработка пробы:** существующий `SketchShot.cs` рисует только
режимы Detector и Source, а подпись строится лишь в `AnnotateOverview` — пробу надо расширить режимом Overview, CHECK-DESIGN-3 F-7) на `.in` той же конфигурации (Э1 — `P/scenes/GS2020_mar1l_oisn10.in`;
Э2 — `.in` от оператора или из BM). Критерий: строки 1–2 нашей подписи **побайтно** = строкам BM («Детектор: ⌀50.8 × 50.8 мм» /
«Стакан Маринелли: ⌀145 × 109 мм» — ожидание по формату `Dims`, проверяется прогоном, не памятью). Плюс числовая приёмка картинки: ни одна
область не выходит за поле; ширина области кристалла на PNG в пикселях = `D·scale` ± 1 пиксель.

**(д) Мутации (#SA-3) и боевые образцы (#SA-3a).**
Проверки: `C_exit` (код + ID), `C_vol` (объёмы деталей против оракула фикстуры), `C_frame` (ось кристалла ∥ z, торец z = 0, число распознанных
примитивов), `C_warn` (множество предупреждений = ожидаемому), `C_label` (строки 1–2 подписи), `C_xml` (GDML разбирается `XmlReader`, числа
разбираются Invariant, счётчики `<volume>/<tessellated>/<triangular>/<position>` = `GdmlWriteResult`), `C_det` (два прогона — один sha256),
`C_unit_in` (фикстура в дюймах). Остальные проверки на выходе ≠ 0 не применяются (как D2 §7 г). Мутант входа — копия годной фикстуры с одной правкой.

| № | мутация | ожидается красной ровно |
|---|---|---|
| M1 | одна деталь (не кристалл, не сосуд) в STL целиком вывернута (все грани обратным обходом) | `C_warn` (`W_MESH_FLIPPED`); объёмы верны — авто-переворот |
| M2 | дырка: удалена одна грань детали | `C_exit` (3, `E_MESH_OPEN`) |
| M3 | две детали перекрываются: деталь корпуса сдвинута на 1 мм внутрь соседней | `C_exit` (6, `E_OVERLAP`) |
| M4 | деталь без вещества: имя изменено так, что не ловит ни одно правило (роль та же — `Other`) | `C_exit` (5, `E_MAT_UNASSIGNED`) |
| M5 | STL без `--unit` | `C_exit` (2, `E_UNIT_MISSING`) |
| M6 | одна вершина детали сдвинута на 0,1 мм поперёк ⇒ деталь перестаёт быть телом вращения | `C_frame` (на одно распознанное меньше; деталь → tessellated) |
| M7 | цилиндр детали заменён правильной шестигранной призмой того же описанного радиуса (STL) | `C_frame` (на одно распознанное меньше; `I_REV_COARSE`) |
| G1 | годные: цилиндр, разбитый ровно на 24 грани по кольцу (граница `RevMinSegments`) — остаётся `tube`; STEP в дюймах (тот же прибор, `CONVERSION_BASED_UNIT('INCH')`); весь STEP повёрнут (90° вокруг x + 17° вокруг z) и сдвинут; STL в метрах с `--unit m`; CRLF↔LF в ASCII STL; порядок деталей в файле переставлен | **ноль** красных |

Мутации кода (по одной на правило): множитель дюйма 25,4 → 2,54 — только `C_unit_in`; убрать `InvariantCulture` в писателе (прогон в ru-RU) —
только `C_xml`; убрать авто-переворот — `C_vol` у M1-фикстуры (при годном входе — ноль); выключить прилипание оси — только `C_frame`
на фикстуре «повёрнутый STEP»; знак «перед» наоборот — `C_frame`; огибающая без пустоты (Headspace не строится) — `C_vol` (V_огиб ≠ сумма) ;
`G4` → `G6` в подписи — только `C_label`; сортировка по хэшу вместо пути — только `C_det`.
**Боевые образцы (фактический вход):** STEP — 1 (STEP-GS, 94 тела); STL — PV3 41, `…\model\geant4\parts_stl\` — 41 запись манифеста (`grep -c '"file"'`), но 36 STL на диске (CHECK-DESIGN-3 F-8; брать по диску),
`GEANT4\atom-nano5-labr\gdml\parts_v8\` 24 и `parts\` 22 файла (`find`, без манифеста не считал), «АтомСпектра Про» в метрах — 17 + `AtomSpectraPro_full.stl`;
OBJ — 1 (`…\АтомСпектра Про\geant4\mesh\AtomSpectraPro_full.obj`); **3MF — 6** (`«локальный путь» модели`, файлы для печати — CHECK-DESIGN-3 F-8; прежнее «0» было по рабочим папкам ИИ),
**STEP из Компаса — 0** → для Компаса покрытие латентно, пока оператор не даст файлы (В4); 3MF-образцы печати проверяют разбор формата, не детектор. Для каждой мутации в отчёте приёмки — число боевых образцов, на которых дефектная и
исправная версии дают разный результат («0» законно и значит «латентно»).

## 12. Декомпозиция, порядок, оценка, граница GPL/MIT

### 12.1. Фазы

| фаза | владелец | что | приёмка фазы | риск |
|---|---|---|---|---|
| Ф-0 | оператор / Программист | решения В1–В5; #FC-1 колеса `cadquery-ocp-novtk 7.9.3.1.1 win_amd64` **и его зависимости `cadquery-ocp-proxy`** (у Конструктора «подозрительная» находка — именно в ней; счёт «не проверено 99» у него — по 28 колёсам Linux, из них 69 ELF у novtk), а также `numpy` (оракулы) и самого интерпретатора Python (скачать без запуска → статика → стерильный проход; методика `references\foreign-code-gate.md`). Справка: в `Конструктор\.venv` уже стоит Windows-колесо (`win_amd64`, Apache-2.0, OCP 91 МБ + libs 63 МБ) — зона чужая, только читать; своё ставим отдельно после #FC-1. #DL-1: размеры колёс назвать оператору до закачки (`curl -sI` → `Content-Length`) | «подозрительных 0, не проверено 0» или решение оператора по остатку | Р1 |
| Ф-1 | Программист (ступень 2, `gen_code.py`) | C1–C9: геометрия, сетки, импорт STL/OBJ/3MF, JSON, модель | `Cad2Gdml.Tests` — проверки C1–C9 зелёные + мутации | Р6 |
| Ф-2 | Программист | C10–C11: адаптер STEP (после Ф-0) | (а1) на STEP-GS | Р1, Р5 |
| Ф-3 | Программист | C12–C22: кадр, распознавание, огибающая, вещества, роли, размещение, пересечения | (а3), (в4), M1–M6 | Р2, Р3 |
| Ф-4 | Программист | C23–C31: GDML, описание, скетч, SVG/GDI, CLI | (а4)–(а6), (г), `C_det`, `C_xml` | Р4 |
| Ф-5 | Программист | C32–C34: окно | ручной сценарий §10.2 + снимок PNG окна | — |
| Ф-6 | Программист + GEANT4 (письмо) + gdml2in | (б1), (б2), отчёт `F0b-REPORT.md`, стерильные проходы | все пункты §15 | Р9 |

### 12.2. Задачи для `gen_code.py`
Каждая: спека `.md` (вход/выход/проверка; поведение словами, без кода BM) → `python «локальный путь» spec.md out.cs 16000 auto qwen3.8:27b`
→ **шлюз компиляции** (`csc /langversion:7.3` или `dotnet build`; `gen_code.py` синтаксис `.cs` не проверяет — `:65-72`) → проверки + мутация
(#SA-3) → вставка правок `splice_fragment.py --expect-sha`. Порядок = номер.

| № | файл | ≈ строк | вход → выход | проверка (боевая где есть) |
|---|---|---|---|---|
| C1 | `Core/Geometry/Vec3d.cs`, `Affine3.cs`, `Aabb.cs` | 170 | — | обратная матрица, повороты |
| C2 | `Core/Mesh/Mesh.cs`, `MeshWelder.cs` | 160 | треугольники → индексная сетка, сварка 1e-5, вырожденные | куб из 12 граней с дублями вершин |
| C3 | `Core/Mesh/MeshTopology.cs` | 150 | рёбра: открытые/неманифолд/ориентация | M2; файлы PV3 — 0 открытых |
| C4 | `Core/Mesh/MeshProperties.cs`, `Eigen3.cs` | 190 | объём, площадь, центр, тензор инерции, Якоби 3×3 | цилиндр: V, моменты аналитически |
| C5 | `Core/Import/StlImporter.cs` | 150 | binary/ASCII → детали | PV3 (41), NANO5 |
| C6 | `Core/Import/ObjImporter.cs` | 150 | `v/f/o/g/usemtl` → детали | `AtomSpectraPro_full.obj` |
| C7 | `Core/Import/ThreeMfImporter.cs` | 200 | ZIP+XML, `unit`, компоненты, `build` | своя фикстура (боевых 0) |
| C8 | `Core/Json/MiniJson.cs` | 200 | разбор/запись JSON (строки UTF-8, числа Invariant) | `materials.json` GS2020 |
| C9 | `Core/Scene/Part.cs`, `SceneModel.cs`, `Diagnostic.cs`, `Units.cs`, `Importers.cs` | 180 | модель | — |
| C10 | `Step/python/step_export.py` | 190 | STEP → `bodies.json` + `meshes.bin` (§2.3) | STEP-GS: 94 тела, объёмы |
| C11 | `Step/StepProtocol.cs`, `OcpProcessStepReader.cs`, `StepImporter.cs` | 190 | запуск, таймаут, чтение протокола, экземпляры | STEP-GS; код ≠ 0 → `E_STEP_ADAPTER` |
| C12 | `Core/Checks/Bvh.cs` | 180 | дерево AABB, луч–грань (Мёллер–Трумбор) | — |
| C13 | `Core/Recognize/HalfPlaneSection.cs` | 200 | сетка ∩ плоскость → контуры (§8.2) | цилиндр, тор, касание контуров |
| C14 | `Core/Recognize/RevolutionRecognizer.cs` | 200 | §4.1 шаги 1–5 | кристалл STEP-GS/PV3; M6 |
| C15 | `Core/Recognize/ProfileClassifier.cs`, `SolidShapes.cs` | 180 | профиль → tube/polycone/genericPolycone, Папп | профили WATER `:135506-135512` |
| C16 | `Core/Recognize/BoxRecognizer.cs` | 120 | §4.2 | брусок 10³, повернутый брусок |
| C17 | `Core/Recognize/VesselEnvelope.cs` | 200 | §4.3 интервалы по слоям | Маринелли OISN: V_пробы 1161,0 мл (D2 `:262`) |
| C18 | `Core/Scene/FrameBuilder.cs` | 190 | §3.2 | повёрнутый STEP (G1) |
| C19 | `Core/Materials/MaterialLibrary.cs`, `MaterialRules.cs` + `data/materials.default.json` (руками) | 190 | §5 | правила GS2020 на PV3: 41/41 назначено |
| C20 | `Core/Scene/RoleAssigner.cs` | 150 | §6 | имена PV3 + STEP-GS: кристалл ровно один |
| C21 | `Core/Scene/Placement.cs` | 170 | контакт/явное (§3) | Э1: зазор 0, (б2) |
| C22 | `Core/Checks/PointInMesh.cs`, `OverlapChecker.cs` | 190 | §4.4 | M3 |
| C23 | `Core/Gdml/GdmlNames.cs`, `NumberFormat.cs` | 120 | имена ASCII, транслит, числа | кириллица, `0x`, `-0` |
| C24 | `Core/Gdml/GdmlWriter.cs` (define, materials, solids) | 200 | §7.1–7.2 | (а5) |
| C25 | `Core/Gdml/GdmlWriter.Structure.cs` (structure, aux, world, setup) | 160 | §7.1 | структура как WATER |
| C26 | `Core/Report/SceneDescriber.cs` | 190 | §9 | — |
| C27 | `Core/Sketch/SketchBuilder.cs`, `Palette.cs`, `CaptionFormatter.cs` | 200 | §8.1 | (г) |
| C28 | `Core/Sketch/SvgSketchWriter.cs` | 120 | SVG | разбор SVG `XmlReader` |
| C29 | `Sketch.Gdi/GdiSketchRenderer.cs`, `SceneSketchControl.cs` | 200 | GDI+ и элемент управления | PNG Э1 |
| C30 | `Core/Pipeline/Cad2GdmlPipeline.cs`, `ProjectFile.cs` | 180 | конвейер, коды | `C_det` |
| C31 | `Cli/Program.cs`, `CliArgs.cs` | 200 | §10.1 | коды 0/2/3/4/5/6 |
| C32 | `App/MainForm.cs` | 200 | §10.2 | — |
| C33 | `App/PartsGrid.cs`, `PlacementPanel.cs`, `Strings.*.cs` | 200 | — | — |
| C34 | `App/RulesEditor.cs` | 120 | — | — |
| C35 | `Tests/Runner.cs`, `Tests/Fixtures/*.cs` (генераторы цилиндра, Маринелли, бруска, STL/3MF-писатели для фикстур) | 2×200 (две задачи C35a раннер, C35b фикстуры) | — | — |
| C36 | `Tests/Check*.cs` (5–6 файлов) | 6×200 (шесть задач C36a–C36f, по файлу на задачу) | §11 д | — |
| C37 | `tests/oracle/stl_volumes.py`, `compare_volumes.py` | 2×100 | §11 а3–а5 | PV3 |
| C38 | `tools/similarity_gate.py` | 100 | §12.3 | — |
| C39 | `build.ps1` (ASCII) | 80 | запасная сборка `csc` | — |

**Оценка объёма** (по таблице; пересчёт CHECK-DESIGN-3 §SA-10: сумма C1–C39 = 7 970, ядро = 4 690): ядро ≈ 4 690 строк, STEP ≈ 380 (C# 190 + Python 190), GDI ≈ 200, CLI ≈ 200, окно ≈ 520, тесты ≈ 1 600,
оракулы/инструменты ≈ 380 — **итого ≈ 7 970 строк**, 39 задач (≈ 46 вызовов `gen_code.py`). **Сроки** (оценка): Ф-0 — 1 день (+ ожидание
решений); Ф-1 — 2; Ф-2 — 1; Ф-3 — 2,5; Ф-4 — 1,5; Ф-5 — 1; Ф-6 — 1,5 + ожидание GEANT4 и Ф0 gdml2in — **≈ 10–11 рабочих дней**.

### 12.3. Что взять из BecqMoni за образец и граница GPL/MIT
**Образец поведения/данных (только читать, ссылки — в спеках задач):**
- палитра цветов — GS:48-61 (числа как данные `palette.json`, §8.1);
- формат размеров подписи — GS:479-487 (`"G4"`, Invariant, `" × "`, `"⌀"`, `" мм"`); порядок строк — GS:494-546; ужатие шрифта и подложка — GS:555-575;
- масштаб и поля миниатюры — GS:301-303, :319-323; стиль двойной буферизации — GS:67-68;
- слова подписи — `Resources.ru.resx:1494-1513, 1257-1258`;
- вид вещества — `GeometryModel.cs:43-51` (Z → массовая доля, ρ в г/см³), имена и плотности библиотеки — `GeometryMaterialLibrary.cs:188, 219, 227` (как данные);
- оракул подписи — пробу пишем в fork BM по образцу `tools/effmaker/probes/SketchShot.cs` (это GPL-сторона, в MIT-проект не входит).
**Граница (правила для исполнителя):**
1. Ни один файл/фрагмент исходников BM не копируется в `cad2gdml`; спеки `gen_code.py` описывают поведение словами и числами, без вставок кода BM.
2. Данные (цвета, строки, плотности, форматы чисел) допустимы с пометкой провенанса.
3. Адаптер `MaterialSpec → GeometryMaterial` и вызов из BM — на стороне BM (GPL) при встраивании.
4. Шлюз `tools\similarity_gate.py`: окна по 6 нормализованных строк (без пробелов/комментариев) из всех наших `.cs` против всех `.cs` BM
   (`git ls-files '*.cs'` fork) — совпадений 0; прогон перед каждым коммитом. Мутационная поверка шлюза: вставить в наш файл 6 строк из GS → 1 совпадение.
5. Python OCP (Apache-2.0) — только отдельным процессом; в BM при встраивании — тоже процессом (v1) или OCCT LGPL через C++/CLI (v2).

## 13. Риски и вопросы оператору

### 13.1. Реестр рисков

| № | серьёзность | риск | мера |
|---|---|---|---|
| Р1 | **В** | STEP зависит от внешнего Python+OCP; #FC-1 Windows-колеса не делался (у Конструктора — Linux-колесо, остаток «подозрительных 1, не проверено 99», `Конструктор\DECISIONS.md:12`) | Ф-0 до кода адаптера; колесо `novtk`; адаптер изолирован интерфейсом, ядро и сетки работают без него; v2 — C++/CLI |
| Р2 | **В** | ложное распознавание тела вращения → неверная физика у gdml2in | две независимые проверки (вершины на профиле + объём, §4.1 п.5), аналитика STEP; M6; `recognition` в JSON; `--no-recognize` для сравнения |
| Р3 | **С** | совместимость с gdml2in, который ещё не написан (огибающая, словарь ролей, поликон) | структура = WATER (эталон, который gdml2in обязан читать); роли вне словаря — отдельным `auxtype`; (б2) после Ф0 |
| Р4 | **С** | GPL-граница: «по образцу» скетча BM может стать производной | §12.3: спеки без кода BM, шлюз подобия 0 совпадений |
| Р5 | **С** | производительность/размер: STEP-GS при 0,01 мм — неизвестно сколько граней | замер в (а1); ключ `--deflection`; распознанные тела снижают объём |
| Р6 | **С** | единицы STL/OBJ | обязательный `--unit`, подсказка `W_UNIT_SUSPECT`, M5 |
| Р7 | **С** | STEP из Компаса (кириллица `\X2\…\X0\`, AP203/AP242) — 0 образцов | В4; транслит имён §7.4; исходное имя в `aux` |
| Р8 | **С** | один боевой STEP — латентные дефекты (#SA-3a) | В4: ещё 2–3 файла (Маринелли, Компас) |
| Р9 | **С** | приёмка Geant4 зависит от контура GEANT4 (§12), раньше он молчал (D2 `:29`) | своё `E_OVERLAP` + (а5) как запасной путь; письмо в начале Ф-6 |
| Р10 | **Н** | C# 7.3 и net48 ограничивают язык | осознанно (встраивание); netstandard2.0 — вторым шагом |
| Р11 | **Н** | `gen_code.py` не проверяет синтаксис `.cs` (`:65-72`) | шлюз компиляции после каждой задачи |
| Р12 | **С** | сборка SDK-проектов net48 без сети не проверена (restore) | запасной `build.ps1` прямым `csc` (как BM, `build_all.ps1:859`) |

### 13.2. Вопросы оператору (решение до кода; форма AskUserQuestion — у оркестратора)
- **В1. STEP-адаптер v1:** (а) Python + OCP отдельным процессом, v2 — C++/CLI под 4.8 для встраивания (Рекомендую); (б) .NET 8-процесс с
  OCCSharp/OcctNet; (в) сразу C++/CLI под 4.8 (дольше на 2–4 дня, сборка OCCT).
- **В2. Репозиторий:** (а) отдельный `cad2gdml` (MIT) в `«локальный путь»` (выбрано 29.10), на GitHub — по «да» (Рекомендую); (б) `tools/cad2gdml/` в fork BM со своим `LICENSE`.
- **В3. Платформа ядра:** (а) net48 + C# 7.3 сейчас, netstandard2.0 — когда понадобится (Рекомендую: ядро C# собирается без загрузок; колесо OCP для STEP-адаптера — отдельная закачка Ф-0); (б) сразу net48 + netstandard2.0
  (пакет `NETStandard.Library 2.0.3` → загрузка и #FC-1).
- **В4. Боевые образцы:** прошу выгрузить из Fusion/Компаса: STEP стакана Маринелли (лучше OISN-размеров), STEP любого прибора из Компаса,
  STL и 3MF кристалла GS2020 (высокое уточнение). Без них 3MF и Компас — 0 образцов (латентно).
- **В5. Тесты:** (а) свой мини-раннер без пакетов (Рекомендую); (б) NUnit/xUnit после #FC-1.

## 14. Вне рамок v1
Blender/glTF/FBX/IGES/PLY; чтение GDML и сворачивание в `.in` (gdml2in); ремонт сеток (заплатки дыр, самопересечения); тела вращения с осью
не по z как примитивы (будут tessellated); `<rotation>` в `physvol`; 3D-вид; булевы тела GDML; встраивание в BM (отдельное предложение Amber
после v1); C++/CLI-адаптер (v2); публикация (push/релиз) без «да».

## 15. Сводный чек-лист приёмки (для релиза v1)
- [ ] Ф-0: решения В1–В5 записаны; #FC-1 колеса OCP — отчёт с «подозрительных 0, не проверено 0» или решение оператора.
- [ ] `similarity_gate.py`: 0 совпадений с `.cs` BM; поверка шлюза: 1 совпадение на мутанте.
- [ ] Сборка: `dotnet build` (или `build.ps1`) — 0 ошибок, все цели net48, LangVersion 7.3.
- [ ] (а1) STEP-GS: K/94 тел с |ΔV|/V ≤ 1e-3 (K и список не прошедших — в отчёте); (а2) сходимость ≥ 95 %; (а6) экземпляры = `bodies[]`.
- [ ] (а3) PV3: сопоставлено K/41, медиана ≤ 2e-3, наибольшее ≤ 1e-2; (а4) V3 = PV3 до 1e-6; (а5) наш GDML = наш JSON до 1e-6.
- [ ] (б1) GEANT4: `geomtest_only.mac` — 0 перекрытий (или записан запасной путь); (б2) gdml2in — код 0, роли `by: aux`.
- [ ] (в1)–(в2) пороги по телу выполнены; (в3) справочно напечатано; (в4) кристалл → `tube`, ΔR ≤ 0,01 мм.
- [ ] (г) Э1, Э2: строки 1–2 подписи побайтно = BM; габарит кристалла на PNG ± 1 пиксель.
- [ ] (д) M1–M6 — по одной красной; G1 — ноль красных; мутации кода — по одной красной; числа боевых образцов на каждую мутацию в отчёте.
- [ ] `C_det`: два прогона — одинаковый sha256 GDML; `C_xml` под ru-RU — зелёный.
- [ ] Стерильные проходы (#SA-11): `CLAIMS.md` → отчёт проверяющего «непроверенных утверждений: 0».

**Источники (веб, 29.10.2026):** [OCCSharp на NuGet](https://www.nuget.org/packages/OCCSharp/); [Inlay.CAD.OCCT](https://feed.nuget.org/packages/Inlay.CAD.OCCT);
[OcctNet.Wrapper](https://www.nuget.org/packages/OcctNet.Wrapper); [DotNetCore.OCCTProxy](https://www-0.nuget.org/packages/DotNetCore.OCCTProxy);
[Open Cascade — Wrappers](https://occt3d.com/components/wrappers/) (редирект с opencascade.com); [пример OCCT samples/CSharp (зеркало)](https://git.rbts.co/OpenCascade/occt/src/branch/IR/samples/CSharp);
[cadquery-ocp на PyPI](https://pypi.org/project/cadquery-ocp/7.7.2.1); [ocp-build-system](https://github.com/CadQuery/ocp-build-system);
[cadquery-ocp на Cloudsmith](https://cloudsmith.com/navigator/pypi/cadquery-ocp); [3MF Core Specification (вики Slic3r)](https://github.com/slic3r/Slic3r/wiki/3MF-Document-Core-Specification).

Непроверенное (стоит на памяти, вторичном источнике или не прогнано):
1) существование и лицензия IxMilia.Step — поиск страницу не нашёл;
2) значения и умолчание атрибута `unit` 3MF — по коду OrcaSlicer и пересказу спецификации 1.4.0, нормативный текст не открыт;
3) число размещённых экземпляров тел в STEP-GS (632 `NAUO` — не равно числу экземпляров тел);
4) что OCCT по умолчанию переводит STEP в мм (`xstep.cascade.unit`);
5) порядок `auxiliary` в `<volume>` по схеме GDML (после `physvol`);
6) что парсер GDML Geant4 отрезает имена по подстроке `0x`;
7) число граней STEP-GS при прогибе 0,01 мм и время адаптера;
8) смысл «До детектора» и диаметра у подписи цилиндра BM (GS:533-538 — поля названы, определения не прослежены);
9) язык по умолчанию старого csproj net48 = C# 7.3 (по документации MSBuild, не проверено на этой машине);
10) полнота компонента C++/CLI в BuildTools (есть только `include\msclr`) — для v2;
11) сборка SDK-проектов net48 без доступа к сети (restore) на этой машине;
12) список тулкитов OCCSharp 7.8 (`TKDESTEP` есть по одной выдаче, нет по другой);
13) лицензия Apache-2.0 у `cadquery-ocp` 7.9.3.1.1 (видел листинг для 7.8.1.1) и состав зависимостей колеса `novtk`;
14) пороги (а2) 95 % и (а3) 2e-3 / 1e-2 — назначены, не измерены;
15) коммерческий статус официального C# Wrapper OCCT — по выдаче поиска (листинг dev.opencascade.org), страница компонента цены/лицензии не показала.

## Решения оператора 29.10 ~03:10 (AskUserQuestion, по вопросам В1–В5)
- В1 STEP v1 — **Python + OCP отдельным процессом** (файловый протокол §2.3); v2 — свой C++/CLI-прокси под net48 для встраивания в BM.
  #FC-1 на Windows-колесо OCP — до установки.
- В2 — **свой репозиторий `VibeEngineering-LLC/cad2gdml`** (MIT, без кода BM; приватный до слова оператора; локально `«локальный путь»`).
- В3 — **net48 сейчас**, netstandard2.0 вторым шагом.
- В4/В5 — **свой мини-раннер тестов без пакетов**; боевые образцы 3MF и STEP из Компаса оператор пришлёт позже; v1 принимается на STEP GS2020
  (Fusion) + STL контура GEANT4 (`parts_stl_v3`).
- Порядок: стерильная проверка этого проекта → чтение оператором → код (задачи `gen_code.py`).

непроверенных утверждений: 15 (перечень — пункты 1–15 выше)
