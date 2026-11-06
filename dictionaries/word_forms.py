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
    reviewed, and exercised on their own:

    * **Hunspell expansion.** A Hunspell dictionary stores a stem plus affix flags (for example
      ``annotate/DSXGNV``). Expanding the flags with the rules of the ``.aff`` file yields every word form
      (``annotated``, ``annotates``, ``unannotated``, ...). The committed lists are plain text and the
      dictionary builder is unchanged, so the expansion happens at refresh time and the forms are written
      out as reviewable text.
    * **Spelling-region routing.** The English lists are split into a common part, an American-only part,
      and a British (-ise)-only part, so that the language setting decides spelling variants such as
      ``color`` and ``colour``. Every word that is not already in the SCOWL base lists, whether it was
      generated from a stem or imported from another list, must be routed to one of the three parts, so
      that a British-only spelling never lands in the common or American list (and the reverse).

ROUTING RULE (the single, documented classification rule)
    A word is routed by the first of these tests that applies, using lower-cased comparison:

    1. ``strict``: the word is in the strict SCOWL base lists (size 70, variant level 1). In both the
       American and the British list it is common; in only one it is a regional spelling of that region.
    2. ``wide``: the word is in the wider SCOWL variant lists (size 80, variant level 3, the same
       American and British spelling codes). In only one list it is a regional spelling of that region;
       in both it is common.
    3. ``counterpart``: the word is unknown to SCOWL, but rewriting it with a regional spelling pattern
       (for example ``-or`` to ``-our``, ``-ize`` to ``-ise``, ``-er`` to ``-re``) gives a word that SCOWL
       lists for exactly one region. The word is then the other region's spelling of that word.
    4. ``hint``: none of the above decided, so the region of the source applies: a word generated from a
       regional stem inherits the region of the stem, and a word from an explicitly regional list (an
       -ize list or an -ise list) gets that list's region.
    5. ``neutral``: otherwise the word is common to both regions.

    The first test that applies wins, so SCOWL's own variant data always overrides a hint, and a hint only
    ever decides a word that SCOWL knows nothing about.
