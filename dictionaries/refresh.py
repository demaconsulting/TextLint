#!/usr/bin/env python3
# Copyright (c) DEMA Consulting
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

"""Regenerates the committed word lists under ``dictionaries/``.

WHY THIS SCRIPT EXISTS
    The word lists are plain text so that they are reviewable, diff-friendly, and editable by
    hand. The large ones are not written by hand: the English lists come from SCOWL/ESDB and the
    technical lists come from pinned cspell-dicts npm packages. This script is the single,
    reproducible recipe that turns those upstream sources into the committed files, so a refresh
    is a reviewable change to the lists rather than an opaque binary swap.

WHAT IT PRODUCES
    ``en-common/common.txt``   words in both the American and the British (-ise) lists
    ``en-US/us.txt``           American-only words
    ``en-GB/gb.txt``           British (-ise)-only words
    ``technical/<name>.txt``   one file per imported cspell-dicts package (13 files)

    ``technical/additions.txt`` is project-owned and is NEVER touched by this script.

PREREQUISITES
    * Python 3.7 or later, built with SQLite 3.33.0 or later (SCOWL requirement).
    * git and npm on the PATH.
    * Network access (GitHub and the npm registry).
    * ``PYTHONUTF8=1`` in the environment. The script sets it for the child processes it starts,
      but on Windows it is still best to set it for the whole session, because SCOWL's importer
      fails on the code page decode of accented words such as "abbe" otherwise.

USAGE
    python dictionaries/refresh.py [--workdir <dir>] [--only english|technical|all]

    Creating the SCOWL database takes about five minutes. Pass the same ``--workdir`` again to
    reuse the clone and the database from an earlier run.

    This script is a manual maintenance tool. It is never run by the build or by CI.
"""

from __future__ import annotations

import argparse
import gzip
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path

# ----------------------------------------------------------------------------------------------
# Pinned upstream sources. Everything that can change a refresh result is in this block.
# ----------------------------------------------------------------------------------------------

SCOWL_REPOSITORY = "https://github.com/en-wl/wordlist"
SCOWL_TAG = "rel-2026.02.25"
SCOWL_COMMIT = "7e99edab8e32f9f9ea2b15f249ca8d4d67237410"
SCOWL_SIZE = "70"
SCOWL_VARIANT_LEVEL = "1"
SCOWL_AMERICAN = "A"
SCOWL_BRITISH_ISE = "B"

# (list name, npm package name, pinned version, source file base names inside the package)
# Every package is MIT licensed; the script verifies that from package.json before using it.
CSPELL_PACKAGES = [
    ("software-terms", "software-terms", "5.4.8",
     ["softwareTerms", "software-tools", "webServices", "computing-acronyms", "coding-compound-terms"]),
    ("companies", "companies", "3.2.13", ["companies"]),
    ("filetypes", "filetypes", "3.0.18", ["filetypes"]),
    ("public-licenses", "public-licenses", "2.0.17", ["public-licenses"]),
    ("fullstack", "fullstack", "3.2.9", ["fullstack"]),
    ("dotnet", "dotnet", "5.0.15", ["dotnet"]),
    ("csharp", "csharp", "4.0.8", ["csharp"]),
    ("node", "node", "5.0.9", ["node"]),
    ("npm", "npm", "5.2.55", ["npm"]),
    ("typescript", "typescript", "3.2.3", ["typescript"]),
    ("html", "html", "4.0.16", ["html"]),
    ("powershell", "powershell", "5.0.17", ["powershell"]),
    ("k8s", "k8s", "1.0.13", ["k8s"]),
]

# ----------------------------------------------------------------------------------------------
# Entry rules. These mirror the rules the DictionaryBuilder enforces when it reads the lists, so a
# refresh can never write a line the build would reject.
# ----------------------------------------------------------------------------------------------

MAX_WORD_LENGTH = 64
APOSTROPHE = "'"
TYPOGRAPHIC_APOSTROPHE = "\u2019"
MIN_LETTERS_IN_RUN = 2
MULTIPLICATION_SIGN = 0xD7
DIVISION_SIGN = 0xF7
FIRST_LATIN1_LETTER = 0xC0
LAST_LATIN1_LETTER = 0xFF

# A run of letters (any script) with single internal apostrophes. Digits, underscores, hyphens,
# dots, slashes, at signs, brackets and every other character separate runs.
LETTER_RUN = re.compile(r"[^\W\d_]+(?:'[^\W\d_]+)*")


def is_allowed_letter(char: str) -> bool:
    """Returns True for an ASCII letter or a Latin-1 letter (U+00C0-U+00FF except U+00D7 and U+00F7)."""
    code = ord(char)
    if "a" <= char <= "z" or "A" <= char <= "Z":
        return True
    return FIRST_LATIN1_LETTER <= code <= LAST_LATIN1_LETTER and code not in (MULTIPLICATION_SIGN, DIVISION_SIGN)


