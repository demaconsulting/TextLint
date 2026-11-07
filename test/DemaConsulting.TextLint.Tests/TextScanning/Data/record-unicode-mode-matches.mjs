// record-unicode-mode-matches.mjs
//
// PURPOSE:
//   Records what the JavaScript engine that cspell runs its patterns in finds when a pattern is used with the u flag (and,
//   for comparison, without it) on text with characters outside the basic plane and with lone surrogates, so that the unit
//   tests of the pattern translator can compare the translation with the engine: in the mode of the u flag a surrogate
//   pair is one character, `.`, negated classes, and the negated shorthands match it whole, a surrogate that is not half
//   of a pair is a character of its own, and a match never starts inside a pair. The recorded file
//   (javascript-unicode-matches.txt) is checked in; this script is NOT run by the build or by CI. Run it with Node.js
//   whenever a case is added:
//
//       node test/DemaConsulting.TextLint.Tests/TextScanning/Data/record-unicode-mode-matches.mjs
//
// HOW IT WORKS:
//   Each case is a pattern, its flags, and a text. The script builds the regular expression with the g flag added,
//   collects the non-empty matches in order, and writes one line per case: the flags, the pattern, the text, and the
//   matches, each string as the hexadecimal UTF-16 code units separated by spaces (so a lone surrogate survives any
//   editor), the matches separated by semicolons, and, last, a readable form for people. A pattern that the engine
//   rejects is recorded as the single match "!".

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

// The characters used by the cases: a grinning face and its neighbours (outside the basic plane), a Deseret letter with
// its capital and small forms, a lone high surrogate, and a lone low surrogate (the halves of the grinning face)
const E = '\u{1F600}';
const E2 = '\u{1F601}';
const E3 = '\u{1F602}';
const ROCKET = '\u{1F680}';
const D = '\u{10400}';
const d = '\u{10428}';
const H = '\uD83D';
const L = '\uDE00';

const cases = [];
const add = (pattern, flags, ...inputs) => inputs.forEach((input) => cases.push([pattern, flags, input]));

// The dot
add('^.$', 'u', E, 'a', H, L, E + E, L + H, '\n', '\u2028', '');
add('^.$', 'su', E, '\n', '\u2028', '\r', H, L);
add('^..$', 'u', E + 'a', L + H, E + E, H + E, 'ab');
add('^.$', '', E);
add('^..$', '', E);
add('.', 'u', 'a' + E + 'b' + H + 'c' + L + 'd');
add('.', '', E);
add('.{2}', 'u', E + E + E, 'a' + E, E);
add('^.{2}$', 'u', E + E, E, 'ab', E + 'a');
add('.+', 'u', E + 'a' + E);
add('.*', 'u', E + 'a' + '\n' + E);
add('.{2}', '', E);
add('(?:.){3}', 'u', E + E + E + E);
add('(.)\\1', 'u', E + E, 'aa', E + 'a', H + H);
add('(?<n>.)\\k<n>', 'u', E + E, E + E2);

// Astral literals and quantifiers
add(E + '{2}', 'u', E + E + E);
add(E + '+', 'u', E + E + 'a' + E);
add(E + '{2}', '', 'x' + H + L + L, E + E);
add('\\u{1F600}{2}', 'u', E + E + E);
add('\\uD83D\\uDE00{2}', 'u', E + E + E);
add('\\uD83D\\uDE00', 'u', E);
add('\\uD83D\\uDE00', '', E);
add('^' + E + '$', 'u', E, H + L);

// Lone surrogates written as literals and escapes
add('\\uD83D', 'u', H, E, 'a' + H, H + H + E);
add('\\uD83D', '', E, H);
add('\\uDE00', 'u', L, E, L + L);
add('\\uDE00', '', E, L);
add('\\uD83D+', 'u', H + H + E);
add('\\uDE00+', 'u', L + L + E);
add('[\\uD83D]', 'u', H, E);
add('[\\uDE00]', 'u', L, E);
add('\\u{D83D}', 'u', H, E);
add('\\u{DE00}', 'u', L, E);
add('\\u{D83D}\\u{DE00}', 'u', E, H + L);
add('\\uD83D\\u{DE00}', 'u', E);
add('\\uD83D|\\uDE00', 'u', L + H + E);
add(H, 'u', H, E);
add(L, 'u', L, E);

