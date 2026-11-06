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
    hand. The large ones are not written by hand: the English lists come from SCOWL/ESDB, from
    Hunspell dictionaries expanded with their affix rules, and from the cspell-dicts English lists;
    the technical lists come from pinned cspell-dicts npm packages. This script is the single,
    reproducible recipe that turns those upstream sources into the committed files, so a refresh
    is a reviewable change to the lists rather than an opaque binary swap. Generating the word forms
    here, rather than in the dictionary builder, keeps the builder unchanged and the committed lists
    plain text that a reviewer can read.

WHAT IT PRODUCES
    The English lists, one file per source in each of three folders (the folder is the spelling
    region; see ``word_forms.py`` for the routing rule):

    ``en-common/<source>.txt``  words that are spelled the same in American and British (-ise) English
    ``en-US/<source>.txt``      American-only spellings
    ``en-GB/<source>.txt``      British (-ise)-only spellings

    where ``<source>`` is, in priority order (a word is written only to the first file that has it):

    ``common|us|gb``         the SCOWL base word lists (size 70, variant level 1)
    ``hunspell``             forms of the SCOWL Hunspell dictionaries of the pinned release (en_US-large,
                             en_GB-large), generated from the stems and affix rules
    ``cspell``               the maintained cspell-dicts English lists (additional words, acronyms,
                             shared words and their -ize and -ise lists)
    ``hunspell-legacy``      forms of the older SCOWL Hunspell en_US-large dictionary that cspell-dicts keeps
    ``cspell-legacy``        the cspell-dicts legacy English list, ONLY the words SCOWL size 80 also lists

    ``technical/language/<name>.txt``  programming-language keyword and API lists (7 files)
    ``technical/general/<name>.txt``   general technical lists (7 files, plus the project-owned additions.txt)

    ``technical/general/additions.txt``, ``technical/variant-keep.txt`` and ``exclusions.txt`` are
    project-owned and are NEVER touched by this script.

    The exclude lists of cspell-dicts (words its dictionaries deliberately do not contain) are honored
    here: those words are never written. The project's own ``exclusions.txt`` is different: it is applied
    by the dictionary builder, so the committed lists stay faithful to the upstream data.

PREREQUISITES
    * Python 3.7 or later, built with SQLite 3.33.0 or later (SCOWL requirement).
    * git and npm on the PATH (``--verify-expander`` also needs Node.js 22.18 or later).
    * Network access (GitHub and the npm registry).
    * ``PYTHONUTF8=1`` in the environment. The script sets it for the child processes it starts,
      but on Windows it is still best to set it for the whole session, because SCOWL's importer
      fails on the code page decode of accented words such as "abbe" otherwise.

USAGE
    python dictionaries/refresh.py [--workdir <dir>] [--only english|technical|all] [--verify-expander]

    Creating the SCOWL database takes about five minutes. Pass the same ``--workdir`` again to
    reuse the clone and the database from an earlier run. Downloads are cached in the work folder and
    checked against the SHA-256 values pinned below.

    ``--verify-expander`` checks the Hunspell expander of ``word_forms.py`` against the independent
    ``hunspell-reader`` npm package (the expander cspell itself uses) on every pinned dictionary and
    exits with an error if the two produce different word sets; no list is written.

    This script is a manual maintenance tool. It is never run by the build or by CI.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
import zipfile
from collections import Counter, defaultdict
from pathlib import Path

import word_forms

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

# The wider SCOWL scope used ONLY to classify words (which spelling region a word belongs to); no
# word is written to a list because it is in this scope. Size 80 is the largest size that does not
# draw on the UKACD list, whose notice would otherwise apply.
SCOWL_CLASSIFIER_SIZE = "80"
SCOWL_CLASSIFIER_VARIANT_LEVEL = "3"

# The Hunspell dictionaries of the pinned SCOWL release (size 70, the "large" dictionaries). The British
# one holds both -ise and -ize spellings; the routing rule keeps the -ize forms out of the British list.
# (dictionary name, release asset, SHA-256 of the asset)
SCOWL_HUNSPELL_FILES = [
    ("en_US-large", "hunspell-en_US-large-2026.02.25.zip",
     "06ab5a2a12c29033f100988d3b0a5e53dcad40bf2473ccb78270619d1da99321"),
    ("en_GB-large", "hunspell-en_GB-large-2026.02.25.zip",
     "704a49438a3a9d4f11cd76db433a6b230b9ce8770ee13fa28700052c4517ddc1"),
]

