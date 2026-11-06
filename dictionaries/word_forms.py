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

"""Pure word-form helpers for ``refresh.py``: Hunspell affix expansion and spelling-region routing.

WHY THIS MODULE EXISTS
    ``refresh.py`` turns upstream sources into the committed English word lists. Two parts of that work
    are pure computations with no file or network access, so they live here where they can be read,
    reviewed, and exercised on their own (``python dictionaries/word_forms.py --self-test``):

    * **Hunspell expansion.** A Hunspell dictionary stores a stem plus affix flags (for example
      ``annotate/DSXGNV``). Expanding the flags with the rules of the ``.aff`` file yields every word form
      (``annotated``, ``annotates``, ``unannotated``, ...). The committed lists are plain text and the
      dictionary builder is unchanged, so the expansion happens at refresh time and the forms are written
      out as reviewable text. Malformed input is refused with an error that names the source and the line.
    * **Spelling-region routing.** The English lists are split into a common part, an American-only part,
      and a British (-ise)-only part, so that the language setting decides spelling variants such as
      ``color`` and ``colour``. Every word that is not already in the SCOWL base lists, whether it was
      generated from a stem or imported from another list, must be routed to one of the three parts, so
      that a British-only spelling never lands in the common or American list (and the reverse).

THE FOLD
    The dictionary builder and the library compare words after folding them to lower case, so a word and
    its capitalized form are one word. Routing therefore works on the folded word. When both a lower-case
    and a capitalized form of a word are listed (for example the common noun ``honor`` in the American list
    and the first name ``Honor`` in both lists), the lower-case forms decide the region, because a
    capitalized form can only be evidence about a name; otherwise a name would carry an American-only
    spelling into the British dictionary. Every case form of a word is then written to the region of its
    folded word.

ROUTING RULE (the single, documented classification rule)
    A word is routed by the first of these tests that applies, using the folded word:

    1. ``strict``: the word is in the strict SCOWL base lists (size 70, variant level 1). In both the American
       and the British list it is common; in only one it is a regional spelling of that region.
    2. ``wide``: the word is in the wider SCOWL variant lists (size 80, variant level 3, the same
       American and British spelling codes). In only one list it is a regional spelling of that region;
       in both it is common.
    3. ``counterpart``: the word is unknown to SCOWL, but rewriting it with a regional spelling pattern
       (for example ``-or`` to ``-our``, ``-ize`` to ``-ise``, ``-er`` to ``-re``) gives a word that SCOWL lists for
       exactly one region. The word is then the other region's spelling of that word.
    4. ``contained``: the word is unknown to SCOWL and no pattern applies, but it is a regional word with
       affixes: a SCOWL word of at least five letters that only one region lists, optionally preceded by a
       prefix and followed by a suffix that the Hunspell affix rules add (``un``, ``re``, ``s``, ``'s``,
       ``able``, ``ing``, ...). For example ``unmaneuverable`` is ``un`` plus ``maneuverable``, so it belongs to
       the American list. This catches words built on a regional spelling whose spelling differences the
       patterns cannot express (the several changes between ``maneuver`` and ``manoeuvre``) and every affixed
       form of a regional word that a source lists but SCOWL does not. The affixes are required because a
       regional word also occurs by accident inside unrelated words (``liter`` in ``alliteration``). The
       longest regional stem decides; if the longest ones of both regions tie, nothing is decided. A stem
       that the other region also lists (``license``) is not regional and never decides. The few genuine
       exceptions are listed, with a reason, in ``CONTAINMENT_ALLOWED``.
    5. ``hint``: none of the above decided, so the region of the source applies: a word generated from a
       regional stem inherits the region of the stem, and a word from an explicitly regional list (an
       -ize list or an -ise list) gets that list's region.
    6. ``neutral``: otherwise the word is common to both regions.

    The first test that applies wins, so SCOWL's own variant data always overrides a hint, and a hint only
    ever decides a word that SCOWL knows nothing about.

    Afterwards the ``-ise`` policy is enforced on every result: British English here is the -ise spelling
    only, so a word that ends in an -ize form (``-ize``, ``-ized``, ``-izing``, ``-ization``, ...) and whose
    -ise counterpart is a known British word cannot be British. If the result was common it becomes American;
    if it was British it is dropped (``none``).

PART OF SPEECH
    The lookup is blind to the part of speech, so a word whose British form differs only by part of speech
    (the verb ``license`` and the noun ``licence``, the noun ``practice`` and the verb ``practise``) is
    accepted in both forms for British English. That is inherent in a word list and is not a leak.
"""

from __future__ import annotations

import re
import sys
import unittest
from collections import defaultdict
from typing import Dict, FrozenSet, Iterable, Iterator, List, Optional, Set, Tuple

# ----------------------------------------------------------------------------------------------
# Errors
# ----------------------------------------------------------------------------------------------


class FormatError(ValueError):
    """Malformed Hunspell input; the message names the source and the line."""


def _fail(source: str, line_number: int, message: str) -> "FormatError":
    """Builds a FormatError whose message starts with ``source:line:`` so that the author can find the line."""
    return FormatError(f"{source}:{line_number}: {message}")


# ----------------------------------------------------------------------------------------------
# Hunspell affix expansion
# ----------------------------------------------------------------------------------------------