"""

from __future__ import annotations

import re
from collections import defaultdict
from typing import Dict, FrozenSet, Iterable, Iterator, List, Optional, Set, Tuple

# ----------------------------------------------------------------------------------------------
# Hunspell affix expansion
# ----------------------------------------------------------------------------------------------

# Names of the Hunspell directives this expander understands. Any other directive that would change
# which words exist (for example FLAG, COMPOUNDFLAG, NEEDAFFIX, CIRCUMFIX, FORBIDDENWORD, KEEPCASE,
# affix continuation classes) makes the expander refuse the file instead of silently producing wrong
# forms: the SCOWL English dictionaries use none of them.
_UNSUPPORTED_DIRECTIVES = frozenset(
    ["FLAG", "COMPOUNDFLAG", "NEEDAFFIX", "CIRCUMFIX", "FORBIDDENWORD", "KEEPCASE", "FULLSTRIP", "AF", "AM"]
)
_PREFIX_DIRECTIVE = "PFX"
_SUFFIX_DIRECTIVE = "SFX"
_ONLY_IN_COMPOUND_DIRECTIVE = "ONLYINCOMPOUND"
_CROSS_PRODUCT_YES = "Y"
_NO_AFFIX_TEXT = "0"
_AFFIX_HEADER_FIELDS = 4
_AFFIX_RULE_MIN_FIELDS = 4
_AFFIX_CONDITION_FIELD = 4
_ANY_CONDITION = "."
_FLAG_SEPARATOR = "/"
_COMMENT_MARKER = "#"


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


def parse_affix_file(text: str) -> AffixTable:
    """Parses the PFX/SFX rules and the ONLYINCOMPOUND flag of a Hunspell ``.aff`` file.

    A rule line is ``PFX|SFX flag strip add condition`` and a header line (one per flag) is
    ``PFX|SFX flag cross-product count``. ``0`` means "nothing" in the strip and add fields. The
    condition is a Hunspell pattern (literals, ``.``, ``[...]``, ``[^...]``), which is also valid
    regular-expression syntax, anchored to the start of the stem for a prefix and to its end for a suffix.
    Raises ``ValueError`` for a directive the expander does not support, so a future dictionary that needs
    it fails loudly.
    """
    table = AffixTable()
    for raw in text.splitlines():
        line = raw.split(_COMMENT_MARKER, 1)[0].strip()
        if not line:
            continue
        fields = line.split()
        directive = fields[0]
        if directive in _UNSUPPORTED_DIRECTIVES:
            raise ValueError(f"unsupported Hunspell directive: {directive}")
        if directive == _ONLY_IN_COMPOUND_DIRECTIVE:
            table.only_in_compound_flag = fields[1]
            continue
        if directive not in (_PREFIX_DIRECTIVE, _SUFFIX_DIRECTIVE):
            continue
        is_prefix = directive == _PREFIX_DIRECTIVE
        flag = fields[1]
        if len(fields) == _AFFIX_HEADER_FIELDS and fields[2] in ("Y", "N"):
            (table.prefix_cross if is_prefix else table.suffix_cross)[flag] = fields[2] == _CROSS_PRODUCT_YES
            continue
        if len(fields) < _AFFIX_RULE_MIN_FIELDS:
            raise ValueError(f"malformed affix rule: {raw!r}")
        strip = "" if fields[2] == _NO_AFFIX_TEXT else fields[2]
        add = "" if fields[3] == _NO_AFFIX_TEXT else fields[3]
        if _FLAG_SEPARATOR in add:
            raise ValueError(f"unsupported affix continuation classes: {raw!r}")
        condition = fields[_AFFIX_CONDITION_FIELD] if len(fields) > _AFFIX_CONDITION_FIELD else _ANY_CONDITION
        pattern = re.compile(("^" + condition) if is_prefix else (condition + "$"))
        (table.prefixes if is_prefix else table.suffixes)[flag].append(AffixRule(strip, add, pattern))
    return table


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


def expand_dictionary(dictionary_text: str, table: AffixTable) -> Iterator[Tuple[str, FrozenSet[str]]]:
    """Expands every entry of a Hunspell ``.dic`` file, yielding ``(stem, forms)`` pairs.

    The first line (the entry count) is skipped. An entry is ``stem`` or ``stem/flags``, optionally
    followed by morphological fields after white space, which are ignored. An entry that carries the
    ONLYINCOMPOUND flag (the ordinal-number fragments such as ``1th``) is not a word on its own and is
    skipped. Duplicate stems are expanded independently, as Hunspell does.
    """
    lines = dictionary_text.splitlines()
    for line in lines[1:]:
        fields = line.split()
        if not fields:
            continue
        stem, _, flags = fields[0].partition(_FLAG_SEPARATOR)
        if table.only_in_compound_flag and table.only_in_compound_flag in flags:
            continue
        yield stem, frozenset(expand_stem(stem, flags, table))


# ----------------------------------------------------------------------------------------------
# Spelling-region routing
# ----------------------------------------------------------------------------------------------

REGION_COMMON = "common"
REGION_US = "US"
REGION_GB = "GB"

TIER_STRICT = "strict"
TIER_WIDE = "wide"
TIER_COUNTERPART = "counterpart"
TIER_HINT = "hint"
TIER_NEUTRAL = "neutral"

# Regional spelling patterns as (American, British -ise) substrings. A pattern is only ever used to
# look up the rewritten word in SCOWL's own regional lists, so a pattern that is too general cannot
# misclassify a word: it must produce a word that SCOWL lists for exactly one region. The patterns are
# the regular differences that SCOWL's variant data documents: -or/-our, -er/-re, -ize/-ise,
# -yze/-yse, -ense/-ence, -og/-ogue, -l/-ll, -ll/-l, -dgment/-dgement, e/ae, e/oe, -ow/-ough, -ck/-que,
# -um/-ium, -ay/-ey, -ire/-yre, -sk/-sc, -o/-ou, -ogram/-ogramme.
REGIONAL_PATTERNS: Tuple[Tuple[str, str], ...] = (
    ("or", "our"), ("er", "re"), ("iz", "is"), ("yz", "ys"), ("ense", "ence"), ("og", "ogue"),
    ("l", "ll"), ("ll", "l"), ("dgm", "dgem"), ("e", "ae"), ("e", "oe"), ("ow", "ough"), ("ck", "que"),
    ("um", "ium"), ("ay", "ey"), ("ire", "yre"), ("sk", "sc"), ("o", "ou"), ("ogram", "ogramme"),
)


def _rewrites(word: str, source: str, target: str) -> Iterator[str]:
    """Yields the word with each single occurrence of ``source`` replaced by ``target``."""
    start = word.find(source)
    while start != -1:
        yield word[:start] + target + word[start + len(source):]
        start = word.find(source, start + 1)


class RegionClassifier:
    """Routes words to the common, American-only, or British (-ise)-only English list (see module notes)."""

    def __init__(
        self,
        strict_us: Iterable[str],
        strict_gb: Iterable[str],
        wide_us: Iterable[str],
        wide_gb: Iterable[str],
    ) -> None:
        """Builds the classifier from the four SCOWL lists (any letter case; compared lower-cased)."""
        self._strict_us = {word.lower() for word in strict_us}
        self._strict_gb = {word.lower() for word in strict_gb}
        self._wide_us = {word.lower() for word in wide_us}
        self._wide_gb = {word.lower() for word in wide_gb}
        # Words that SCOWL lists for exactly one region at either scope: the data a counterpart must match
        self._us_exclusive = (self._strict_us - self._strict_gb) | (self._wide_us - self._wide_gb)
        self._gb_exclusive = (self._strict_gb - self._strict_us) | (self._wide_gb - self._wide_us)

    def route(self, word: str, hint: Optional[str] = None) -> Tuple[str, str]:
        """Returns ``(region, tier)`` for a word; ``hint`` is the region of its source or stem, if any.

        ``hint`` is only ever the American or British region, or ``None`` for a source with no region.
        """
        key = word.lower()

        # Tier 1: the strict SCOWL base lists decide first
        in_us, in_gb = key in self._strict_us, key in self._strict_gb
        if in_us or in_gb:
            return (REGION_COMMON if in_us and in_gb else REGION_US if in_us else REGION_GB), TIER_STRICT

        # Tier 2: the wider SCOWL variant lists
        in_us, in_gb = key in self._wide_us, key in self._wide_gb
        if in_us or in_gb:
            return (REGION_COMMON if in_us and in_gb else REGION_US if in_us else REGION_GB), TIER_WIDE

        # Tier 3: a regional counterpart that SCOWL lists for exactly one region
        for american, british in REGIONAL_PATTERNS:
            if any(candidate in self._gb_exclusive for candidate in _rewrites(key, american, british)):
                return REGION_US, TIER_COUNTERPART
            if any(candidate in self._us_exclusive for candidate in _rewrites(key, british, american)):
                return REGION_GB, TIER_COUNTERPART

        # Tiers 4 and 5: the region of the source, or common when the source has none
        if hint is not None:
            return hint, TIER_HINT
        return REGION_COMMON, TIER_NEUTRAL

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
        return None if region == REGION_COMMON else region
