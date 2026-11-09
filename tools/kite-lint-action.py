#!/usr/bin/env python3
"""
Kite CI/CD Architectural Drift Firewall
Audits source code imports and git diffs against architectural boundaries and layers.
Outputs structured reports, Mermaid topology diagrams, and returns exit code 0 or 1.
Zero external dependencies. Pure standard library Python 3.
Zero emojis.
"""

import sys
import os
import re
import json
import subprocess
from pathlib import Path

DEFAULT_RULES = [
    {
        "name": "PresentationToDataBypass",
        "sourceLayer": "Presentation",
        "forbiddenTargets": ["Data", "Persistence", "Storage"],
        "severity": "error",
        "description": "Presentation layer cannot import data/storage layers directly without domain service encapsulation."
    },
    {
        "name": "DomainReverseDependency",
        "sourceLayer": "Domain",
        "forbiddenTargets": ["Presentation", "UI", "Infrastructure"],
        "severity": "error",
        "description": "Domain layer must remain clean and cannot depend on UI or external driver infrastructure."
    },
    {
        "name": "DataReverseDependency",
        "sourceLayer": "Data",
        "forbiddenTargets": ["Presentation", "UI"],
        "severity": "error",
        "description": "Data layer cannot depend on presentation or UI components."
    }
]

def infer_layer(path_str):
    p = path_str.lower().replace("\\", "/")
    clean = "/" + p.strip("/.") + "/"

    def matches(keywords):
        return any(f"/{kw}/" in clean or f"/{kw}" in clean or f"{kw}/" in clean or p == kw or f".{kw}" in p for kw in keywords)

    if matches(["views", "pages", "ui", "components", "controllers", "viewmodels"]):
        return "Presentation"
    if matches(["domain", "services", "models", "usecases", "core"]):
        return "Domain"
    if matches(["data", "repositories", "storage", "persistence", "db", "database"]):
        return "Data"
    if matches(["infrastructure", "network", "drivers", "gateways"]):
        return "Infrastructure"
    return "Common"

def extract_imports(file_content, ext):
    imports = set()
    lines = file_content.splitlines()

    for line in lines:
        line = line.strip()

        # Swift: import Module
        if ext == ".swift":
            if line.startswith("import "):
                parts = line.split()
                if len(parts) >= 2:
                    mod = parts[1].strip(";,")
                    if mod:
                        imports.add(mod)

        # C#: using Namespace;
        elif ext == ".cs":
            if line.startswith("using ") and line.endswith(";"):
                trimmed = line[6:-1].strip()
                if trimmed and "=" not in trimmed:
                    imports.add(trimmed)

        # TypeScript / JavaScript / TSX / JSX: import ... from '...' or require('...')
        elif ext in [".ts", ".tsx", ".js", ".jsx"]:
            if "from \"" in line or "from '" in line:
                m = re.findall(r"from\s+['\"]([^'\"]+)['\"]", line)
                for item in m:
                    imports.add(item)
            elif "require(\"" in line or "require('" in line:
                m = re.findall(r"require\s*\(\s*['\"]([^'\"]+)['\"]\s*\)", line)
                for item in m:
                    imports.add(item)

        # Python: import x, from x import y
        elif ext == ".py":
            if line.startswith("from "):
                parts = line.split()
                if len(parts) >= 2 and parts[1]:
                    imports.add(parts[1])
            elif line.startswith("import "):
                parts = line.split()
                if len(parts) >= 2:
                    mod = parts[1].split(",")[0].strip()
                    if mod:
                        imports.add(mod)

    return sorted(list(imports))

def get_git_diff_files():
    try:
        # Check staged and unstaged modified files
        res = subprocess.run(["git", "diff", "--name-only", "HEAD~1"], capture_output=True, text=True, check=False)
        files = [f.strip() for f in res.stdout.splitlines() if f.strip()]
        if not files:
            res = subprocess.run(["git", "diff", "--name-only"], capture_output=True, text=True, check=False)
            files = [f.strip() for f in res.stdout.splitlines() if f.strip()]
        return files
    except Exception:
        return []

def scan_files(file_paths, rules=None):
    if rules is None:
        rules = DEFAULT_RULES

    violations = []
    supported_exts = {".swift", ".cs", ".ts", ".tsx", ".js", ".jsx", ".py"}

    for path_str in file_paths:
        p = Path(path_str)
        if not p.is_file() or p.suffix not in supported_exts:
            continue

        source_layer = infer_layer(path_str)
        if source_layer == "Common":
            continue

        try:
            content = p.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue

        imports = extract_imports(content, p.suffix)

        for imp in imports:
            target_layer = infer_layer(imp)
            if target_layer == "Common" or target_layer == source_layer:
                continue

            for rule in rules:
                if rule["sourceLayer"] == source_layer and target_layer in rule["forbiddenTargets"]:
                    violations.append({
                        "id": f"{path_str}->{imp}",
                        "ruleName": rule["name"],
                        "sourceFile": path_str,
                        "sourceLayer": source_layer,
                        "importedModule": imp,
                        "targetLayer": target_layer,
                        "severity": rule["severity"],
                        "description": rule["description"]
                    })

    return violations

