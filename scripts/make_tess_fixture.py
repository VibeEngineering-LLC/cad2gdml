#!/usr/bin/env python3
# Вырезка боевого образца из рабочего файла -> C#-константа для тестов (FixturesTess.cs).
# Запуск: python scripts/make_tess_fixture.py <файл.gdml> <выход.cs> [число_треугольников]
import re, sys, io
src, out = sys.argv[1], sys.argv[2]
n = int(sys.argv[3]) if len(sys.argv) > 3 else 24
t = io.open(src, encoding='utf-8').read()
mats = re.search(r'  <materials>.*?</materials>\n', t, re.S).group(0)
sol = re.search(r'<tessellated name="([^"]+)"[^>]*>', t)
tris = re.findall(r'      <triangular [^>]*/>', t[sol.end():])[:n]
names = sorted({m for x in tris for m in re.findall(r'vertex\d="([^"]+)"', x)}, key=lambda s: int(s.rsplit('v', 1)[1]))
pos = ''.join(re.search(r'    <position name="%s" [^>]*/>\n' % re.escape(v), t).group(0) for v in names)
body = ('<?xml version="1.0" encoding="UTF-8"?>\n<gdml>\n  <define>\n' + pos + '  </define>\n' + mats + '  <solids>\n    ' + '<tessellated name="Body_solid" aunit="deg" lunit="mm">' + '\n'
        + '\n'.join(tris) + '\n    </tessellated>\n    <box name="World_solid" x="100" y="100" z="100" lunit="mm"/>\n  </solids>\n  <structure>\n'
        + '    <volume name="Can"><materialref ref="PETG"/><solidref ref="' + 'Body_solid' + '"/></volume>\n'
        + '    <volume name="World"><materialref ref="AIR"/><solidref ref="World_solid"/>\n      <physvol name="Can_pv"><volumeref ref="Can"/></physvol>\n    </volume>\n'
        + '  </structure>\n  <setup name="Default" version="1.0"><world ref="World"/></setup>\n</gdml>\n')
cs = 'namespace Cad2Gdml.Tests\n{\n    // Сгенерировано scripts/make_tess_fixture.py из рабочего файла (первые %d треугольников, как в файле)\n    internal static class FixturesTess\n    {\n        internal const int RealTriangles = %d;\n        internal const string RealCut = @"%s";\n    }\n}\n' % (n, len(tris), body.replace('"', '""'))
io.open(out, 'w', encoding='utf-8', newline='\n').write(cs)
print(len(tris), len(names), len(cs))