def is_valid_entry(word: str) -> bool:
    """Applies the DictionaryBuilder entry rule: 1-64 allowed letters or internal single apostrophes."""
    if not word or len(word) > MAX_WORD_LENGTH:
        return False
    if not is_allowed_letter(word[0]) or not is_allowed_letter(word[-1]):
        return False
    previous_was_apostrophe = False
    for char in word:
        if char == APOSTROPHE:
            if previous_was_apostrophe:
                return False
            previous_was_apostrophe = True
        elif is_allowed_letter(char):
            previous_was_apostrophe = False
        else:
            return False
    return True


# ----------------------------------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------------------------------


def run(command: list, cwd: Path = None, capture: bool = False) -> str:
    """Runs a child process, failing loudly, and optionally returns its UTF-8 standard output."""
    child_env = dict(os.environ)
    child_env["PYTHONUTF8"] = "1"
    print("  $ " + " ".join(str(part) for part in command), flush=True)
    result = subprocess.run(
        [str(part) for part in command],
        cwd=str(cwd) if cwd else None,
        env=child_env,
        check=True,
        stdout=subprocess.PIPE if capture else None,
    )
    return result.stdout.decode("utf-8") if capture else ""


def write_list(path: Path, header: list, words: list) -> None:
    """Writes a list file: comment header, then one word per line, LF line endings, no BOM."""
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [("# " + line) if line else "" for line in header]
    lines.extend(words)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")


# ----------------------------------------------------------------------------------------------
# English (SCOWL/ESDB)
# ----------------------------------------------------------------------------------------------


def prepare_scowl(workdir: Path) -> Path:
    """Clones the pinned SCOWL tag (if absent), verifies the commit, and creates scowl.db (if absent)."""
    clone = workdir / "wordlist"
    if not clone.exists():
        run(["git", "clone", "--quiet", "--depth", "1", "--branch", SCOWL_TAG, SCOWL_REPOSITORY, clone])
    commit = run(["git", "rev-parse", "HEAD"], cwd=clone, capture=True).strip()
    if commit != SCOWL_COMMIT:
        raise SystemExit(f"SCOWL tag {SCOWL_TAG} resolves to {commit}, expected {SCOWL_COMMIT}.")
    if not (clone / "scowl.db").exists():
        print("Creating the SCOWL database (about five minutes)...", flush=True)
        run([sys.executable, "combine.py", "create-db", "scowl.db"], cwd=clone)
    return clone


def scowl_words(clone: Path, spelling: str, deaccent: bool) -> list:
    """Exports one SCOWL word list as a list of lines."""
    command = [sys.executable, "scowl", "--db", "scowl.db", "word-list", SCOWL_SIZE, spelling, SCOWL_VARIANT_LEVEL]
    if deaccent:
        command.append("--deaccent")
    return run(command, cwd=clone, capture=True).splitlines()


def english_set(clone: Path, spelling: str, dropped: list) -> set:
    """Returns the plain list unioned with its de-accented list, keeping only valid entries."""
    result = set()
    for deaccent in (False, True):
        for line in scowl_words(clone, spelling, deaccent):
            word = line.strip()
            if is_valid_entry(word):
                result.add(word)
            elif word:
                dropped.append(word)
    return result


def build_english(workdir: Path, output: Path) -> list:
    """Builds en-common, en-US and en-GB and returns summary rows."""
    clone = prepare_scowl(workdir)
    dropped: list = []
    american = english_set(clone, SCOWL_AMERICAN, dropped)
    british = english_set(clone, SCOWL_BRITISH_ISE, dropped)

    # Case-sensitive set algebra: the builder merges case variants later, so the lists stay faithful to SCOWL.
    parts = [
        ("en-common", "common.txt", american & british, "words found in both the American and the British (-ise) lists"),
        ("en-US", "us.txt", american - british, "words found only in the American list"),
        ("en-GB", "gb.txt", british - american, "words found only in the British (-ise) list"),
    ]
    rows = []
    for folder, name, words, meaning in parts:
        header = [
            f"English word list ({folder}): {meaning}.",
            f"Source: SCOWL/ESDB (Spell Checker Oriented Word Lists), {SCOWL_REPOSITORY}",
            f"Version: tag {SCOWL_TAG}, commit {SCOWL_COMMIT}",
            "License: SCOWL permissive notice, see third-party/THIRD-PARTY-NOTICES.txt",
            f"Generation: python scowl word-list {SCOWL_SIZE} <A|B> {SCOWL_VARIANT_LEVEL} [--deaccent], then",
            "  A = American, B = British -ise; each list is the union of the plain and de-accented output;",
            "  common = A and B, US = A not B, GB = B not A (case-sensitive set operations).",
            "Generated by dictionaries/refresh.py; do not edit by hand.",
            f"Entries: {len(words)}",
            "",
        ]
        write_list(output / folder / name, header, sorted(words))
        rows.append((f"{folder}/{name}", len(words), 0))
    rows.append(("english entries rejected", 0, len(dropped)))
    if dropped:
        print(f"English entries rejected by the entry rule: {len(dropped)} (first few: {dropped[:5]})")
    return rows


