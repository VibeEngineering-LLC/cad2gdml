import sys
import os
import subprocess


def main():
    sys.stdout.reconfigure(encoding='utf-8')

    args = sys.argv[1:]
    if len(args) < 3:
        sys.exit(1)

    rel_file = args[0]
    pairs = []
    i = 1
    while i + 1 < len(args):
        pairs.append((args[i], args[i + 1]))
        i += 2

    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.dirname(script_dir)

    file_path = os.path.join(repo_root, rel_file)
    with open(file_path, 'r', encoding='utf-8', newline='') as f:
        original_text = f.read()

    try:
        for old, new in pairs:
            count = original_text.count(old)
            if count != 1:
                print(f"SKIP (вхождений {count}): {old}")
                continue

            modified = original_text.replace(old, new, 1)
            with open(file_path, 'w', encoding='utf-8', newline='') as f:
                f.write(modified)

            build = subprocess.run(
                ['dotnet', 'build', 'src/Cad2Gdml.Tests', '-c', 'Release',
                 '-o', 'work/bin/tests', '-v', 'q', '-nologo'],
                cwd=repo_root, capture_output=True)
            stdout = build.stdout.decode('utf-8', errors='replace')

            if build.returncode != 0:
                print(f"MUTANT [{old} -> {new}]: BUILD-ERROR")
                print(stdout[-300:])
                continue

            test = subprocess.run(
                [os.path.join(repo_root, 'work', 'bin', 'tests', 'Cad2GdmlTests.exe')],
                cwd=repo_root, capture_output=True)
            output = test.stdout.decode('utf-8', errors='replace')
            lines = output.splitlines()
            fail_lines = [l for l in lines if l.startswith('FAIL')]
            tests_lines = [l for l in lines if l.startswith('TESTS')]

            print(f"MUTANT [{old} -> {new}]: красных={len(fail_lines)}")
            for fl in fail_lines:
                print(f"  {fl}")
            if tests_lines:
                print(f"  {tests_lines[0]}")
    finally:
        with open(file_path, 'w', encoding='utf-8', newline='') as f:
            f.write(original_text)
        print(f"ВОССТАНОВЛЕН {rel_file}")


if __name__ == '__main__':
    main()
    sys.exit(0)
