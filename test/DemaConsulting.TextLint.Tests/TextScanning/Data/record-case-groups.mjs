// record-case-groups.mjs
//
// PURPOSE:
//   Records which UTF-16 code units the JavaScript engine treats as the same letter when a regular expression ignores
//   case, so that the unit tests of JavaScriptCaseFolding can compare the library with the engine that cspell runs its
//   patterns in. The recorded file (javascript-case-groups.txt) is checked in; this script is NOT run by the build or
//   by CI. Run it with Node.js whenever the test data should be refreshed:
//
//       node test/DemaConsulting.TextLint.Tests/TextScanning/Data/record-case-groups.mjs
//
// HOW IT WORKS:
//   Two code units are the same letter when the expression /^X$/i (or /^X$/iu) built from one matches the other. For every
//   code unit the script tests the characters that its lower-case and upper-case forms lead to, and joins the code units
//   that match into groups. Only groups with more than one member are written, in the basic multilingual plane.

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

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
    const escape = (cp) => (unicode ? `\\u{${cp.toString(16)}}` : `\\u${cp.toString(16).padStart(4, '0')}`);
    const flags = unicode ? 'iu' : 'i';
    const limit = unicode ? 0x110000 : 0x10000;
    for (let c = 0; c < limit; c++) {
        if (c >= 0xd800 && c <= 0xdfff) continue;
        const s = String.fromCodePoint(c);
        const candidates = new Set([s.toLowerCase(), s.toUpperCase(), s.toLowerCase().toUpperCase(), s.toUpperCase().toLowerCase()]);
        for (const t of candidates) {
            const chars = [...t];
            if (t === s || chars.length !== 1) continue;
            const d = chars[0].codePointAt(0);
            if (!unicode && d > 0xffff) continue;
            if (new RegExp(`^${escape(d)}$`, flags).test(s)) union(c, d);
        }
    }
    const byRoot = new Map();
    for (const x of parent.keys()) {
        const r = find(x);
        if (!byRoot.has(r)) byRoot.set(r, []);
        byRoot.get(r).push(x);
    }
    return [...byRoot.values()]
        .map((g) => g.filter((c) => c < 0x10000).sort((a, b) => a - b))
        .filter((g) => g.length > 1)
        .sort((a, b) => a[0] - b[0]);
}

const lines = [
    '# cspell:disable',
    '# The groups of UTF-16 code units that the JavaScript engine treats as the same letter when a regular expression ignores',
    `# case, recorded from Node.js ${process.version} (Unicode ${process.versions.unicode}) by record-case-groups.mjs. One group per line:`,
    '# the mode (nonunicode for the i flag, unicode for the iu flags), a colon, and the code units in hexadecimal. Groups with',
    '# one member are not listed. Only the basic multilingual plane is recorded. Do not edit by hand.',
];
for (const [mode, unicode] of [['nonunicode', false], ['unicode', true]]) {
    for (const g of groups(unicode)) {
        lines.push(`${mode}: ${g.map((c) => c.toString(16).toUpperCase().padStart(4, '0')).join(' ')}`);
    }
}
const target = path.join(path.dirname(fileURLToPath(import.meta.url)), 'javascript-case-groups.txt');
fs.writeFileSync(target, lines.join('\n') + '\n');
console.log(`${lines.length} lines written to ${target}`);
