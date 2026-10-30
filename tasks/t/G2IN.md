Write ONE Python 3 file `gdml2in.py` (stdlib only, no numpy). Output ONLY the code in one ```python block.

PURPOSE: convert a GDML scene (written by cad2gdml) into a BecqMoni/LSRM text template `.in` for a scintillator detector with a box-shaped crystal and a POINT source.

CLI: `python gdml2in.py <scene.gdml> <out.in> [--crystal NAME] [--distance-cm D] [--dump]`
- `--crystal`: logical-volume name of the crystal. Default: the volume whose material name contains one of LaBr, NaI, CsI, CeBr, SrI, BGO, GAGG, LYSO, YAP (case-insensitive); if none or >1 candidates -> exit 2 with message listing candidates.
- `--distance-cm`: source distance from the crystal front face, default 10.
- `--dump`: also print the parsed summary to stdout.
- sys.stdout.reconfigure(encoding='utf-8'); read the GDML with encoding='utf-8'; write the `.in` as UTF-8 text with ASCII-only comments, LF newlines.

PARSING (use xml.etree.ElementTree; GDML has no namespace you need, tags: define/position, materials/element/material, solids/box/tessellated/triangular, structure/volume/physvol, setup):
- `<define>`: `<position name=.. x y z unit="mm"/>` -> dict name->(x,y,z) in mm (unit mm/cm/m; default mm).
- `<element name Z><atom value/>`: Z by element name.
- `<material name>`: density from `<D value unit="g/cm3"/>`, list of `<fraction n ref>` (mass fractions; ref is element name). Materials whose name starts with `G4_` are NOT in `<materials>`; use the built-in table NIST below.
- `<box name x y z>` (mm full lengths); `<tessellated>` with `<triangular vertex1 vertex2 vertex3>` referencing define positions -> bounding box (min/max per axis) of used vertices.
- `<volume name>`: `<materialref ref>` and `<solidref ref>` (solid name is `<volume>_s` in our files, but follow solidref). `<physvol>` inside the World volume: `<volumeref ref>` and `<positionref ref>` (offset in mm; missing -> 0).
- Per volume: bbox = solid bbox (box: +-x/2,+-y/2,+-z/2; tessellated: vertices as is, ABSOLUTE) shifted by the physvol position. The world volume (the one named in `<setup><world ref=..>`) is excluded.

BUILT-IN NIST TABLE (density g/cm3, list of (Z, mass fraction)):
G4_Al: 2.699, [(13,1)]; G4_Cu: 8.96, [(29,1)]; G4_Si: 2.33, [(14,1)]; G4_Fe: 7.874, [(26,1)]; G4_Pb: 11.35, [(82,1)];
G4_AIR: 0.00120479, [(6,0.000124),(7,0.755268),(8,0.231781),(18,0.012827)];
G4_TEFLON: 2.25, [(6,0.240183),(9,0.759817)];
G4_POLYCARBONATE: 1.2, [(1,0.055491),(6,0.755751),(8,0.188758)];
G4_WATER: 1.0, [(1,0.111898),(8,0.888102)];
G4_SODIUM_IODIDE: 3.667, [(11,0.153373),(53,0.846627)];
G4_CESIUM_IODIDE: 4.51, [(53,0.488451),(55,0.511549)].
Unknown `G4_*` material for a volume that is used -> exit 3 naming it.

LAYER LOGIC (all values in cm in the output, mm/10):
- Crystal bbox C (from crystal volume). Crystal box size X,Y,Z = bbox extents; "long axis" = axis with the largest extent; the front face of the crystal (the one facing the source) = the end of the long axis at its MAX coordinate side if the crystal centre is below the centre of the union bbox of all non-world volumes on that axis... SIMPLER, implement exactly this: front face is at the long-axis MAX end of the crystal. Print a note `front=+<axis>` in a comment line.
- Cladding (housing) = volumes of material containing `Al`-only composition (single element Z=13) whose bbox contains the crystal bbox center point, choose the one with the largest bbox volume; side cladding thickness = min over the two transverse axes of (housing_half_extent - crystal_half_extent) using bbox min/max of the housing relative to crystal centre, front cladding thickness = housing_max - crystal_max along the long axis. If no such volume -> thickness 0.
- Reflector = volume with material G4_TEFLON (or composition C+F only) whose bbox contains the crystal centre; side thickness and front thickness computed the same way against the crystal bbox (if cladding found, subtract nothing: thicknesses are measured from the crystal to the inner volume's own bbox). Negative -> 0.
- DS_CrystalDiameter = sqrt(4*X*Y/pi) of the two transverse extents (equal-area cylinder) in cm; DS_CrystalHeight = long-axis extent in cm; DS_CrystalBoxX/Y/Z = transverse1, transverse2, long extents in cm.
- DS_CrystalFrontReflectorThickness, DS_CrystalSideReflectorThickness, DS_CrystalFrontCladdingThickness, DS_CrystalSideCladdingThickness as computed; DS_DetectorMountingThickness = 0.2 cm constant.

OUTPUT `.in` FORMAT (key = value lines; this exact text skeleton, fill the `<..>` fields; numbers with up to 6 significant digits via format(v,'.6g'), units ` cm`):
```
//DETECTOR PARAMETERS BLOCK:
//Detector types: COAXIAL, SCINTILLATOR

