#!/usr/bin/env python3
import sys, os, re, subprocess, time, datetime

def main():
    args = sys.argv[1:]
    task_file = None
    max_fix = 3
    i = 0
    while i < len(args):
        if args[i] == '--max-fix':
            i += 1
            max_fix = int(args[i])
        elif task_file is None:
            task_file = args[i]
        i += 1
    if task_file is None:
        print("Usage: python build_loop.py <task_file.md> [--max-fix 3]")
        sys.exit(2)

    with open(task_file, 'r', encoding='utf-8') as f:
        content = f.read()

    lines = content.split('\n')
    headers = {'SECTIONS': [], 'DEPENDS': []}
    brief_start = None
    for idx, line in enumerate(lines):
        if line.strip() == '':
            brief_start = idx
            break
        if line.startswith('TARGET:'):
            headers['TARGET'] = line[len('TARGET:'):].strip()
        elif line.startswith('SECTIONS:'):
            val = line[len('SECTIONS:'):].strip()
            headers['SECTIONS'] = [s.strip() for s in val.split(',') if s.strip()]
        elif line.startswith('DEPENDS:'):
            val = line[len('DEPENDS:'):].strip()
            headers['DEPENDS'] = [d.strip() for d in val.split(',') if d.strip()]

    if 'TARGET' not in headers:
        print("ERROR: No TARGET in task file")
        sys.exit(2)

    target = headers['TARGET'].replace('\\', '/')
    sections = headers['SECTIONS']
    depends = headers['DEPENDS']
    stem = os.path.splitext(os.path.basename(task_file))[0]

    brief = '\n'.join(lines[brief_start + 1:]).strip() if brief_start is not None else ''

    parts = target.split('/')
    proj_dir = '/'.join(parts[:2])
    csproj = None
    if os.path.isdir(proj_dir):
        for fn in sorted(os.listdir(proj_dir)):
            if fn.endswith('.csproj'):
                csproj = os.path.join(proj_dir, fn)
                break
    if csproj is None:
        print(f"ERROR: No .csproj found in {proj_dir}")
        sys.exit(2)
    projname = os.path.splitext(os.path.basename(csproj))[0]

    os.makedirs('work', exist_ok=True)

    COMMON_RULES = (
        "COMMON RULES\n"
        "- C# language version 7.3, target .NET Framework 4.8, no external packages.\n"
        "- File must start with the required `using` lines.\n"
        "- Namespace as given by the brief.\n"
        "- Use CultureInfo.InvariantCulture for every number-to-text and text-to-number conversion.\n"
        "- No `?.` chains on value types.\n"
        "- No `is not`, no records, no `switch` expressions, no `using var`, no nullable annotations, no default interface methods, no `init`.\n"
        "- Code comments in Russian, short.\n"
        "- Classes public sealed or static where fitting.\n"
        "- Never invent members of other files: use ONLY the API digest and the DEPENDS files given below.\n"
        "- Answer is the complete target file in one ```csharp fence, nothing else.\n"
    )

    # --- b) Design excerpt ---
    design_excerpt = ""
    design_path = 'docs/DESIGN.md'
    if sections and not os.path.isfile(design_path):
        print(f"WARNING: {design_path} not found, skipping design excerpt")
    elif sections and os.path.isfile(design_path):
        with open(design_path, 'r', encoding='utf-8') as f:
            design_lines = f.read().split('\n')
        for sec in sections:
            pattern = re.compile(r'^(#+)\s*' + re.escape(sec) + r'[\.\s]')
            found = False
            for li, dl in enumerate(design_lines):
                m = pattern.match(dl)
                if m:
                    level = len(m.group(1))
                    block = [dl]
                    for lj in range(li + 1, len(design_lines)):
                        nm = re.match(r'^(#+)\s', design_lines[lj])
                        if nm and len(nm.group(1)) <= level:
                            break
                        block.append(design_lines[lj])
                    design_excerpt += f"### Section {sec}\n" + '\n'.join(block) + "\n\n"
                    found = True
                    break
            if not found:
                print(f"WARNING: Section {sec} not found in {design_path}")

    # --- c) API digest ---
    api_digest_entries = []
    target_norm = os.path.normpath(target)
    depends_norm_set = set(os.path.normpath(d.replace('\\', '/')) for d in depends)
    src_root = 'src'
    if os.path.isdir(src_root):
        for root, dirs, files in os.walk(src_root):
            for fn in sorted(files):
                if not fn.endswith('.cs'):
                    continue
                full = os.path.normpath(os.path.join(root, fn))
                if full == target_norm or full in depends_norm_set:
                    continue
                try:
                    with open(full, 'r', encoding='utf-8', errors='replace') as f2:
                        for line in f2:
                            s = line.strip()
                            if s.startswith('public ') or s.startswith('internal '):
                                api_digest_entries.append(s[:160])
                except (IOError, OSError):
                    pass
    api_digest = '\n'.join(api_digest_entries) if api_digest_entries else '(none)'

    # --- d) DEPENDS ---
    depends_text = ""
    for dp in depends:
        dp_norm = dp.replace('\\', '/')
        try:
            with open(dp_norm, 'r', encoding='utf-8') as f:
                depends_text += f"--- {dp_norm} ---\n" + f.read() + "\n\n"
        except (IOError, OSError) as e:
            print(f"WARNING: Cannot read DEPENDS file {dp_norm}: {e}")

    # --- Build and write initial prompt ---
    prompt_parts = [COMMON_RULES, ""]
    if design_excerpt:
        prompt_parts.append("DESIGN EXCERPT\n" + design_excerpt + "\n")
    prompt_parts.append("API DIGEST\n" + api_digest + "\n")
    if depends_text:
        prompt_parts.append("DEPENDS\n" + depends_text + "\n")
    prompt_parts.append("BRIEF\n" + brief + "\n")
    full_prompt = '\n'.join(prompt_parts)

    prompt_path = f'work/{stem}.prompt.md'
    with open(prompt_path, 'w', encoding='utf-8') as f:
        f.write(full_prompt)
    print(f"[{stem}] Prompt written to {prompt_path}")

    # --- gen_code helper ---
    def call_gen_code(prompt_md, target_path):
        d = os.path.dirname(target_path)
        if d:
            os.makedirs(d, exist_ok=True)
        env = os.environ.copy()
        env['PYTHONIOENCODING'] = 'utf-8'
        cmd = [
            'python',
            r'«локальный путь»',
            prompt_md, target_path, '30000', '49152', 'qwen3.8:27b'
        ]
        t0 = time.time()
        result = subprocess.run(cmd, capture_output=True, env=env)
        elapsed = time.time() - t0
        if result.returncode != 0:
            stderr = result.stderr.decode('utf-8', errors='replace') if isinstance(result.stderr, bytes) else (result.stderr or '')
            tail = stderr[-500:] if stderr else '(no stderr)'
            print(f"ERROR: gen_code failed (rc={result.returncode}) after {elapsed:.1f}s")
            print(tail)
            sys.exit(10)
        print(f"[{stem}] gen_code done in {elapsed:.1f}s")
        try:
            with open(target_path, 'r', encoding='utf-8') as tf:
                ttext = tf.read()
            tlines = ttext.split('\n')
            if tlines and tlines[0].strip().startswith('```'):
                tlines = tlines[1:]
                while tlines and tlines[-1].strip() == '':
                    tlines.pop()
                if tlines and tlines[-1].strip().startswith('```'):
                    tlines.pop()
                while tlines and tlines[-1].strip() == '':
                    tlines.pop()
                with open(target_path, 'w', encoding='utf-8') as tf:
                    tf.write('\n'.join(tlines) + '\n')
        except (IOError, OSError):
            pass

    # --- compile helper ---
    def compile_project():
        env = os.environ.copy()
        env['PYTHONIOENCODING'] = 'utf-8'
        out_dir = os.path.join('work', 'bin', projname)
        cmd = ['dotnet', 'build', csproj, '-c', 'Release', '-v', 'q', '-nologo', '-o', out_dir]
        t0 = time.time()
        result = subprocess.run(cmd, capture_output=True, env=env)
        elapsed = time.time() - t0
        output = ''
        if result.stdout:
            output += result.stdout.decode('utf-8', errors='replace') if isinstance(result.stdout, bytes) else result.stdout
        if result.stderr:
            output += result.stderr.decode('utf-8', errors='replace') if isinstance(result.stderr, bytes) else result.stderr
        target_filename = os.path.basename(target)
        target_errors = []
        other_warnings = []
        seen = set()
        for line in output.split('\n'):
            if re.search(r'error CS\d+', line):
                if target_filename in line:
                    key = line.strip()
                    if key not in seen:
                        seen.add(key)
                        target_errors.append(key)
                else:
                    other_warnings.append(line.strip())
        target_errors = target_errors[:25]
        print(f"[{stem}] build done in {elapsed:.1f}s  target_errors={len(target_errors)}  other_warnings={len(other_warnings)}")
        for w in other_warnings[:10]:
            print(f"  WARNING: {w}")
        return target_errors

    # --- Initial generation + compile ---
    call_gen_code(prompt_path, target)
    attempts = 1
    target_errors = compile_project()

    # --- Fix loop ---
    fix_n = 0
    while target_errors and fix_n < max_fix:
        fix_n += 1
        attempts += 1
        print(f"[{stem}] Attempt {attempts} (fix {fix_n}): {len(target_errors)} errors")
        try:
            with open(target, 'r', encoding='utf-8') as f:
                current_code = f.read()
        except (IOError, OSError):
            current_code = ''
        fix_parts = [COMMON_RULES, ""]
        fix_parts.append("CURRENT FILE\n```csharp\n" + current_code + "\n```\n")
        fix_parts.append("COMPILER ERRORS\n")
        for e in target_errors:
            fix_parts.append(e)
        fix_parts.append("")
        fix_parts.append("API DIGEST\n" + api_digest + "\n")
        if depends_text:
            fix_parts.append("DEPENDS\n" + depends_text + "\n")
        fix_parts.append("Fix only the reported errors, return the complete file.\n")
        fix_prompt = '\n'.join(fix_parts)
        fix_path = f'work/{stem}.fix{fix_n}.prompt.md'
        with open(fix_path, 'w', encoding='utf-8') as f:
            f.write(fix_prompt)
        call_gen_code(fix_path, target)
        target_errors = compile_project()

    # --- Result ---
    if not target_errors:
        result_line = f"RESULT {stem} OK attempts={attempts}"
        exit_code = 0
    else:
        result_line = f"RESULT {stem} FAIL attempts={attempts} errors={len(target_errors)}"
        exit_code = 1
    print(result_line)

    ts = datetime.datetime.now().isoformat()
    log_path = os.path.join('work', 'build-loop.log')
    with open(log_path, 'a', encoding='utf-8') as f:
        f.write(f"{ts} {result_line}\n")

    sys.exit(exit_code)

if __name__ == '__main__':
    main()
