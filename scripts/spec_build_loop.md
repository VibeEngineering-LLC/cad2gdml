Write ONE complete Python 3 script (stdlib only), no explanations, one fenced python block.

Name: build_loop.py. Usage: `python build_loop.py <task_file.md> [--max-fix 3]`, run from repository root (cwd = repo root).

Purpose: generate one C# source file with a local LLM, compile it, and feed compiler errors back until it builds.

Task file format (UTF-8): header lines at top, then a blank line, then free-text brief.
  TARGET: src/Cad2Gdml.Core/Mesh/Mesh.cs        (one output file, path relative to repo root)
  SECTIONS: 4.1, 7.1                            (optional, comma list of heading numbers in docs/DESIGN.md)
  DEPENDS: src/Cad2Gdml.Core/Geometry/Vec3d.cs  (optional, comma list: files whose FULL text is given to the model)

Steps:
1. Parse header and brief. Project dir = first two path parts of TARGET joined (e.g. src/Cad2Gdml.Core); find the .csproj inside it.
2. Build the prompt text (UTF-8) and write it to `work/<task-stem>.prompt.md`:
   a) COMMON RULES block (constant string in the script): C# language version 7.3, target .NET Framework 4.8, no external packages;
      file must start with the required `using` lines; namespace as given by the brief; use CultureInfo.InvariantCulture for every
      number-to-text and text-to-number conversion; no `?.` chains on value types problems, no `is not`, no records, no `switch` expressions,
      no `using var`, no nullable annotations, no default interface methods, no `init`; code comments in Russian, short; classes public sealed
      or static where fitting; never invent members of other files: use ONLY the API digest and the DEPENDS files given below; answer is the
      complete target file in one ```csharp fence, nothing else.
   b) For each section number in SECTIONS: extract from docs/DESIGN.md the text of the heading line that starts with `#`, optional spaces,
      then the number followed by a dot or space (e.g. "### 4.1. Тело вращения") up to (not including) the next heading of the same or
      higher level (fewer or equal number of leading # characters). Include it under "DESIGN EXCERPT".
   c) API DIGEST: for every *.cs file under src/ (all projects) except TARGET and except files in DEPENDS, include its relative path and
      every line whose stripped text starts with `public ` or `internal ` and does not end with `;` after a `=`  (declarations only; keep
      lines containing `class `, `struct `, `enum `, `interface `, and member signatures), trimmed to 160 chars each.
   d) DEPENDS files: full text under their path.
   e) The brief.
3. Call: `python «локальный путь» work/<stem>.prompt.md <TARGET> 16000 32768 qwen3.8:27b` via
   subprocess, `PYTHONIOENCODING=utf-8`, capture output; create TARGET's parent dir first. If gen_code exits non-zero: print its stderr
   tail, exit 10. (gen_code is expected to extract the fenced code into TARGET; after it returns, if TARGET text still starts with a
   markdown fence line, strip the first fence line and the last fence line yourself.)
4. Compile: `dotnet build <csproj> -c Release -v q -nologo -o work/bin/<projname>` via subprocess with UTF-8 decoding (errors='replace').
   Collect lines matching ` error CS\d+` (deduplicate, keep at most 25) that mention TARGET's file name.  Errors in OTHER files do not
   count as failure of this task but are printed as WARNING lines.
5. If there are errors in TARGET and fix attempts < max-fix: write `work/<stem>.fix<N>.prompt.md` = COMMON RULES + current TARGET full text
   + the error lines + the API digest + DEPENDS + instruction "Fix only the reported errors, return the complete file", regenerate to
   TARGET with gen_code (same command), compile again.
6. Print one final line: `RESULT <stem> OK attempts=<n>` or `RESULT <stem> FAIL attempts=<n> errors=<k>`; exit 0 on OK, 1 on FAIL.
   Append the same line plus a timestamp to `work/build-loop.log`.
Print progress lines (task stem, attempt number, seconds per gen_code call). Handle missing docs/DESIGN.md (no excerpt, warn).
Do not delete or edit any file other than TARGET and files under work/.