# The independent Hunspell expander used by --verify-expander (the one cspell itself uses; MIT).
HUNSPELL_READER_PACKAGE = "hunspell-reader"
HUNSPELL_READER_VERSION = "10.4.0"

# The cspell-dicts repository at a pinned commit. Files are downloaded from the commit-addressed raw
# URL (immutable) and checked against the SHA-256 values below. Both the repository and the English
# packages are MIT licensed (Copyright Street Side Software); the word data inside is SCOWL data.
CSPELL_DICTS_REPOSITORY = "https://github.com/streetsidesoftware/cspell-dicts"
CSPELL_DICTS_COMMIT = "72b65202e1b774153003577073303c4ef1d72e4e"
CSPELL_DICTS_RAW = f"https://raw.githubusercontent.com/streetsidesoftware/cspell-dicts/{CSPELL_DICTS_COMMIT}/dictionaries/"
CSPELL_EN_US_PACKAGE = "@cspell/dict-en_us 4.4.43"
CSPELL_EN_SHARED_PACKAGE = "@cspell/dict-en-shared 1.0.58 (private package)"

# (file key, path below dictionaries/, SHA-256)
CSPELL_ENGLISH_FILES = {
    "legacy": ("en_US/src/legacy/en_US.txt", "b2a521bc9a82ac9d86689f19cb173b82af05092706e4843a7a22093607fde928"),
    "us-additional": ("en_US/src/additional_words.txt", "85599bce87557191cd92cb8290a4218e28ad76c102a79967ca278fedb388bfb8"),
    "us-acronyms": ("en_US/src/acronyms.txt", "5840f4d7599213a0013cf75e7963e6c8926b93df3628ef6126e6c43e6366010e"),
    "us-exclude": ("en_US/src/exclude-words.txt", "cf99529027e63f323dcc4732b95e745325850ba7a12be0dc76a275b7853bb4ec"),
    "hunspell-dic": ("en_US/src/hunspell/en_US-large.dic", "ae6a954150706152d111348160fccc3a2e5b34ecdb684fe9a6910938dd7b05cf"),
    "hunspell-aff": ("en_US/src/hunspell/en_US-large.aff", "c7a8c4d08c29d237880844b1623099f59092602f189be38ce3912e457ff38bc1"),
    "shared": ("en_shared/src/shared-additional-words.txt", "8f1fdb7e2776f844b498c185fb98aa5c51932f3a4d2b83be0689474d2717845c"),
    "shared-ize": ("en_shared/src/shared-additional-words-ize.txt", "be8c432486154a910134d567debb47db8b9489a0366964c3aa851b53238f7864"),
    "shared-ise": ("en_shared/src/shared-additional-words-ise.txt", "7324c6901145b9e284ac6809ebe431c777c6248322f7164ba4b7c222b80aad65"),
    "shared-acronyms": ("en_shared/src/acronyms.txt", "6d0a3d37a5150241aa782238d28862ce9922b51bec09b96b1102237f811d2ce8"),
    "shared-exclude": ("en_shared/src/exclude-words.txt", "757e4453840d57337bc030c9ddf09181aa5ea2b6ecc66b8af883ce2fe0a30e62"),
}