# Directives that would change which words exist and that this expander does not implement. A dictionary
# that uses one is refused instead of expanded wrongly; the SCOWL English dictionaries use none of them.
_UNSUPPORTED_DIRECTIVES = frozenset(
    ["FLAG", "COMPOUNDFLAG", "NEEDAFFIX", "CIRCUMFIX", "FORBIDDENWORD", "KEEPCASE", "FULLSTRIP", "AF", "AM"]
)
_PREFIX_DIRECTIVE = "PFX"
_SUFFIX_DIRECTIVE = "SFX"
_ONLY_IN_COMPOUND_DIRECTIVE = "ONLYINCOMPOUND"
_CROSS_PRODUCT_FLAGS = ("Y", "N")
_NO_AFFIX_TEXT = "0"
_HEADER_FIELD_COUNT = 4
_RULE_MIN_FIELDS = 4
_RULE_MAX_FIELDS = 6
_CONDITION_FIELD = 4
_ANY_CONDITION = "."
_FLAG_SEPARATOR = "/"
_COMMENT_MARKER = "#"
_SINGLE_CHARACTER_FLAG = 1


class AffixRule:
    """One prefix or suffix rule: what to strip from the stem, what to add, and the condition on the stem."""

    __slots__ = ("strip", "add", "condition")

    def __init__(self, strip: str, add: str, condition: "re.Pattern[str]") -> None:
        self.strip = strip
        self.add = add
        self.condition = condition


class AffixTable:
    """The prefix and suffix rules of one Hunspell ``.aff`` file, grouped by flag."""

    def __init__(self) -> None:
        self.prefixes: Dict[str, List[AffixRule]] = defaultdict(list)
        self.suffixes: Dict[str, List[AffixRule]] = defaultdict(list)
        self.prefix_cross: Dict[str, bool] = {}
        self.suffix_cross: Dict[str, bool] = {}
        self.only_in_compound_flag: Optional[str] = None


def parse_affix_file(text: str, source: str = "<affix>") -> AffixTable:
    """Parses the PFX/SFX rules and the ONLYINCOMPOUND flag of a Hunspell ``.aff`` file.

    A header line (one per flag) is ``PFX|SFX flag Y|N count`` and a rule line is
    ``PFX|SFX flag strip add [condition]``; ``0`` means "nothing" in the strip and add fields. The condition is
    a Hunspell pattern (literals, ``.``, ``[...]``, ``[^...]``), which is also valid regular-expression syntax,
    anchored to the start of the stem for a prefix and to its end for a suffix. Flags must be single
    characters. Every problem raises ``FormatError`` naming ``source`` and the line: an unsupported directive,
    a line with too few or too many fields, a header with an invalid cross-product marker or count, a rule
    without a header, an invalid condition, a continuation class, and a flag whose number of rules differs from
    its header's count.
    """
    table = AffixTable()
    declared: Dict[Tuple[str, str], Tuple[int, int]] = {}
    seen: Dict[Tuple[str, str], int] = defaultdict(int)
    for line_number, raw in enumerate(text.splitlines(), start=1):
        line = raw.split(_COMMENT_MARKER, 1)[0].strip()
        if not line:
            continue
        fields = line.split()
        directive = fields[0]
        if directive in _UNSUPPORTED_DIRECTIVES:
            raise _fail(source, line_number, f"unsupported Hunspell directive {directive}")
        if directive == _ONLY_IN_COMPOUND_DIRECTIVE:
            if len(fields) != 2:
                raise _fail(source, line_number, f"{directive} needs exactly one flag")
            table.only_in_compound_flag = fields[1]
            continue
        if directive not in (_PREFIX_DIRECTIVE, _SUFFIX_DIRECTIVE):
            continue

        # PFX and SFX lines: a header per flag, then its rules
        is_prefix = directive == _PREFIX_DIRECTIVE
        if len(fields) < _HEADER_FIELD_COUNT:
            raise _fail(source, line_number, f"{directive} line has {len(fields)} fields, at least {_RULE_MIN_FIELDS} are needed")
        flag = fields[1]
        if len(flag) != _SINGLE_CHARACTER_FLAG:
            raise _fail(source, line_number, f"flag '{flag}' is not a single character")
        key = (directive, flag)
        if len(fields) == _HEADER_FIELD_COUNT and fields[2] in _CROSS_PRODUCT_FLAGS and fields[3].isdigit():
            if key in declared:
                raise _fail(source, line_number, f"{directive} {flag} is declared twice")
            declared[key] = (int(fields[3]), line_number)
            (table.prefix_cross if is_prefix else table.suffix_cross)[flag] = fields[2] == "Y"
            continue
        if len(fields) == _HEADER_FIELD_COUNT and fields[2] in _CROSS_PRODUCT_FLAGS:
            raise _fail(source, line_number, f"the rule count '{fields[3]}' is not a number")
        if key not in declared:
            raise _fail(source, line_number, f"{directive} {flag} has a rule before its header line")
        if len(fields) > _RULE_MAX_FIELDS:
            raise _fail(source, line_number, f"{directive} rule has {len(fields)} fields, at most {_RULE_MAX_FIELDS} are allowed")
        strip = "" if fields[2] == _NO_AFFIX_TEXT else fields[2]
        add = "" if fields[3] == _NO_AFFIX_TEXT else fields[3]
        if _FLAG_SEPARATOR in add:
            raise _fail(source, line_number, "affix continuation classes are not supported")
        condition = fields[_CONDITION_FIELD] if len(fields) > _CONDITION_FIELD else _ANY_CONDITION
        try:
            pattern = re.compile(("^" + condition) if is_prefix else (condition + "$"))
        except re.error as error:
            raise _fail(source, line_number, f"invalid condition '{condition}': {error}") from error
        (table.prefixes if is_prefix else table.suffixes)[flag].append(AffixRule(strip, add, pattern))
        seen[key] += 1

    # Every flag must have exactly the number of rules its header promised, which catches a truncated file
    for key, (count, line_number) in declared.items():
        if seen[key] != count:
            raise _fail(source, line_number, f"{key[0]} {key[1]} declares {count} rules but has {seen[key]}")
    return table


