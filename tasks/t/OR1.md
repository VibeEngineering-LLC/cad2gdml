TARGET: src/Cad2Gdml.Core/Scene/FrameBuilder.cs
DEPENDS: src/Cad2Gdml.Core/Scene/FrameBuilder.cs

FIX the existing file (return the COMPLETE file, keep every other behaviour). Extend `FrameOptions.FrontSign` from {"auto","+z","-z"} to {"auto","+x","-x","+y","-y","+z","-z"}: the letter names the FILE axis along which the crystal axis is taken, the sign says which end is the front face.

Add a private static helper `static bool AxisFromSign(string sign, out Vec3d axis, out char letter)`: trims, lower-cases; for "+x"/"-x" → UnitX, 'x'; "+y"/"-y" → UnitY, 'y'; "+z"/"-z" → (0,0,1), 'z'; anything else → false, axis Zero, letter '\0'.

Changes: (a) `TryDetermineAxis`: at the very beginning, if `AxisFromSign` succeeds and the letter is 'x' or 'y', set `axis` to that unit vector and return true (explicit x/y forces the axis; "+z"/"-z" keep the existing behaviour: inertia-based axis, z used only where the current code already uses it). (b) `ResolveCubeAxis` and `TryAxisFromBBox`: where the code currently tests `sign == "+z" || sign == "-z"` and sets axis = (0,0,1), use `AxisFromSign` instead so that ±x/±y give their own axis. (c) `TryDetermineFront`: for an explicit sign, take the component of `d = (facePlus - faceMinus).Normalized()` named by the letter (`d.X`, `d.Y` or `d.Z`); if `|comp| >= 1e-9` then `plusIsFront = comp > 0` and `front = (plusIsFront == wantPlus) ? facePlus : faceMinus` where `wantPlus` = sign starts with '+'; else fall back to the dominant of the two other components exactly as the current else-branch does for z. Keep it compact; think briefly.
