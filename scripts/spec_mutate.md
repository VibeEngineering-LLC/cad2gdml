Write ONE complete Python 3 script (stdlib only), no explanations, one fenced python block.

Name: mutate.py. Usage: `python scripts/mutate.py <relative file> <old1> <new1> [<old2> <new2> ...]`, any cwd (repo root = parent of the scripts folder this file lives in).

Purpose: mutation check of a C# source file against the test runner.
For every (old, new) pair, in order:
1. Read the target file once at start (UTF-8, keep the original text). If `old` occurs not exactly once in the text: print `SKIP (вхождений N): <old>` and continue.
2. Write the file with that single replacement (UTF-8, newline='' so line endings are kept).
3. Build: `dotnet build src/Cad2Gdml.Tests -c Release -o work/bin/tests -v q -nologo` (cwd = repo root, capture, decode UTF-8 errors='replace'). If return code != 0: print `MUTANT [old -> new]: BUILD-ERROR` plus the last 300 chars of stdout, continue.
4. Run `work/bin/tests/Cad2GdmlTests.exe` (cwd = repo root), decode UTF-8 errors='replace'. Collect lines starting with `FAIL` and the line starting with `TESTS`.
5. Print `MUTANT [old -> new]: красных=<count of FAIL lines>`, then each FAIL line indented by two spaces, then the TESTS line indented.
In a `finally` block ALWAYS restore the original file text, then print `ВОССТАНОВЛЕН <relative file>`. Call `sys.stdout.reconfigure(encoding='utf-8')` first. Exit code 0.