# (list name, group, npm package name, pinned version, source file base names inside the package)
# The group is the folder under technical/ that the list is written to: "language" for programming
# language keyword and API lists, "general" for everything else (see dictionaries/README.md).
# Every package is MIT licensed; the script verifies that from package.json before using it.
CSPELL_PACKAGES = [
    ("software-terms", "general", "software-terms", "5.4.8",
     ["softwareTerms", "software-tools", "webServices", "computing-acronyms", "coding-compound-terms"]),
    ("companies", "general", "companies", "3.2.13", ["companies"]),
    ("filetypes", "general", "filetypes", "3.0.18", ["filetypes"]),
    ("public-licenses", "general", "public-licenses", "2.0.17", ["public-licenses"]),
    ("fullstack", "general", "fullstack", "3.2.9", ["fullstack"]),
    ("dotnet", "language", "dotnet", "5.0.15", ["dotnet"]),
    ("csharp", "language", "csharp", "4.0.8", ["csharp"]),
    ("node", "language", "node", "5.0.9", ["node"]),
    ("npm", "general", "npm", "5.2.55", ["npm"]),
    ("typescript", "language", "typescript", "3.2.3", ["typescript"]),
    ("html", "language", "html", "4.0.16", ["html"]),
    ("css", "language", "css", "4.1.2", ["css"]),
    ("powershell", "language", "powershell", "5.0.17", ["powershell"]),
    ("k8s", "general", "k8s", "1.0.13", ["k8s"]),
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


def scowl_words(clone: Path, spelling: str, deaccent: bool, size: str = SCOWL_SIZE, variant_level: str = SCOWL_VARIANT_LEVEL) -> list:
    """Exports one SCOWL word list as a list of lines."""
    command = [sys.executable, "scowl", "--db", "scowl.db", "word-list", size, spelling, variant_level]
    if deaccent:
        command.append("--deaccent")
    return run(command, cwd=clone, capture=True).splitlines()


def english_set(clone: Path, spelling: str, dropped: list, size: str = SCOWL_SIZE, variant_level: str = SCOWL_VARIANT_LEVEL) -> set:
    """Returns the plain list unioned with its de-accented list, keeping only valid entries."""
    result = set()
    for deaccent in (False, True):
        for line in scowl_words(clone, spelling, deaccent, size, variant_level):
            word = line.strip()
            if is_valid_entry(word):
                result.add(word)
            elif word:
                dropped.append(word)
    return result


def fetch_pinned(url: str, sha256: str, destination: Path) -> Path:
    """Downloads a file once into the work folder and verifies it against its pinned SHA-256."""
    if not destination.exists():
        destination.parent.mkdir(parents=True, exist_ok=True)
        print(f"  download {url}", flush=True)
        with urllib.request.urlopen(url) as response:
            destination.write_bytes(response.read())
    actual = hashlib.sha256(destination.read_bytes()).hexdigest()
    if actual != sha256:
        raise SystemExit(f"{destination} has SHA-256 {actual}, expected {sha256}. Delete it to download it again.")
    return destination


def fetch_cspell_english(workdir: Path) -> dict:
    """Downloads the pinned cspell-dicts English files and returns their text by file key."""
    texts = {}
    for key, (path, sha256) in CSPELL_ENGLISH_FILES.items():
        destination = workdir / "cspell-english" / path.replace("/", "__")
        texts[key] = fetch_pinned(CSPELL_DICTS_RAW + path, sha256, destination).read_text(encoding="utf-8")
    return texts


def fetch_scowl_hunspell(workdir: Path) -> dict:
    """Downloads the pinned SCOWL Hunspell dictionaries and returns ``name -> (dic text, aff text)``."""
    result = {}
    for name, asset, sha256 in SCOWL_HUNSPELL_FILES:
        url = f"{SCOWL_REPOSITORY}/releases/download/{SCOWL_TAG}/{asset}"
        archive = fetch_pinned(url, sha256, workdir / "scowl-hunspell" / asset)
        with zipfile.ZipFile(archive) as bundle:
            result[name] = (bundle.read(f"{name}.dic").decode("utf-8"), bundle.read(f"{name}.aff").decode("utf-8"))
    return result


def cspell_list_words(text: str, dropped: list) -> list:
    """Splits a cspell-dicts English list into entries, in file order.

    A line is cut at its first ``#`` (a trailing comment), the typographic apostrophes and the backtick
    become ``'`` (the ``replacements`` of the cspell-dicts build), and the rest is split into runs of
    letters, as the ``cspell-tools: split`` directive of those lists does. A run of fewer than two
    letters is skipped, and a run that the entry rule rejects (for example a non-Latin letter) is counted.
    """
    words = []
    for line in text.splitlines():
        content = line.split("#", 1)[0].replace("\u2019", APOSTROPHE).replace("\u2018", APOSTROPHE).replace("`", APOSTROPHE)
        for run_text in LETTER_RUN.findall(content):
            if len(run_text.replace(APOSTROPHE, "")) < MIN_LETTERS_IN_RUN:
                continue
            if is_valid_entry(run_text):
                words.append(run_text)
            else:
                dropped.append(run_text)
    return words


def hunspell_entries(classifier: word_forms.RegionClassifier, dictionary_text: str, affix_text: str) -> list:
    """Expands a Hunspell dictionary into ``(form, hint)`` pairs; the hint is the region of the form's stem."""
    table = word_forms.parse_affix_file(affix_text)
    entries = []
    for stem, forms in word_forms.expand_dictionary(dictionary_text, table):
        hint = classifier.stem_hint(stem)
        entries.extend((form, hint) for form in sorted(forms))
    return entries


# Where a word goes. The folder is the spelling region and also the dictionary name.
REGION_FOLDERS = {
    word_forms.REGION_COMMON: "en-common",
    word_forms.REGION_US: "en-US",
    word_forms.REGION_GB: "en-GB",
}
REGION_MEANINGS = {
    word_forms.REGION_COMMON: "spelled the same in American and British (-ise) English",
    word_forms.REGION_US: "American-only spellings",
    word_forms.REGION_GB: "British (-ise)-only spellings",
}


def english_source_header(region: str, description: list, count: int) -> list:
    """Builds the comment header of one English list file."""
    folder = REGION_FOLDERS[region]
    return [
        f"English word list ({folder}): words {REGION_MEANINGS[region]}.",
        *description,
        "Routing: each word is assigned to the common, en-US, or en-GB folder by the rule in",
        "  dictionaries/word_forms.py (SCOWL's own variant data first, then spelling-pattern counterparts,",
        "  then the region of the source or stem); see dictionaries/README.md.",
        "Generated by dictionaries/refresh.py; do not edit by hand.",
        f"Entries: {count}",
        "",
    ]


def merge_regions(regions: set) -> str:
    """Merges the regions that different stems or lists proposed for one word.

    One proposal stands. A specific region (American or British) beats common, because a common
    proposal only says the source had no region for the word. Both specific regions together mean the
    word is valid in both, so it is common.
    """
    if len(regions) == 1:
        return next(iter(regions))
    specific = regions - {word_forms.REGION_COMMON}
    return next(iter(specific)) if len(specific) == 1 else word_forms.REGION_COMMON


def read_cspell_excludes(texts: dict) -> set:
    """Returns the lower-cased words that the cspell-dicts English exclude lists remove from their dictionaries."""
    excluded = set()
    for key in ("us-exclude", "shared-exclude"):
        for line in texts[key].splitlines():
            content = line.split("#", 1)[0]
            excluded.update(found.lower() for found in LETTER_RUN.findall(content))
    return excluded


def build_english(workdir: Path, output: Path) -> list:
    """Builds the English lists from all English sources and returns summary rows."""
    clone = prepare_scowl(workdir)
    dropped: list = []

    # The strict SCOWL base lists keep their exact meaning: common = A and B, US = A not B, GB = B not A
    american = english_set(clone, SCOWL_AMERICAN, dropped)
    british = english_set(clone, SCOWL_BRITISH_ISE, dropped)

    # The wider SCOWL scope is used only to classify words, never to add them
    classifier = word_forms.RegionClassifier(
        american,
        british,
        english_set(clone, SCOWL_AMERICAN, dropped, SCOWL_CLASSIFIER_SIZE, SCOWL_CLASSIFIER_VARIANT_LEVEL),
        english_set(clone, SCOWL_BRITISH_ISE, dropped, SCOWL_CLASSIFIER_SIZE, SCOWL_CLASSIFIER_VARIANT_LEVEL),
    )

    cspell = fetch_cspell_english(workdir)
    hunspell = fetch_scowl_hunspell(workdir)
    excluded = read_cspell_excludes(cspell)
    entry_drops: Counter = Counter()

    # The base lists, honoring the cspell-dicts exclude lists
    base_excluded = sorted(word for word in american | british if word.lower() in excluded)
    american -= set(base_excluded)
    british -= set(base_excluded)
    base_parts = {
        word_forms.REGION_COMMON: ("common.txt", american & british),
        word_forms.REGION_US: ("us.txt", american - british),
        word_forms.REGION_GB: ("gb.txt", british - american),
    }

    # The other sources, in priority order. Each yields (word, hint) pairs; the hint is the region the source
    # itself implies for the word (an -ize or -ise list, or the stem of a generated form), or None.
    def cspell_lists() -> list:
        sources = [
            ("shared", None), ("shared-acronyms", None), ("shared-ize", word_forms.REGION_US),
            ("shared-ise", word_forms.REGION_GB), ("us-additional", None), ("us-acronyms", None),
        ]
        dropped_here: list = []
        entries = [(word, hint) for key, hint in sources for word in cspell_list_words(cspell[key], dropped_here)]
        entry_drops["cspell"] = len(dropped_here)
        return entries

    def legacy_list() -> list:
        # The legacy list is not maintained upstream and may contain misspellings, so a word is accepted only when
        # SCOWL's wider scope (size 80, variant level 3) also lists it: SCOWL vets the word, the legacy list supplies
        # the form that the size-70 lists lack.
        dropped_here: list = []
        listed = [(word, None) for word in cspell_list_words(cspell["legacy"], dropped_here)]
        entries = [entry for entry in listed if classifier.is_in_wide_scope(entry[0])]
        entry_drops["cspell-legacy"] = len(dropped_here)
        print(f"legacy list: {len(entries)} of {len(listed)} entries are also listed by SCOWL size "
              f"{SCOWL_CLASSIFIER_SIZE}; the rest are not imported")
        return entries

    def hunspell_lists(dictionaries: list) -> list:
        entries: list = []
        for dictionary_text, affix_text in dictionaries:
            entries.extend(hunspell_entries(classifier, dictionary_text, affix_text))
        return entries

    sources = [
        ("hunspell", hunspell_lists(list(hunspell.values()))),
        ("cspell", cspell_lists()),
        ("hunspell-legacy", hunspell_lists([(cspell["hunspell-dic"], cspell["hunspell-aff"])])),
        ("cspell-legacy", legacy_list()),
    ]

    claimed = set(american | british)
    tiers: Counter = Counter()
    results: dict = {}
    excluded_found: Counter = Counter()
    for key, entries in sources:
        proposals: dict = defaultdict(set)
        for word, hint in entries:
            if word in claimed:
                continue
            if word.lower() in excluded:
                excluded_found[key] += 1
                continue
            if not is_valid_entry(word):
                entry_drops[key] += 1
                continue
            region, tier = classifier.route(word, hint)
            tiers[(key, tier)] += 1
            proposals[word].add(region)
        regions: dict = defaultdict(list)
        for word, proposed in proposals.items():
            regions[merge_regions(proposed)].append(word)
            claimed.add(word)
        results[key] = {region: sorted(words) for region, words in regions.items()}

    # Provenance headers, one per source
    base_description = [
        "Source: SCOWL/ESDB (Spell Checker Oriented Word Lists), " + SCOWL_REPOSITORY,
        f"Version: tag {SCOWL_TAG}, commit {SCOWL_COMMIT}",
        "License: SCOWL permissive notice, see third-party/THIRD-PARTY-NOTICES.txt",
        f"Generation: python scowl word-list {SCOWL_SIZE} <A|B> {SCOWL_VARIANT_LEVEL} [--deaccent], then",
        "  A = American, B = British -ise; each list is the union of the plain and de-accented output;",
        "  common = A and B, US = A not B, GB = B not A (case-sensitive set operations).",
        "  Words removed by the cspell-dicts exclude lists are not written (see refresh.py).",
    ]
    hunspell_description = [
        "Source: the SCOWL Hunspell dictionaries en_US-large and en_GB-large (size 70), " + SCOWL_REPOSITORY,
        f"Version: release {SCOWL_TAG} ({', '.join(asset for _, asset, _ in SCOWL_HUNSPELL_FILES)})",
        "License: SCOWL permissive notice and the Hunspell affix-file (Kuenning) notice,",
        "  see third-party/THIRD-PARTY-NOTICES.txt",
        "Generation: every stem of the .dic files expanded with the prefix and suffix rules (including cross",
        "  products) of the .aff files by dictionaries/word_forms.py; the expander is verified against",
        "  hunspell-reader (refresh.py --verify-expander). Only forms that the SCOWL base lists do not have",
        "  are written. These forms are DERIVED (generated) words, not stored SCOWL data.",
    ]
    cspell_description = [
        f"Source: cspell-dicts, {CSPELL_DICTS_REPOSITORY}: {CSPELL_EN_US_PACKAGE} (additional_words.txt,",
        f"  acronyms.txt) and {CSPELL_EN_SHARED_PACKAGE} (shared-additional-words.txt, -ize and -ise lists,",
        "  acronyms.txt)",
        f"Version: commit {CSPELL_DICTS_COMMIT}",
        "License: MIT, Copyright Street Side Software; see third-party/THIRD-PARTY-NOTICES.txt",
        "Generation: each line is cut at '#', split into runs of letters, runs of fewer than two letters are",
        "  dropped; the -ize list is routed American and the -ise list British unless SCOWL's data decides;",
        "  words in the cspell-dicts exclude lists are not written.",
    ]
    hunspell_legacy_description = [
        f"Source: the SCOWL 2020.12.07 Hunspell dictionary en_US-large kept by cspell-dicts ({CSPELL_DICTS_REPOSITORY},",
        "  dictionaries/en_US/src/hunspell), which also derives words that the current SCOWL release does not",
        f"Version: commit {CSPELL_DICTS_COMMIT}",
        "License: SCOWL (version 2020.12.07) and Hunspell affix-file (Kuenning) notices, see",
        "  third-party/THIRD-PARTY-NOTICES.txt; the repository itself is MIT, Copyright Street Side Software",
        "Generation: every stem expanded with the rules of the .aff file by dictionaries/word_forms.py, as for",
        "  the hunspell lists; only forms that no earlier source has are written. These are DERIVED words.",
    ]
    legacy_description = [
        f"Source: cspell-dicts legacy English list, {CSPELL_DICTS_REPOSITORY}:",
        "  dictionaries/en_US/src/legacy/en_US.txt (the original cspell English word list; its own README says",
        "  it is not maintained and may contain misspellings; its provenance is not documented upstream)",
        f"Version: commit {CSPELL_DICTS_COMMIT}",
        "License: MIT, Copyright Street Side Software; see third-party/THIRD-PARTY-NOTICES.txt",
        "Generation: each line split into runs of letters (as for the other cspell lists); ONLY words that SCOWL size",
        f"  {SCOWL_CLASSIFIER_SIZE} (variant level {SCOWL_CLASSIFIER_VARIANT_LEVEL}) also lists are written, and only if no earlier",
        "  source has them; words in the cspell-dicts exclude lists are not written.",
    ]
    descriptions = {
        "hunspell": hunspell_description,
        "cspell": cspell_description,
        "hunspell-legacy": hunspell_legacy_description,
        "cspell-legacy": legacy_description,
    }

    rows = []
    for region, folder in REGION_FOLDERS.items():
        name, words = base_parts[region]
        write_list(output / folder / name, english_source_header(region, base_description, len(words)), sorted(words))
        rows.append((f"{folder}/{name}", len(words), 0))
    for key, _ in sources:
        for region, folder in REGION_FOLDERS.items():
            words = results[key].get(region, [])
            path = output / folder / f"{key}.txt"
            if words:
                write_list(path, english_source_header(region, descriptions[key], len(words)), words)
                rows.append((f"{folder}/{key}.txt", len(words), 0))
            elif path.exists():
                path.unlink()
        rows.append((f"{key} rejected by the entry rule", 0, entry_drops[key]))

    rows.append(("english entries rejected (SCOWL)", 0, len(dropped)))
    print(f"cspell-dicts exclude lists: {len(excluded)} words; removed from the base lists: {base_excluded}")
    print(f"removed from other sources, by source: {dict(excluded_found)}")
    print("routing tier of every word that a source added (source, tier -> words):")
    for (key, tier), count in sorted(tiers.items()):
        print(f"  {key:<16} {tier:<12} {count:>8}")
    if dropped:
        print(f"English entries rejected by the entry rule: {len(dropped)} (first few: {dropped[:5]})")
    return rows


def verify_expander(workdir: Path) -> None:
    """Checks the Hunspell expander against the independent hunspell-reader package for every pinned dictionary."""
    node, npm = shutil.which("node"), shutil.which("npm")
    if node is None or npm is None:
        raise SystemExit("node and npm were not found on the PATH.")
    reader_dir = workdir / "hunspell-reader"
    reader_dir.mkdir(parents=True, exist_ok=True)
    run([npm, "install", "--silent", "--no-save", "--prefix", reader_dir, f"{HUNSPELL_READER_PACKAGE}@{HUNSPELL_READER_VERSION}"])
    package = json.loads((reader_dir / "node_modules" / HUNSPELL_READER_PACKAGE / "package.json").read_text(encoding="utf-8"))
    if package.get("license") != "MIT":
        raise SystemExit(f"{HUNSPELL_READER_PACKAGE} is licensed '{package.get('license')}', expected MIT.")
    reader_script = reader_dir / "node_modules" / HUNSPELL_READER_PACKAGE / package["bin"]

    cspell = fetch_cspell_english(workdir)
    dictionaries = dict(fetch_scowl_hunspell(workdir))
    dictionaries["en_US-large (cspell-dicts)"] = (cspell["hunspell-dic"], cspell["hunspell-aff"])
    failed = False
    for name, (dictionary_text, affix_text) in dictionaries.items():
        stem = name.split(" ")[0]
        folder = workdir / "verify" / re.sub(r"[^A-Za-z0-9_-]+", "-", name)
        folder.mkdir(parents=True, exist_ok=True)
        (folder / f"{stem}.dic").write_text(dictionary_text, encoding="utf-8", newline="\n")
        (folder / f"{stem}.aff").write_text(affix_text, encoding="utf-8", newline="\n")
        reference_file = folder / "reference.txt"
        run([node, reader_script, "words", folder / f"{stem}.dic", "-o", reference_file])
        reference = {line for line in reference_file.read_text(encoding="utf-8").splitlines() if line}
        table = word_forms.parse_affix_file(affix_text)
        ours = {form for _, forms in word_forms.expand_dictionary(dictionary_text, table) for form in forms}
        print(f"{name}: expander {len(ours)} forms, hunspell-reader {len(reference)} forms, "
              f"only expander {len(ours - reference)}, only reader {len(reference - ours)}")
        if ours != reference:
            failed = True
            print(f"  only expander: {sorted(ours - reference)[:20]}")
            print(f"  only reader:   {sorted(reference - ours)[:20]}")
    if failed:
        raise SystemExit("The Hunspell expander disagrees with hunspell-reader.")
    print("The Hunspell expander agrees with hunspell-reader on every dictionary.")


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
    for list_name, group, package, version, sources in CSPELL_PACKAGES:
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
            f"Technical term list ({list_name}, group {group}), imported from the cspell-dicts package {spec}.",
            "Source: https://github.com/streetsidesoftware/cspell-dicts (published npm package)",
            f"Version: {spec}; source files: {', '.join(sources)}",
            f"License: MIT, {copyright_line}",
            "  See third-party/THIRD-PARTY-NOTICES.txt for the full notice.",
            "Generation: npm pack of the package above; every entry is split into runs of letters, runs of fewer",
            "  than two letters and runs that are not ASCII/Latin-1 are dropped, then duplicates are removed.",
            "  The list is faithful to upstream: spelling variants are NOT filtered here; the dictionary builder",
            "  applies the variant rule when it combines the technical lists (see dictionaries/README.md).",
            "Generated by dictionaries/refresh.py; do not edit by hand. Add project words to general/additions.txt.",
            f"Entries: {len(words)}",
            "",
        ]
        write_list(output / "technical" / group / f"{list_name}.txt", header, sorted(words))
        rows.append((f"technical/{group}/{list_name}.txt", len(words), dropped))
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
    parser.add_argument("--verify-expander", action="store_true",
                        help="check the Hunspell expander against hunspell-reader instead of writing lists")
    arguments = parser.parse_args()

    output = Path(__file__).resolve().parent
    created_workdir = arguments.workdir is None
    workdir = (arguments.workdir or Path(tempfile.mkdtemp(prefix="textlint-dictionaries-"))).resolve()
    workdir.mkdir(parents=True, exist_ok=True)

    if arguments.verify_expander:
        verify_expander(workdir)
        return 0

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
