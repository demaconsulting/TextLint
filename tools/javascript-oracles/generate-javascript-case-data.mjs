// generate-javascript-case-data.mjs
//
// PURPOSE:
//   Generates the table of letters that the JavaScript engine treats as the same letter when a regular expression
//   ignores case, which the translator of JavaScript patterns (JavaScriptCaseFolding) uses so that the case behavior of a
//   pattern is the same on every platform and every .NET version. The table is recorded from a pinned Node.js, the
//   JavaScript engine that cspell runs in, and is NOT computed from the character data of the .NET runtime, whose casing
//   differs between runtime versions and platforms. The script is NOT run by the build or by CI; run it with Node.js
//   whenever the table should be refreshed, for example for a newer Unicode version:
//
//   It finds the repository root from its own location, so it can be run from any directory:
//
//       node tools/javascript-oracles/generate-javascript-case-data.mjs
//
//   It writes two files that must be committed together:
//     * src/DemaConsulting.TextLint/TextScanning/JavaScriptCaseData.cs - the table the library embeds, with the Node.js
//       and Unicode versions it was recorded from in its header and in two constants;
//     * test/DemaConsulting.TextLint.Tests/TextScanning/Data/javascript-case-groups.txt - the same groups in a text form
//       that the unit tests compare with the table that the library actually uses.
//
// HOW IT WORKS:
//   Two code points are the same letter when the expression /^X$/i (without the u flag, for code units) or /^X$/iu (with
//   it, for code points) built from one matches the other. The script collects the characters that change in a JavaScript
//   case mapping, and the characters those mappings lead to, tests every pair of them with the engine, and joins the
//   characters that match into groups. (Testing every pair, and not only the characters one mapping reaches, finds the
//   relations of simple case folding that no toLowerCase or toUpperCase call shows, such as U+1FD3 with U+0390.) It then
//   checks that every pair inside a group matches, and that no group mixes characters of the basic plane with astral
//   ones, which the library relies on. Only groups with more than one member are written. The mode without the u flag has
//   code units only (JavaScript reads a character outside the basic plane as two code units without that flag), and the
//   mode with the flag has code points.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repository = path.resolve(here, '..', '..');
const generatedSource = path.join(repository, 'src', 'DemaConsulting.TextLint', 'TextScanning', 'JavaScriptCaseData.cs');
const referenceFile = path.join(repository, 'test', 'DemaConsulting.TextLint.Tests', 'TextScanning', 'Data', 'javascript-case-groups.txt');

const hex = (cp) => cp.toString(16).toUpperCase().padStart(4, '0');
const surrogate = (cp) => cp >= 0xd800 && cp <= 0xdfff;

function groups(unicode) {
    const parent = new Map();
    const find = (x) => {
        while (parent.get(x) !== x) {
            parent.set(x, parent.get(parent.get(x)));
            x = parent.get(x);
        }
        return x;
    };
    const union = (a, b) => {
        if (!parent.has(a)) parent.set(a, a);
        if (!parent.has(b)) parent.set(b, b);
        const ra = find(a);
        const rb = find(b);
        if (ra !== rb) parent.set(ra, rb);
    };
    const escape = (cp) => (unicode ? String.raw`\u{${cp.toString(16)}}` : String.raw`\u${cp.toString(16).padStart(4, '0')}`);
    const flags = unicode ? 'iu' : 'i';
    const limit = unicode ? 0x110000 : 0x10000;

    // The characters that can have a case relation: those that change in a case mapping (including the ones whose full
    // mapping has several characters, which a simple case folding may still relate to another character), and every
    // single character that a mapping leads to
    const cased = new Set();
    for (let c = 0; c < limit; c++) {
        if (surrogate(c)) continue;
        const s = String.fromCodePoint(c);
        const lower = s.toLowerCase();
        const upper = s.toUpperCase();
        if (lower === s && upper === s) continue;
        cased.add(c);
        for (const t of [lower, upper]) {
            const chars = [...t];
            if (chars.length === 1) {
                const d = chars[0].codePointAt(0);
                if (d < limit) cased.add(d);
            }
        }
    }

    // Compare every pair of them with the engine itself; the characters of one group all match each other
    const list = [...cased].sort((a, b) => a - b);
    for (const c of list) {
        const regex = new RegExp(`^${escape(c)}$`, flags);
        for (const d of list) {
            if (d > c && regex.test(String.fromCodePoint(d))) union(c, d);
        }
    }
    const byRoot = new Map();
    for (const x of parent.keys()) {
        const r = find(x);
        if (!byRoot.has(r)) byRoot.set(r, []);
        byRoot.get(r).push(x);
    }
    const result = [...byRoot.values()].map((g) => g.sort((a, b) => a - b)).filter((g) => g.length > 1).sort((a, b) => a[0] - b[0]);

    // The relation must be an equivalence: every pair inside a group matches, so the grouping lost nothing
    for (const g of result) {
        for (const c of g) {
            const regex = new RegExp(`^${escape(c)}$`, flags);
            for (const d of g) {
                if (c !== d && !regex.test(String.fromCodePoint(d))) {
                    throw new Error(`U+${hex(c)} and U+${hex(d)} are in one group but the engine does not match them (${flags}).`);
                }
            }
        }
    }

    for (const g of result) {
        if (g.some((cp) => cp > 0xffff) && g.some((cp) => cp <= 0xffff)) {
            throw new Error(`A group mixes basic-plane and astral characters: ${g.map(hex).join(' ')}`);
        }
    }

    return result;
}