// Negated classes
add('[^a]', 'u', 'a' + E + 'b' + H + 'c' + L);
add('[^a]', '', 'a' + E + 'b');
add('[^a-z]', 'u', 'ab' + E + '1' + H);
add('[^' + E + ']', 'u', E + 'a' + E2);
add('[^\\u{1F600}-\\u{1F64F}]', 'u', E + E2 + D + 'a');
add('[^]', 'u', E + H + L + '\n');
add('[^]', '', E);
add('[^\\uD800-\\uDFFF]', 'u', E + H + 'a' + L);
add('[^\\uD800-\\uDFFF]', '', E + 'a');
add('[^\\uD800-\\uDBFF]', 'u', E + H + 'a' + L);
add('^[^a]$', 'u', E, H, 'a');
add('^[^a]$', '', E);
add('[^a]{2}', 'u', E + E, E + E + E);

// Classes with characters outside the basic plane, and with surrogates
add('[\\uD800-\\uDBFF]', 'u', H + E + 'a');
add('[\\uDC00-\\uDFFF]', 'u', E + L);
add('[\\uD800-\\uDFFF]', 'u', E + H + L);
add('[\\uD800-\\uDFFF]', '', E);
add('[' + E + ']', 'u', E, E2, H);
add('[' + E + '-' + E3 + ']', 'u', E + E2 + E3 + '\u{1F603}');
add('[a' + E + ']', 'u', 'a' + E + 'b' + E2);
add('[\\u{1F600}a]', 'u', 'a' + E + 'b');
add('[a-\\u{1F600}]', 'u', 'b' + E + E2 + 'z' + '\u00E9' + H);
add('[\\uD83D\\uDE00]', 'u', E, 'a' + H);
add('[\\uD83D\\uDE00]', '', E);
add('[\\u{10000}-\\u{10FFFF}]', 'u', E + 'a' + H + L);
add('[^\\u{10000}-\\u{10FFFF}]', 'u', E + 'a' + H + L);
add('[\\u{0}-\\u{10FFFF}]', 'u', E + H + L + 'a');
add('[' + E + '-\\u{1F600}]', 'u', E, E2);
add('[\\u{1F601}-\\u{1F600}]', 'u', E);
add('[' + E + ']', '', E);
add('[\\u{1F600}-\\u{1F602}\\u{1F680}]', 'u', E3 + ROCKET + E2 + '\u{1F603}');

// Negated shorthands, alone and in classes
add('\\W', 'u', 'a' + E + '-' + H);
add('\\W', '', 'a' + E);
add('\\W+', 'u', 'ab' + E + E + '-c');
add('\\S', 'u', E + ' a');
add('\\S+', 'u', E + 'a b' + E);
add('\\D', 'u', E + '1');
add('[\\W]', 'u', 'a' + E + H);
add('[^\\W]', 'u', 'a' + E + '1');
add('[\\S\\D]', 'u', E + '1' + ' ');
add('[^\\S]', 'u', E + ' ' + 'a' + '\u00A0');
add('\\w', 'u', 'a' + E + '_');
add('\\s', 'u', '\u2028' + E + '\uFEFF');
add('\\W{2}', 'u', E + E + 'a');
add('\\W', 'iu', 'a' + E + '\u017F' + '\u212A');

// Unicode properties
add('\\p{L}', 'u', 'a' + E + D + '1' + d);
add('\\P{L}', 'u', 'a' + E + D + '1' + d);
add('\\p{Lu}', 'u', 'A' + D + d + 'a');
add('\\p{Ll}', 'u', 'A' + D + d + 'a');
add('\\p{So}', 'u', E + 'a' + ROCKET);
add('\\P{So}', 'u', E + 'a' + ROCKET);
add('[\\p{So}]', 'u', E + 'a' + ROCKET);
add('[^\\p{So}]', 'u', E + 'a' + ROCKET);
add('\\p{L}+', 'u', 'ab' + D + d + '1');
add('\\p{Cs}', 'u', H + E + L + 'a');
add('\\P{Cs}', 'u', H + E + L + 'a');
add('\\p{C}', 'u', H + E + L + 'a' + '\u0000');
add('\\p{N}', 'u', '1' + E + '\u00B2');
add('\\p{Lu}', 'iu', 'a' + D + d + '1');
add('[\\p{Lu}]', 'iu', 'a' + D + d + '1');
add('[^\\p{Lu}]', 'iu', 'a' + D + d + '1' + E);
add('\\P{Lu}', 'iu', 'a' + D + d + '1' + E);
add('\\p{So}{2}', 'u', E + E2 + 'a' + ROCKET);

// Letter case of characters outside the basic plane
add(D, 'iu', d + D + 'a');
add('[' + D + ']', 'iu', d + D + 'a');
add('[^' + D + ']', 'iu', D + d + 'a');
add('[\\u{10428}-\\u{1044F}]', 'iu', D + d + 'a');
add('\\u{10400}', 'iu', d + D);
add(D, 'i', d + D);
add('[' + D + ']', 'u', d + D);