def generate_mermaid(violations):
    lines = [
        "graph TD",
        "    subgraph Presentation [Presentation Layer]",
        "        Views[Views and UI Components]",
        "        Controllers[Controllers and ViewModels]",
        "    end",
        "    subgraph Domain [Domain Layer]",
        "        Services[Application Services]",
        "        Entities[Domain Entities and UseCases]",
        "    end",
        "    subgraph Data [Data Layer]",
        "        Repos[Repositories and Storage]",
        "        Database[Database and Persistence]",
        "    end",
        "    subgraph Infrastructure [Infrastructure Layer]",
        "        Network[Network and Gateways]",
        "        Drivers[External Drivers]",
        "    end",
        "",
        "    %% Compliant Architectural Dependencies",
        "    Presentation --> Domain",
        "    Domain --> Repos",
        "    Repos --> Database",
        "    Infrastructure --> Domain"
    ]

    if violations:
        lines.append("")
        lines.append("    %% Architectural Drift Violations Detected")
        for v in violations[:6]:
            lines.append(f"    {v['sourceLayer']} -.-|\"DRIFT: {v['importedModule']}\"| {v['targetLayer']}")
            lines.append(f"    style {v['sourceLayer']} stroke:#e74c3c,stroke-width:2px")
            lines.append(f"    style {v['targetLayer']} stroke:#e74c3c,stroke-width:2px")

    return "\n".join(lines)

def generate_markdown_report(violations, checked_count):
    lines = [
        "## Kite Architectural Drift Firewall Report",
        "",
        f"- Files Audited: {checked_count}",
        f"- Total Violations: {len(violations)}",
        ""
    ]

    if not violations:
        lines.append("Status: PASS. No architectural boundary violations detected.")
        lines.append("")
    else:
        lines.append("Status: FAIL. Architectural drift detected.")
        lines.append("")
        lines.append("| Rule | Severity | Source File | Source Layer | Target Layer | Module |")
        lines.append("| --- | --- | --- | --- | --- | --- |")
        for v in violations:
            lines.append(f"| {v['ruleName']} | {v['severity'].upper()} | `{v['sourceFile']}` | {v['sourceLayer']} | {v['targetLayer']} | `{v['importedModule']}` |")
        lines.append("")

    lines.append("### Architectural Topology")
    lines.append("```mermaid")
    lines.append(generate_mermaid(violations))
    lines.append("```")
    lines.append("")

    return "\n".join(lines)

def main():
    args = sys.argv[1:]
    target_files = []
    output_markdown = None
    json_output = False
    strict = True

    idx = 0
    while idx < len(args):
        arg = args[idx]
        if arg == "--out-md" and idx + 1 < len(args):
            output_markdown = args[idx + 1]
            idx += 2
        elif arg == "--json":
            json_output = True
            idx += 1
        elif arg == "--lenient":
            strict = False
            idx += 1
        elif not arg.startswith("--"):
            target_files.append(arg)
            idx += 1
        else:
            idx += 1

    if not target_files:
        diff_files = get_git_diff_files()
        if diff_files:
            target_files = diff_files
        else:
            # Fallback: scan all source files under repo
            root = Path(".")
            for ext in ["*.swift", "*.cs", "*.ts", "*.tsx", "*.js", "*.py"]:
                target_files.extend([str(p) for p in root.rglob(ext) if ".git" not in p.parts and "bin" not in p.parts and "obj" not in p.parts])

    violations = scan_files(target_files)

    if output_markdown:
        md = generate_markdown_report(violations, len(target_files))
        Path(output_markdown).write_text(md, encoding="utf-8")

    # If running in GitHub Actions, populate GITHUB_STEP_SUMMARY
    step_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        md = generate_markdown_report(violations, len(target_files))
        try:
            with open(step_summary, "a", encoding="utf-8") as f:
                f.write(md + "\n")
        except Exception:
            pass

    if json_output:
        print(json.dumps({
            "status": "fail" if violations else "pass",
            "auditedFilesCount": len(target_files),
            "violationsCount": len(violations),
            "violations": violations
        }, indent=2))
    else:
        print(f"=== Kite Architectural Drift Firewall ===")
        print(f"Audited {len(target_files)} files.")
        if violations:
            print(f"FAIL: Detected {len(violations)} architectural boundary violation(s):")
            for v in violations:
                print(f"  [{v['severity'].upper()}] {v['ruleName']}: {v['sourceFile']} ({v['sourceLayer']}) -> {v['importedModule']} ({v['targetLayer']})")
        else:
            print("PASS: Clean architecture verified. Zero boundary drift violations.")

    if violations and strict:
        sys.exit(1)
    sys.exit(0)

if __name__ == "__main__":
    main()