DetectorType = SCINTILLATOR

// Scintillator detector
DS_CrystalDiameter = <d> cm
DS_CrystalHeight = <h> cm
DS_CrystalFrontReflectorThickness = <v> cm
DS_CrystalSideReflectorThickness = <v> cm
DS_CrystalFrontCladdingThickness = <v> cm
DS_CrystalSideCladdingThickness = <v> cm
DS_DetectorMountingThickness = 0.2 cm

//SOURCE PARAMETERS BLOCK:
//Source types: POINT, CYLINDER, MARINELLI

SourceType = POINT

//Point source
pdistance = <D> cm
```
followed by the material blocks, each of the form (N = number of elements; index i from 0; fractions = mass fractions normalised to sum 1, 6 significant digits):
```
// <Role>
DS_n<Part>Elements = N
DS_Ro<Part> = <density>
DS_Z<Part>[i] = <Z>
DS_Fractions<Part>[i] = <w>
DS_FractionType<Part> = MASS
M_DS_<Role>.MName = <material name>
M_DS_<Role>.Nmaterials = 1
M_DS_<Role>.Name[0] = <material name>
M_DS_<Role>.MatRelWeight[0] = 1
```
with exactly these (Part, Role) pairs: (Crystal, Crystal), (CrystalCladding, Crystal_Cladding), (CrystalReflector, Reflector). Note the special spelling used by the reader: counts are `DS_nCrystalElements`, `DS_nCrystalCladdingElements`, `DS_nCrystalReflectorElements`; fraction-type keys are `DS_FractionTypeCrystal`, `DS_FractionTypeCrystalCladding`, `DS_FractionTypeReflector` (NOT CrystalReflector). If a layer has thickness 0 / not found, still emit the block using Aluminium (Z13, 2.7) for cladding and PTFE (C 0.240183, F 0.759817, 2.25) for reflector.
Finally, the box extension (always emitted, after the blocks):
```
DS_CrystalBoxX = <x> cm
DS_CrystalBoxY = <y> cm
DS_CrystalBoxZ = <z> cm
```
Fractions of a GDML material are used as given then normalised; a `<fraction>` ref to an unknown element -> exit 3.

ERRORS: wrong arguments -> exit 2; unreadable/invalid GDML -> exit 3 with a one-line message; success -> exit 0 and a single stdout line `OK crystal=<name> box=<x>x<y>x<z>mm refl=<side>/<front>mm clad=<side>/<front>mm`.

Keep it compact (about 200 lines), plain functions, no classes needed, type hints optional.