# ----------------------------------------------------------------------------------------------
# Technical terms (cspell-dicts npm packages)
# ----------------------------------------------------------------------------------------------


def read_package_file(archive: tarfile.TarFile, base_name: str) -> str:
    """Reads one dictionary source (plain or gzip) from an npm package tarball."""
    for candidate in (f"dict/{base_name}.txt", f"dict/{base_name}.txt.gz", f"{base_name}.txt", f"{base_name}.txt.gz"):
        try:
            member = archive.extractfile("package/" + candidate)
        except KeyError:
            continue
        if member is None:
            continue
        data = member.read()
        if candidate.endswith(".gz"):
            data = gzip.decompress(data)
        return data.decode("utf-8")
    raise SystemExit(f"Source file '{base_name}' not found in package.")


def split_entry(line: str) -> list:
    """Splits one cspell source line into candidate word runs (see the module notes on the import filter)."""
    text = line.strip()
    if not text or text.startswith("#") or text.startswith("!"):
        return []
    return LETTER_RUN.findall(text.replace(TYPOGRAPHIC_APOSTROPHE, APOSTROPHE))


def build_technical(workdir: Path, output: Path) -> list:
    """Imports every pinned cspell-dicts package and returns summary rows."""
    npm = shutil.which("npm")
    if npm is None:
        raise SystemExit("npm was not found on the PATH.")
    rows = []
    for list_name, package, version, sources in CSPELL_PACKAGES:
        spec = f"@cspell/dict-{package}@{version}"
        run([npm, "pack", spec, "--silent", "--pack-destination", workdir])
        tarball = workdir / f"cspell-dict-{package}-{version}.tgz"
        words = set()
        dropped = 0
        with tarfile.open(tarball) as archive:
            metadata = json.loads(archive.extractfile("package/package.json").read().decode("utf-8"))
            if metadata.get("license") != "MIT":
                raise SystemExit(f"{spec} is licensed '{metadata.get('license')}', expected MIT.")
            license_text = archive.extractfile("package/LICENSE").read().decode("utf-8")
            copyright_line = next(line for line in license_text.splitlines() if line.startswith("Copyright"))
            for source in sources:
                for line in read_package_file(archive, source).splitlines():
                    for run_text in split_entry(line):
                        letters = run_text.replace(APOSTROPHE, "")
                        if len(letters) < MIN_LETTERS_IN_RUN:
                            continue
                        if is_valid_entry(run_text):
                            words.add(run_text)
                        else:
                            dropped += 1
        header = [
            f"Technical term list ({list_name}), imported from the cspell-dicts package {spec}.",
            "Source: https://github.com/streetsidesoftware/cspell-dicts (published npm package)",
            f"Version: {spec}; source files: {', '.join(sources)}",
            f"License: MIT, {copyright_line}",
            "  See third-party/THIRD-PARTY-NOTICES.txt for the full notice.",
            "Generation: npm pack of the package above; every entry is split into runs of letters, runs of fewer",
            "  than two letters and runs that are not ASCII/Latin-1 are dropped, then duplicates are removed.",
            "Generated by dictionaries/refresh.py; do not edit by hand. Add project words to additions.txt.",
            f"Entries: {len(words)}",
            "",
        ]
        write_list(output / "technical" / f"{list_name}.txt", header, sorted(words))
        rows.append((f"technical/{list_name}.txt", len(words), dropped))
    return rows


# ----------------------------------------------------------------------------------------------
# Entry point
# ----------------------------------------------------------------------------------------------


def main() -> int:
    """Parses the command line, runs the requested imports, and prints a summary for the PR description."""
    parser = argparse.ArgumentParser(description="Regenerate the committed word lists under dictionaries/.")
    parser.add_argument("--workdir", type=Path, help="scratch folder (default: a new temporary folder)")
    parser.add_argument("--only", choices=["english", "technical", "all"], default="all",
                        help="which lists to regenerate (default: all)")
    arguments = parser.parse_args()

    output = Path(__file__).resolve().parent
    created_workdir = arguments.workdir is None
    workdir = (arguments.workdir or Path(tempfile.mkdtemp(prefix="textlint-dictionaries-"))).resolve()
    workdir.mkdir(parents=True, exist_ok=True)

    rows = []
    if arguments.only in ("english", "all"):
        rows.extend(build_english(workdir, output))
    if arguments.only in ("technical", "all"):
        rows.extend(build_technical(workdir, output))

    print()
    print(f"{'list':<36}{'entries':>10}{'dropped':>10}")
    for name, count, dropped in rows:
        print(f"{name:<36}{count:>10}{dropped:>10}")
    print(f"{'total':<36}{sum(row[1] for row in rows):>10}{sum(row[2] for row in rows):>10}")

    if created_workdir:
        print(f"Scratch folder kept for inspection: {workdir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