def affix_strings(table: AffixTable) -> Tuple[FrozenSet[str], FrozenSet[str]]:
    """Returns the strings that the prefix rules and the suffix rules of an affix table add (never empty)."""
    prefixes = {rule.add for rules in table.prefixes.values() for rule in rules if rule.add}
    suffixes = {rule.add for rules in table.suffixes.values() for rule in rules if rule.add}
    return frozenset(prefixes), frozenset(suffixes)


def expand_stem(stem: str, flags: str, table: AffixTable) -> Set[str]:
    """Returns every word form of one stem, including the stem itself.

    Each suffix flag contributes the suffixed forms whose condition the stem satisfies; each prefix flag
    contributes the prefixed forms; and when both the prefix and the suffix allow cross products, every
    prefix is also combined with every suffixed form (``test`` gives ``untestable`` from ``un`` and
    ``able``). The conditions are tested against the stem, as Hunspell's own ``unmunch`` does.
    """
    forms: Set[str] = {stem}
    suffixed_forms: List[str] = []
    for flag in flags:
        for rule in table.suffixes.get(flag, ()):
            if rule.condition.search(stem) and stem.endswith(rule.strip):
                form = (stem[: len(stem) - len(rule.strip)] if rule.strip else stem) + rule.add
                forms.add(form)
                if table.suffix_cross.get(flag):
                    suffixed_forms.append(form)
    for flag in flags:
        for rule in table.prefixes.get(flag, ()):
            if rule.condition.search(stem) and stem.startswith(rule.strip):
                forms.add(rule.add + stem[len(rule.strip):])
                if table.prefix_cross.get(flag):
                    for form in suffixed_forms:
                        forms.add(rule.add + form[len(rule.strip):])
    return forms


def expand_dictionary(
    dictionary_text: str, table: AffixTable, source: str = "<dictionary>"
) -> Iterator[Tuple[str, FrozenSet[str]]]:
    """Expands every entry of a Hunspell ``.dic`` file, yielding ``(stem, forms)`` pairs.

    The first line is the entry count and must be a whole number; the number of entries that follow must
    equal it, which catches a truncated file. An entry is ``stem`` or ``stem/flags``, optionally followed by
    morphological fields after white space, which are ignored. A stem must not be empty, and a ``/`` must be
    followed by flags. An entry that carries the ONLYINCOMPOUND flag (the ordinal-number fragments such as
    ``1th``) is not a word on its own and is skipped. Every problem raises ``FormatError`` naming ``source``
    and the line. The count is checked after the last entry, so the error is raised when the generator is
    exhausted.
    """
    lines = dictionary_text.splitlines()
    first = next((index for index, line in enumerate(lines) if line.strip()), None)
    if first is None:
        raise _fail(source, 1, "the dictionary is empty")
    count_text = lines[first].strip()
    if not count_text.isdigit():
        raise _fail(source, first + 1, f"the first line must be the entry count, found '{count_text[:32]}'")
    entries = 0
    for index in range(first + 1, len(lines)):
        fields = lines[index].split()
        if not fields:
            continue
        entries += 1
        stem, separator, flags = fields[0].partition(_FLAG_SEPARATOR)
        if not stem:
            raise _fail(source, index + 1, "the entry has an empty stem")
        if separator and not flags:
            raise _fail(source, index + 1, f"the entry '{stem}/' has no flags after the slash")
        if table.only_in_compound_flag and table.only_in_compound_flag in flags:
            continue
        yield stem, frozenset(expand_stem(stem, flags, table))
    if entries != int(count_text):
        raise _fail(source, first + 1, f"the dictionary declares {count_text} entries but has {entries}")


# ----------------------------------------------------------------------------------------------
# Spelling-region routing
# ----------------------------------------------------------------------------------------------

REGION_COMMON = "common"
REGION_US = "US"
REGION_GB = "GB"
REGION_NONE = "none"

TIER_STRICT = "strict"
TIER_WIDE = "wide"
TIER_COUNTERPART = "counterpart"
TIER_CONTAINED = "contained"
TIER_HINT = "hint"
TIER_NEUTRAL = "neutral"
TIER_ISE_RULE = "ise-rule"

# The shortest regional word (in letters) that can decide the region of a longer word that contains it. Shorter
# stems are ordinary short words or word fragments (``tire``, ``curb``, ``plow``) that occur inside unrelated words.
MIN_CONTAINED_STEM = 5

# Words that are an affixed regional word only by accident of spelling (a genuine false positive of the containment
# test), each with the reason. A word is only ever added after a review of the data; the refresh output lists
# every word that the containment test moves, so a new false positive is seen when it appears.
CONTAINMENT_ALLOWED: Dict[str, str] = {
    "bourges": "the French city, not the plural of the SCOWL British-only word 'bourg'",
    "reprise's": "the possessive of 'reprise' (a repeated passage), not 'prise' (the British prise/prize) with a prefix",
    "deventer": "the Dutch city, not 'de' plus the SCOWL American-only word 'venter'",
    "formers": "the plural of 'former', not the SCOWL American-only word 'forme' with a suffix",
}