const modes = [
    ['nonunicode', groups(false)],
    ['unicode', groups(true)],
];
const version = process.version;
const unicodeVersion = process.versions.unicode;
const icuVersion = process.versions.icu;
const line = (g) => g.map(hex).join(' ');

// The reference file for the unit tests
const reference = [
    '# cspell:disable',
    '# The groups of characters that the JavaScript engine treats as the same letter when a regular expression ignores',
    `# case, recorded from Node.js ${version} (Unicode ${unicodeVersion}, ICU ${icuVersion}) by generate-javascript-case-data.mjs. One group per`,
    '# line: the mode (nonunicode for the i flag, with UTF-16 code units; unicode for the iu flags, with code points), a colon,',
    '# and the characters in hexadecimal. Groups with one member are not listed. Do not edit by hand.',
];
for (const [mode, list] of modes) {
    for (const g of list) reference.push(`${mode}: ${line(g)}`);
}
fs.writeFileSync(referenceFile, reference.join('\n') + '\n');

// The table the library embeds
const license = fs
    .readFileSync(path.join(repository, 'src', 'DemaConsulting.TextLint', 'TextScanning', 'JavaScriptSyntax.cs'), 'utf8')
    .split(/\r?\n/)
    .slice(0, 18)
    .join('\n');
const source = `${license}

// <auto-generated>
//   Generated by generate-javascript-case-data.mjs from Node.js ${version} (Unicode ${unicodeVersion}, ICU ${icuVersion}).
//   Do not edit by hand: run the script again instead.
// </auto-generated>

// cspell:disable

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     The groups of characters that JavaScript treats as the same letter when a regular expression ignores case,
///     recorded from a pinned JavaScript engine so that the result does not depend on the .NET runtime.
/// </summary>
/// <remarks>
///     One group per line, the characters in hexadecimal separated by spaces. A group is a set of characters that
///     the JavaScript engine matches with each other under the flag in question; characters with no case
///     relation are not listed. Generated; see <c>generate-javascript-case-data.mjs</c>. Constants only, so the class
///     is thread-safe.
/// </remarks>
internal static class JavaScriptCaseData
{
    /// <summary>
    ///     The version of Node.js that the groups were recorded from.
    /// </summary>
    internal const string NodeVersion = "${version}";

    /// <summary>
    ///     The version of Unicode that the JavaScript engine of the recording used.
    /// </summary>
    internal const string UnicodeVersion = "${unicodeVersion}";

    /// <summary>
    ///     The groups of the mode of the <c>i</c> flag without the <c>u</c> flag, which compares UTF-16 code units.
    /// </summary>
    internal const string NonUnicodeGroups = """
${modes[0][1].map(line).join('\n')}
""";

    /// <summary>
    ///     The groups of the mode of the <c>i</c> and <c>u</c> flags together, which compares code points.
    /// </summary>
    internal const string UnicodeGroups = """
${modes[1][1].map(line).join('\n')}
""";
}
`;
fs.writeFileSync(generatedSource, source);
console.log(`Node.js ${version}, Unicode ${unicodeVersion}: ${modes[0][1].length} groups without u, ${modes[1][1].length} with u.`);
console.log(`Wrote ${generatedSource}`);
console.log(`Wrote ${referenceFile}`);
