Write ONE complete Python 3 script (stdlib only), no explanations, one fenced python block.

Name: fuzz.py. Usage: `python fuzz.py <path_to_cad2gdml.exe> <work_dir> [per_seed=300] [seed=1]`.
Purpose: seed-based mutational fuzzing of the input readers of a console converter (method of NIST IR 8397 / Manes et al. fuzzing survey: valid seed corpus, byte-level mutation, crash oracle).

1. Create these valid seed files in work_dir/seeds (all generated in code, deterministic):
   - cyl_bin.stl: binary STL of a closed cylinder (radius 25.4, height 50.8, 32 segments, float32, 80-byte header, count, 50-byte records);
   - cyl_ascii.stl: the same cylinder as ASCII STL (solid/facet normal/outer loop/vertex lines);
   - cube.obj: an OBJ cube (`o cube`, 8 `v` lines, 12 `f` triangles, 1-based);
   - one.3mf: a ZIP (zipfile) with `[Content_Types].xml`, `_rels/.rels` (relationship Target="/3D/3dmodel.model") and `3D/3dmodel.model` (XML namespace http://schemas.microsoft.com/3dmanufacturing/core/2015/02, unit="millimeter", one object id=1 with a mesh of the cube: 8 vertices, 12 triangles, and `<build><item objectid="1"/></build>`).
2. For each seed make `per_seed` mutants with random.Random(seed) choosing one of: flip random bytes (1..8), overwrite a random byte run with 0x00 / 0xFF, delete a random slice, insert random bytes, truncate at a random offset, duplicate a random slice, replace a number token in text seeds by one of `NaN`, `inf`, `-inf`, `1e999`, `-0`, `99999999999`, `0,5`, `0x10`, empty string; for the 3MF the mutation is applied to the decompressed model XML (then re-zipped) in half of the cases and to raw zip bytes in the other half.
3. Run the exe as `[exe, mutant_path, "--unit", "mm", "--front", "+z", "--allow-default-material", "--out", out_dir]` (for .3mf also without --unit) with subprocess.run, timeout 30 s, capture_output, no shell. Oracle: exit code must be in {0, 2, 3, 4, 5}. A mutant is a FINDING if the exit code is 9 or anything else, if the process times out, or if the process crashes (negative or >100 code).
4. For each finding copy the mutant to work_dir/findings/<seedname>_<index>.<ext> and record exit code, the last 3 stdout lines (decoded utf-8 errors=replace). Keep at most 40 findings of each distinct (seed, exit code, first stdout error id line) combination class: dedupe by the key (seed name, exit code, the stdout line that contains "E_").
5. Print a summary: total runs, counts per exit code, number of findings, the deduplicated finding classes with one example file each. Write the same summary to work_dir/summary.txt (utf-8). Exit code 0 always.