# Regional spelling patterns as (American, British -ise) substrings. A pattern is only ever used to
# look up the rewritten word in SCOWL's own regional lists, so a pattern that is too general cannot
# misclassify a word: it must produce a word that SCOWL lists for exactly one region. The patterns are
# the regular differences that SCOWL's variant data documents: -or/-our, -er/-re, -ize/-ise,
# -yze/-yse, -ense/-ence, -og/-ogue, -l/-ll, -ll/-l, -dgment/-dgement, e/ae, e/oe, -ow/-ough, -ck/-que,
# -um/-ium, -ay/-ey, -ire/-yre, -sk/-sc, -o/-ou, -ogram/-ogramme. The second group was added after measuring which
# single-edit differences between SCOWL's American-only and British-only words the first group did not express;
# each pattern below covers this many SCOWL word pairs that no other pattern covers: -ered/-red (accoutered/
# accoutred, 13), -ering/-ring (sceptering/sceptring, 13), -aft/-aught (draft/draught, 22), -lf/-lph (sulfur/
# sulphur, 15), -p/-pp (worshiper/worshipper, 10), -ab/-eab (likable/likeable, 13), -n/-ne (chaperon/chaperone,
# 13), -lty/-lity (specialty/speciality, 4), and -neuver/-noeuvr(e) (maneuver/manoeuvre, 16). The pairs
# -ae-/-e-, -oe-/-e-, -ense/-ence, and -yze/-yse that were asked about are already expressed by the first
# group (anemia/anaemia, estrogen/oestrogen, defense/defence, analyze/analyse). These were checked and not added:
# -ce/-se (license/licence, practice/practise) because British English accepts the -se verb form, so those
# words are not regional (a spelling check is blind to the part of speech), and -m/-mme (program/programme)
# because "program" is also British in computing and stays common (the self-check asserts it).
REGIONAL_PATTERNS: Tuple[Tuple[str, str], ...] = (
    ("or", "our"), ("er", "re"), ("iz", "is"), ("yz", "ys"), ("ense", "ence"), ("og", "ogue"),
    ("l", "ll"), ("ll", "l"), ("dgm", "dgem"), ("e", "ae"), ("e", "oe"), ("ow", "ough"), ("ck", "que"),
    ("um", "ium"), ("ay", "ey"), ("ire", "yre"), ("sk", "sc"), ("o", "ou"), ("ogram", "ogramme"),
    ("ered", "red"), ("ering", "ring"), ("aft", "aught"), ("lf", "lph"), ("p", "pp"), ("ab", "eab"),
    ("n", "ne"), ("lty", "lity"), ("neuver", "noeuvr"), ("neuver", "noeuvre"),
)

# Words that are valid in BOTH American and British English although a related spelling is regional, so a router
# that treated them as regional would be wrong: British English keeps "program" for computing, and accepts the
# verb "license" and the noun "practice" (a spelling check is blind to the part of speech). The refresh
# self-check requires each of them to be known to both dialects.
VALID_IN_BOTH_DIALECTS: Tuple[str, ...] = (
    "program", "programs", "programmer", "programming", "license", "licensed", "licensing", "practice", "practices",
)

# An -ize form at the end of a word: the verb and its inflections and the common derived nouns and adjectives.
IZE_FORM = re.compile(r"iz(e|es|ed|ing|er|ers|ation|ations|able|ability|ational|ationally)$")

# Pairs of (American, British -ise) spellings that must never cross regions; used by the refresh self-check and
# mirrored by the C# tests so that a regression in the routing is caught by a word everyone knows.
CURATED_SPELLING_PAIRS: Tuple[Tuple[str, str], ...] = (
    ("honor", "honour"), ("color", "colour"), ("center", "centre"), ("catalog", "catalogue"),
    ("labeled", "labelled"), ("organize", "organise"), ("analyze", "analyse"), ("modeling", "modelling"),
    ("judgment", "judgement"), ("favorite", "favourite"), ("defense", "defence"),
)


def _rewrites(word: str, source: str, target: str) -> Iterator[str]:
    """Yields the word with each single occurrence of ``source`` replaced by ``target``."""
    start = word.find(source)
    while start != -1:
        yield word[:start] + target + word[start + len(source):]
        start = word.find(source, start + 1)


def effective_membership(
    us_words: Iterable[str], gb_words: Iterable[str], lower_keys: Set[str]
) -> Tuple[Set[str], Set[str]]:
    """Returns the folded American and British word sets that decide regions (see THE FOLD).

    ``lower_keys`` are the folded words that have a lower-case entry anywhere in the SCOWL data. A folded word in
    that set is a member of a list only through its lower-case entries; a folded word that has only capitalized
    entries everywhere (a name) is a member through them. Sharing one set across the strict and the wide lists
    keeps the two scopes consistent: a name that is capitalized in the size-70 lists cannot contradict a
    lower-case word of the wider lists.
    """
    us_list, gb_list = list(us_words), list(gb_words)
    us_lower = {word for word in us_list if word == word.lower()}
    gb_lower = {word for word in gb_list if word == word.lower()}
    us_keys = {key for key in (word.lower() for word in us_list) if key in us_lower or key not in lower_keys}
    gb_keys = {key for key in (word.lower() for word in gb_list) if key in gb_lower or key not in lower_keys}
    return us_keys, gb_keys


def ise_counterpart(key: str) -> Optional[str]:
    """Returns the -ise spelling of a folded -ize form, or None when the word is not an -ize form."""
    match = IZE_FORM.search(key)
    return None if match is None else key[: match.start()] + "is" + match.group(1)


def known_ise_counterpart(key: str, known: Iterable[str]) -> Optional[str]:
    """Returns the known British -ise spelling that makes a folded -ize form forbidden, or None.

    The counterpart is the plain -ise spelling, or that spelling with a British -our turned to -or: the hybrid
    "glamourize" has no "glamourise" in the data, but "glamorise" is the British -ise spelling of the word.
    """
    plain = ise_counterpart(key)
    if plain is None:
        return None
    known_words = known if isinstance(known, (set, frozenset)) else set(known)
    for candidate in (plain, plain.replace("our", "or")):
        if candidate in known_words:
            return candidate
    return None


