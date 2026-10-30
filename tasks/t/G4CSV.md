Write ONE Python 3 file `g4csv2lsrm.py` (stdlib only). Output ONLY the code in one ```python block.

PURPOSE: convert a Geant4 efficiency CSV into the LSRM/BecqMoni efficiency-curve TSV.

CLI: `python g4csv2lsrm.py <in.csv> <out.txt>`. Wrong arg count -> exit 2. Unreadable file / bad number / fewer than 2 usable points -> print `ERR <message>` and exit 3. Success -> write file, print one line `OK points=<N>`, exit 0.
- sys.stdout.reconfigure(encoding='utf-8'); read input encoding='utf-8'; write output UTF-8, LF newlines (newline='\n').

INPUT: lines starting with `#` and blank lines are skipped; the header line starting with `E_keV` (case-insensitive) is skipped; data lines are `E_keV,eps_fep,sig_fep` (comma separated, extra columns ignored).

RULES per data line: e,eps,sig = floats. Skip the line if eps <= 0. unc = sig/eps*100. Skip the line if unc > 100. Keep the rest in input order.

OUTPUT: first line exactly `Energy, keV<TAB>Efficiency<TAB>Uncertainty, %`, then one line per kept point: `<e formatted .6g><TAB><eps formatted .6e><TAB><unc formatted .4g>`.

Provide a function `convert(text) -> (str, int)` raising ValueError on <2 points, used by main. Keep it compact (about 40 lines).