// Look-behind, look-ahead, boundaries, anchors
add('(?<=' + E + ')a', 'u', E + 'a', 'a');
add('(?<=.)a', 'u', E + 'a' + 'ba', H + 'a', 'a');
add('(?<!.)a', 'u', 'a' + E + 'a');
add('(?<=^.)x', 'u', E + 'x', 'ax');
add('(?<=[^a])b', 'u', E + 'b' + 'ab', H + 'b');
add('(?<=\\p{So})a', 'u', E + 'a' + 'a');
add('(?<=\\W)a', 'u', E + 'a');
add('.(?=a)', 'u', E + 'a' + 'ba');
add('.(?!a)', 'u', E + 'a' + 'b');
add('\\bx', 'u', E + 'x', 'ax');
add('x\\b', 'u', 'x' + E, 'xa');
add('\\Bx', 'u', E + 'x', 'ax');
add('(?<=' + H + ')' + L, 'u', E);
add('(?<=' + H + ')a', 'u', H + 'a', E + 'a');

// More combinations: lazy and nested quantifiers, groups, mixed classes, flags together
add('.+?', 'u', E + E + 'a');
add('.+?b', 'u', E + 'a' + E + 'b');
add('(?:' + E + '|a){2}', 'u', E + 'a' + E + E);
add('[^a]*', 'u', E + 'b' + 'a' + E);
add('(?:[^a]b)+', 'u', E + 'b' + 'c' + 'b');
add('\\P{L}+', 'u', 'ab' + E + '1' + D + 'c');
add('[\\p{L}\\u{1F600}]+', 'u', 'a' + E + D + '1' + E2);
add('[^\\p{L}\\u{1F600}]+', 'u', 'a' + E + D + '1' + E2 + 'b');
add('\\p{Lu}\\p{Ll}', 'u', 'A' + 'b' + D + d + 'Ab');
add('[\\u{1F600}-\\u{1F64F}]+', 'u', 'a' + E + E2 + E3 + 'b' + H);
add('(?=.)', 'u', E);
add('^.+$', 'mu', E + '\n' + E + E);
add('^.+$', 'msu', E + '\n' + E + E);
add('^.+$', 'm', E + '\n' + E);
add('[^a-z]', 'iu', 'A' + E + '1' + '\u017F' + '\u212A');
add('[a-z]', 'iu', 'A' + E + '\u017F' + '\u212A');
add('[^\\u{10400}-\\u{1044F}]', 'iu', D + d + 'a' + E);
add('\\W', 'u', E + '\u2028' + H + L + 'a');
add('[\\uD83D\\uDE00-\\uDE02]', 'u', E + 'a');
add('[\\uD83D\\uDE00-\\uDE02]', '', E + 'a' + '\uDE01');
add('\\ud83d\\ude00', 'u', E);
add('\\ud83d\\ude00|a', 'u', E + 'a');
add('(' + E + ')\\1', 'u', E + E);
add('.\\uDE00', 'u', E, H + L, 'a' + L);
add('\\uD83D.', 'u', E, H + 'a', H + E);
add('[^\\uDC00-\\uDFFF]', 'u', E + 'a' + H + L);
add('[^\\uD800-\\uDBFF\\uDC00-\\uDFFF]', 'u', E + 'a' + H + L);

const encode = (s) =>
    Array.from({ length: s.length }, (_, i) => s.charCodeAt(i).toString(16).toUpperCase().padStart(4, '0')).join(' ');

const lines = [
    '# cspell:disable',
    `# What the JavaScript engine finds with the u flag, and without it, on characters outside the basic plane and on lone surrogates, recorded from Node.js ${process.version}`,
    '# by record-unicode-mode-matches.mjs. One case per line, with four tab-separated fields and a fifth for people: the flags, the pattern, the text,',
    '# and the non-empty matches (each string as hexadecimal UTF-16 code units separated by spaces; several matches separated by semicolons; "!" when',
    '# the engine rejects the pattern). Do not edit by hand.',
];
for (const [pattern, flags, input] of cases) {
    let matches;
    try {
        matches = [...input.matchAll(new RegExp(pattern, flags + 'g'))].filter((m) => m[0].length > 0).map((m) => encode(m[0])).join(';');
    } catch {
        matches = '!';
    }

    const readable = JSON.stringify(pattern) + ' /' + flags + ' on ' + JSON.stringify(input);
    lines.push([flags, encode(pattern), encode(input), matches, readable].join('\t'));
}

const target = path.join(path.dirname(fileURLToPath(import.meta.url)), 'javascript-unicode-matches.txt');
fs.writeFileSync(target, lines.join('\n') + '\n');
console.log(`${cases.length} cases written to ${target}`);