class RegionClassifier:
    """Routes words to the common, American-only, or British (-ise)-only English list (see module notes)."""

    def __init__(
        self,
        strict_us: Iterable[str],
        strict_gb: Iterable[str],
        wide_us: Iterable[str],
        wide_gb: Iterable[str],
        british_ise_forms: Iterable[str] = (),
        prefixes: Iterable[str] = (),
        suffixes: Iterable[str] = (),
    ) -> None:
        """Builds the classifier from the four SCOWL lists (any letter case) and generated -ise forms.

        ``british_ise_forms`` are additional words known to be British -ise spellings, for example the forms
        of the Hunspell British dictionary; they only widen the set of -ise counterparts that the -ise policy
        recognizes. ``prefixes`` and ``suffixes`` are the strings that the Hunspell affix rules add (see
        ``affix_strings``); they define what may surround a regional word in the containment test.
        """
        strict_us, strict_gb, wide_us, wide_gb = list(strict_us), list(strict_gb), list(wide_us), list(wide_gb)
        lower_keys = {word for words in (strict_us, strict_gb, wide_us, wide_gb) for word in words if word == word.lower()}
        self._strict_us, self._strict_gb = effective_membership(strict_us, strict_gb, lower_keys)
        self._wide_us, self._wide_gb = effective_membership(wide_us, wide_gb, lower_keys)
        # Words that SCOWL lists for exactly one region at either scope: the data a counterpart must match
        self.us_exclusive = (self._strict_us - self._strict_gb) | (self._wide_us - self._wide_gb)
        self.gb_exclusive = (self._strict_gb - self._strict_us) | (self._wide_gb - self._wide_us)
        self._british_known: FrozenSet[str] = frozenset(
            self._strict_gb | self._wide_gb | {word.lower() for word in british_ise_forms if ise_counterpart(word.lower()) is None}
        )
        self._listed = self._strict_us | self._strict_gb | self._wide_us | self._wide_gb
        # The regional stems that can decide a longer word: alphabetic regional words of at least the minimum length
        self._stems: Dict[str, str] = {}
        for stems, region in ((self.us_exclusive, REGION_US), (self.gb_exclusive, REGION_GB)):
            for stem in stems:
                if len(stem) >= MIN_CONTAINED_STEM and stem.isalpha() and stem.isascii():
                    self._stems[stem] = region
        # The strings that the Hunspell prefix and suffix rules add: what may surround a regional stem (no affixes
        # given means that the containment test is off)
        self._prefixes = frozenset(prefixes)
        self._suffixes = frozenset(suffixes)

    def is_listed(self, word: str) -> bool:
        """Returns True when SCOWL lists the word at either scope for either region (the data decides it)."""
        return word.lower() in self._listed

    def contained_region(self, word: str) -> Optional[Tuple[str, str]]:
        """Returns ``(region, stem)`` when the word is a regional SCOWL word with affixes, else None.

        The word must be ``prefix + stem + suffix`` where the stem is a SCOWL word of at least the minimum length
        that only one region lists, the prefix is empty or one of the strings that the Hunspell prefix rules add
        (``un``, ``re``, ...), and the suffix is empty or one of the strings that the Hunspell suffix rules add
        (``s``, ``'s``, ``able``, ``ing``, ...). Requiring the affixes keeps a regional word that merely occurs inside
        an unrelated word (``liter`` in ``alliteration``) from deciding it. The longest stem decides; when the
        longest stems of both regions tie, nothing is decided. A word that SCOWL lists, or that is in
        ``CONTAINMENT_ALLOWED``, is never decided here, and a stem that the other region also lists
        (``license`` for British English) is not a regional stem at all.
        """
        key = word.lower()
        if key in self._listed or key in CONTAINMENT_ALLOWED:
            return None
        best: Dict[int, Set[Tuple[str, str]]] = defaultdict(set)
        for prefix in ("", *(prefix for prefix in self._prefixes if key.startswith(prefix))):
            rest = key[len(prefix):]
            for suffix in ("", *(suffix for suffix in self._suffixes if rest.endswith(suffix))):
                core = rest[: len(rest) - len(suffix)]
                if (prefix or suffix) and core in self._stems:
                    best[len(core)].add((self._stems[core], core))
        if not best:
            return None
        found = best[max(best)]
        return None if len({region for region, _ in found}) > 1 else min(found, key=lambda item: item[1])

    @property
    def british_known(self) -> FrozenSet[str]:
        """The folded words known to be British (-ise) spellings, used to recognize -ise counterparts."""
        return self._british_known

    def strict_region(self, word: str) -> Optional[str]:
        """Returns the region that the strict SCOWL lists give a word, or None when they do not list it."""
        key = word.lower()
        in_us, in_gb = key in self._strict_us, key in self._strict_gb
        if not (in_us or in_gb):
            return None
        return REGION_COMMON if in_us and in_gb else REGION_US if in_us else REGION_GB

    def route(self, word: str, hint: Optional[str] = None) -> Tuple[str, str]:
        """Returns ``(region, tier)`` for a word; ``hint`` is the region of its source or stem, if any.

        ``hint`` is only ever the American or British region, or ``None`` for a source with no region. The result
        region is ``none`` when the word must not be written to any list (a British -ize form).
        """
        key = word.lower()
        # A form generated from a British stem that is an -ize form with a known -ise counterpart is a hybrid
        # (for example "colourize"): it is neither British (-ise only) nor American, so no data test may claim it
        if hint == REGION_GB and known_ise_counterpart(key, self._british_known) is not None:
            return REGION_NONE, TIER_ISE_RULE
        region, tier = self._route_by_data(key, hint)
        return self._apply_ise_policy(key, region, tier)

    def _route_by_data(self, key: str, hint: Optional[str]) -> Tuple[str, str]:
        """Applies tests 1-6 of the routing rule to a folded word."""
        in_us, in_gb = key in self._strict_us, key in self._strict_gb
        if in_us or in_gb:
            return (REGION_COMMON if in_us and in_gb else REGION_US if in_us else REGION_GB), TIER_STRICT
        in_us, in_gb = key in self._wide_us, key in self._wide_gb
        if in_us or in_gb:
            return (REGION_COMMON if in_us and in_gb else REGION_US if in_us else REGION_GB), TIER_WIDE
        for american, british in REGIONAL_PATTERNS:
            if any(candidate in self.gb_exclusive for candidate in _rewrites(key, american, british)):
                return REGION_US, TIER_COUNTERPART
            if any(candidate in self.us_exclusive for candidate in _rewrites(key, british, american)):
                return REGION_GB, TIER_COUNTERPART
        contained = self.contained_region(key)
        if contained is not None:
            return contained[0], TIER_CONTAINED
        if hint is not None:
            return hint, TIER_HINT
        return REGION_COMMON, TIER_NEUTRAL

    def _apply_ise_policy(self, key: str, region: str, tier: str) -> Tuple[str, str]:
        """Enforces that British English is -ise only: an -ize form with a known -ise counterpart is not British."""
        if region in (REGION_COMMON, REGION_GB):
            if known_ise_counterpart(key, self._british_known) is not None:
                return (REGION_US if region == REGION_COMMON else REGION_NONE), TIER_ISE_RULE
        return region, tier

    def is_in_wide_scope(self, word: str) -> bool:
        """Returns True when the wider SCOWL variant lists (either region) list the word.

        Used to admit words from lists that are otherwise not trusted: a word is accepted only when SCOWL,
        a curated and documented source, also lists it.
        """
        key = word.lower()
        return key in self._wide_us or key in self._wide_gb

    def stem_hint(self, stem: str) -> Optional[str]:
        """Returns the region that forms generated from ``stem`` inherit, or ``None`` for a common stem."""
        region, _ = self.route(stem)
        return None if region in (REGION_COMMON, REGION_NONE) else region

    def check_regions(
        self,
        common: Iterable[str],
        american: Iterable[str],
        british: Iterable[str],
        pairs: Iterable[Tuple[str, str]] = CURATED_SPELLING_PAIRS,
        shared: Iterable[str] = VALID_IN_BOTH_DIALECTS,
    ) -> List[str]:
        """Checks finished region lists and returns a description of every violation (empty when sound).

        The lists are checked on folded words, the way the builder combines them. A violation is: a known
        British-only spelling in the common or American list; a known American-only spelling in the common or
        British list; an unlisted word that contains a regional SCOWL word (the containment test of the routing rule)
        but is in the other region's dictionary; an -ize form in the British dictionary whose -ise counterpart is
        a known British word; or
        a curated spelling pair that is not American-only (American spelling) and British-only (British spelling).
        """
        common_keys = {word.lower() for word in common}
        american_keys = {word.lower() for word in american}
        british_keys = {word.lower() for word in british}
        american_dictionary = common_keys | american_keys
        british_dictionary = common_keys | british_keys
        problems: List[str] = []
        for key in sorted(american_dictionary & self.gb_exclusive):
            problems.append(f"'{key}' is a British-only spelling but is in the common or American list")
        for key in sorted(british_dictionary & self.us_exclusive):
            problems.append(f"'{key}' is an American-only spelling but is in the common or British list")
        for key in sorted(american_dictionary):
            contained = self.contained_region(key)
            if contained is not None and contained[0] == REGION_GB:
                problems.append(f"'{key}' contains the British-only word '{contained[1]}' but is in the common or American list")
        for key in sorted(british_dictionary):
            contained = self.contained_region(key)
            if contained is not None and contained[0] == REGION_US:
                problems.append(f"'{key}' contains the American-only word '{contained[1]}' but is in the common or British list")
        british_known = self._british_known | british_dictionary
        for key in sorted(british_dictionary):
            counterpart = known_ise_counterpart(key, british_known)
            if counterpart is not None:
                problems.append(f"'{key}' is an -ize form with the -ise counterpart '{counterpart}' in British English")
        for american_spelling, british_spelling in pairs:
            if american_spelling not in american_dictionary or american_spelling in british_dictionary:
                problems.append(f"'{american_spelling}' must be American-only")
            if british_spelling not in british_dictionary or british_spelling in american_dictionary:
                problems.append(f"'{british_spelling}' must be British-only")
        for word in shared:
            if word not in american_dictionary or word not in british_dictionary:
                problems.append(f"'{word}' is valid in both American and British English but is missing from one")
        return problems


# ----------------------------------------------------------------------------------------------
# Self-test
# ----------------------------------------------------------------------------------------------

_SAMPLE_AFFIX = """\
ONLYINCOMPOUND c
PFX U Y 1
PFX U 0 un .
SFX B Y 2
SFX B 0 able [^aeiou]
SFX B e able [^aeiou]e
SFX D Y 3
SFX D 0 d e
SFX D y ied [^aeiou]y
SFX D 0 ed [^ey]
"""
_SAMPLE_DICTIONARY = "4\ntest/UBD\nrate/BD\nhappy/D\n1th/tc\n"


class _SelfTests(unittest.TestCase):
    """Checks the expander, its input validation, and the router; run with ``--self-test``."""

    def test_expansion_includes_stem_suffixes_prefixes_and_cross_products(self) -> None:
        table = parse_affix_file(_SAMPLE_AFFIX, "sample.aff")
        forms = dict(expand_dictionary(_SAMPLE_DICTIONARY, table, "sample.dic"))
        self.assertEqual({"test", "testable", "tested", "untest", "untestable", "untested"}, forms["test"])
        self.assertEqual({"rate", "ratable", "rated"}, forms["rate"])
        self.assertEqual({"happy", "happied"}, forms["happy"])
        self.assertNotIn("1th", forms)

    def test_malformed_affix_files_are_refused_with_source_and_line(self) -> None:
        cases = [
            ("ONLYINCOMPOUND\n", "sample.aff:1: ONLYINCOMPOUND needs exactly one flag"),
            ("PFX\n", "sample.aff:1: PFX line has 1 fields"),
            ("PFX U Y\n", "sample.aff:1: PFX line has 3 fields"),
            ("SFX B Y many\n", "sample.aff:1: the rule count 'many' is not a number"),
            ("SFX B 0 able .\n", "sample.aff:1: SFX B has a rule before its header line"),
            ("SFX B Y 2\nSFX B 0 able .\n", "sample.aff:1: SFX B declares 2 rules but has 1"),
            ("SFX B Y 1\nSFX B Y 1\n", "sample.aff:2: SFX B is declared twice"),
            ("SFX BB Y 1\n", "sample.aff:1: flag 'BB' is not a single character"),
            ("SFX B Y 1\nSFX B 0 able [\n", "sample.aff:2: invalid condition"),
            ("SFX B Y 1\nSFX B 0 able/X .\n", "sample.aff:2: affix continuation classes are not supported"),
            ("SFX B Y 1\nSFX B 0 able . x y z\n", "sample.aff:2: SFX rule has 8 fields"),
            ("COMPOUNDFLAG x\n", "sample.aff:1: unsupported Hunspell directive COMPOUNDFLAG"),
        ]
        for text, expected in cases:
            with self.subTest(text=text):
                with self.assertRaises(FormatError) as raised:
                    parse_affix_file(text, "sample.aff")
                self.assertIn(expected, str(raised.exception))

    def test_malformed_dictionaries_are_refused_with_source_and_line(self) -> None:
        table = parse_affix_file(_SAMPLE_AFFIX, "sample.aff")
        cases = [
            ("", "sample.dic:1: the dictionary is empty"),
            ("many\ntest\n", "sample.dic:1: the first line must be the entry count"),
            ("2\ntest\n", "sample.dic:1: the dictionary declares 2 entries but has 1"),
            ("1\n/UB\n", "sample.dic:2: the entry has an empty stem"),
            ("1\ntest/\n", "sample.dic:2: the entry 'test/' has no flags"),
        ]
        for text, expected in cases:
            with self.subTest(text=text):
                with self.assertRaises(FormatError) as raised:
                    list(expand_dictionary(text, table, "sample.dic"))
                self.assertIn(expected, str(raised.exception))

    def test_capitalized_name_cannot_carry_a_regional_spelling_across(self) -> None:
        american = ["honor", "Honor", "color"]
        british = ["Honor", "honour", "colour"]
        classifier = RegionClassifier(american, british, american, british)
        self.assertEqual(REGION_US, classifier.route("honor")[0])
        self.assertEqual(REGION_US, classifier.route("Honor")[0])
        self.assertEqual(REGION_GB, classifier.route("honour")[0])
        self.assertEqual(REGION_US, classifier.strict_region("HONOR"))

    def test_a_name_with_no_lower_case_form_is_decided_by_its_capitalized_forms(self) -> None:
        classifier = RegionClassifier(["Paris"], ["Paris"], [], [])
        self.assertEqual(REGION_COMMON, classifier.route("paris")[0])

    def test_ise_policy_drops_british_ize_forms_and_makes_common_ize_forms_american(self) -> None:
        american = ["organize", "ionizer", "size", "sized"]
        british = ["organise", "ioniser", "ionizer", "size", "sized", "colourise"]
        classifier = RegionClassifier(american, british, american, british)
        self.assertEqual((REGION_NONE, TIER_ISE_RULE), classifier.route("colourize", REGION_GB))
        self.assertEqual((REGION_US, TIER_ISE_RULE), classifier.route("ionizer"))
        self.assertEqual(REGION_COMMON, classifier.route("size")[0])
        self.assertEqual(REGION_COMMON, classifier.route("sized")[0])

    def test_ise_counterpart_may_use_the_or_spelling_of_an_our_word(self) -> None:
        british = ["glamorise", "glamour"]
        classifier = RegionClassifier(["glamorize"], british, ["glamorize"], british)
        self.assertEqual("glamorise", known_ise_counterpart("glamourize", classifier.british_known))
        self.assertIsNone(known_ise_counterpart("sized", classifier.british_known))
        self.assertEqual((REGION_NONE, TIER_ISE_RULE), classifier.route("glamourize", REGION_GB))

    def test_check_regions_reports_leaks_and_passes_a_sound_split(self) -> None:
        american = ["color", "honor", "organize"]
        british = ["colour", "honour", "organise"]
        classifier = RegionClassifier(american, british, american, british)
        pairs = (("color", "colour"), ("honor", "honour"), ("organize", "organise"))
        self.assertEqual([], classifier.check_regions(["table"], ["color", "honor", "organize"], ["colour", "honour", "organise"], pairs, ()))
        leaked = classifier.check_regions(["table", "honor"], ["color", "organize"], ["colour", "honour", "organise"], pairs, ())
        self.assertTrue(any("'honor' must be American-only" in problem for problem in leaked))
        british_ize = classifier.check_regions(["table"], ["color", "honor", "organize"], ["colour", "honour", "organise", "colourise", "colourize"], pairs, ())
        self.assertTrue(any("-ize form" in problem for problem in british_ize))

    def test_check_regions_requires_the_words_valid_in_both_dialects_to_be_in_both(self) -> None:
        classifier = RegionClassifier(["program"], ["program"], ["program"], ["program"])
        self.assertEqual([], classifier.check_regions(["program"], [], [], (), ("program",)))
        problems = classifier.check_regions([], ["program"], ["programme"], (), ("program",))
        self.assertEqual(["'program' is valid in both American and British English but is missing from one"], problems)

    def test_affix_strings_collects_what_the_prefix_and_suffix_rules_add(self) -> None:
        prefixes, suffixes = affix_strings(parse_affix_file(_SAMPLE_AFFIX))
        self.assertEqual(frozenset(["un"]), prefixes)
        self.assertEqual(frozenset(["able", "d", "ied", "ed"]), suffixes)

    @staticmethod
    def _affixed_classifier() -> RegionClassifier:
        american = ["maneuverable", "liter", "licenses", "forme"]
        british = ["manoeuvrable", "litre", "licences", "formes"]
        return RegionClassifier(american, british, american, british, (), ["un", "re"], ["s", "'s", "able", "ing"])

    def test_a_regional_word_with_affixes_decides_the_region_of_an_unlisted_word(self) -> None:
        classifier = self._affixed_classifier()
        self.assertEqual((REGION_US, TIER_CONTAINED), classifier.route("unmaneuverable"))
        self.assertEqual((REGION_GB, TIER_CONTAINED), classifier.route("Unmanoeuvrable"))
        self.assertEqual((REGION_US, TIER_CONTAINED), classifier.route("remaneuverable's"))
        self.assertEqual((REGION_US, "maneuverable"), classifier.contained_region("unmaneuverable"))

    def test_containment_needs_the_affixes_and_an_unlisted_word(self) -> None:
        classifier = self._affixed_classifier()
        # "alliteration" contains "liter" but is not liter plus an affix, and "x" is not an affix; a listed word is
        # decided by SCOWL's own data, and without any affixes the test is off
        self.assertEqual((REGION_COMMON, TIER_NEUTRAL), classifier.route("alliteration"))
        self.assertEqual((REGION_COMMON, TIER_NEUTRAL), classifier.route("xmaneuverable"))
        self.assertEqual((REGION_US, TIER_STRICT), classifier.route("maneuverable"))
        self.assertIsNone(RegionClassifier(["maneuverable"], ["manoeuvrable"], [], []).contained_region("unmaneuverable"))

    def test_a_stem_that_both_regions_list_never_decides(self) -> None:
        shared = ["license", "practice"]
        classifier = RegionClassifier(shared, shared, [], [], (), ["un"], ["s", "ing"])
        self.assertEqual((REGION_COMMON, TIER_NEUTRAL), classifier.route("unlicense"))
        self.assertEqual((REGION_COMMON, TIER_NEUTRAL), classifier.route("practicing"))

    def test_a_tie_between_the_regions_decides_nothing(self) -> None:
        # "abcdef" is the prefix "a" and the American word "bcdef", and also the British word "abcde" and the suffix "f"
        classifier = RegionClassifier(["bcdef"], ["abcde"], [], [], (), ["a"], ["f"])
        self.assertIsNone(classifier.contained_region("abcdef"))
        self.assertEqual((REGION_COMMON, TIER_NEUTRAL), classifier.route("abcdef"))

    def test_listed_exceptions_are_not_moved_but_their_other_forms_are(self) -> None:
        classifier = RegionClassifier(["venter"], ["vented"], ["venter"], ["vented"], (), ["de"], ["s"])
        self.assertEqual((REGION_COMMON, TIER_NEUTRAL), classifier.route("deventer"))
        self.assertEqual((REGION_US, TIER_CONTAINED), classifier.route("deventers"))

    def test_check_regions_reports_an_unlisted_word_that_contains_a_regional_word_in_the_wrong_list(self) -> None:
        classifier = self._affixed_classifier()
        self.assertEqual(
            ["'unmaneuverable' contains the American-only word 'maneuverable' but is in the common or British list"],
            classifier.check_regions(["unmaneuverable"], [], [], (), ()),
        )
        self.assertEqual(
            ["'unmanoeuvrable' contains the British-only word 'manoeuvrable' but is in the common or American list"],
            classifier.check_regions(["unmanoeuvrable"], [], [], (), ()),
        )
        self.assertEqual([], classifier.check_regions([], ["unmaneuverable"], ["unmanoeuvrable"], (), ()))

    def test_the_added_spelling_patterns_find_the_other_regions_spelling(self) -> None:
        british = ["accoutred", "sulphur", "worshipper", "likeable", "chaperone", "speciality", "manoeuvre", "draught"]
        american = ["accoutered", "sulfur", "worshiper", "likable", "chaperon", "specialty", "maneuver", "draft"]
        classifier = RegionClassifier([], british, [], british)
        for word in american:
            self.assertEqual((REGION_US, TIER_COUNTERPART), classifier.route(word), word)
        reverse = RegionClassifier(american, [], american, [])
        for word in british:
            self.assertEqual((REGION_GB, TIER_COUNTERPART), reverse.route(word), word)


def self_test_suite() -> unittest.TestSuite:
    """Returns the self-tests of this module as a suite (so ``refresh.py`` can run them with its own)."""
    return unittest.defaultTestLoader.loadTestsFromTestCase(_SelfTests)


def self_test() -> int:
    """Runs the self-tests and returns the process exit code (0 when every test passes)."""
    result = unittest.TextTestRunner(verbosity=1).run(self_test_suite())
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    if sys.argv[1:] == ["--self-test"]:
        sys.exit(self_test())
    print("usage: python dictionaries/word_forms.py --self-test", file=sys.stderr)
    sys.exit(2)
