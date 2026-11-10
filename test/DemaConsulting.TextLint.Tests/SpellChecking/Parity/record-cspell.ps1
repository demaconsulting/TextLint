# record-cspell.ps1
#
# PURPOSE:
#   Records the output of the real cspell for every parity corpus, so the parity test can compare TextLint
#   with it. The recorded files are checked in (expected/*.cspell); this script is NOT run by the build or
#   by CI. Run it from anywhere, after "npm install" at the repository root (cspell is a devDependency used only by this recorder, never by lint.ps1), whenever a corpus or its settings
#   change:
#
#       pwsh ./test/DemaConsulting.TextLint.Tests/SpellChecking/Parity/record-cspell.ps1
#
# HOW IT WORKS:
#   For each corpus/NAME.input the script runs "cspell lint" with corpus/NAME.settings.yaml as the
#   configuration. The same settings file is loaded by TextLint in the parity test, so both tools check the
#   corpus with identical settings. cspell stops reporting a word after 5 repeats and a file after 100
#   problems by default; TextLint has neither cap, so the script lifts both by importing the settings file
#   from a temporary configuration. The output is reduced to one "LINE:COLUMN WORD [flagged]" line per
#   finding and written to expected/NAME.cspell under a header that records the cspell version. The corpora whose
#   result depends on line endings (the directive corpora, and the corpora of patterns that use line anchors and
#   the dot, and the ignoreRegExp directive) are checked twice, once with the line endings they are stored with (line feed) and once with every line
#   ending turned into a carriage return and a line feed; the second recording is written to expected/NAME.crlf.cspell.
#   The corpus of Markdown patterns is checked with the language markdown forced, because its name has the neutral
#   extension .input.
#
# The corpora use only invented words (zorb, quix, flib, ...), so the recorded output does not depend on the
# dictionaries of either tool, except for the deliberate differences listed in known-divergences.txt.

$ErrorActionPreference = 'Stop'

$parityDirectory = $PSScriptRoot
$corpusDirectory = Join-Path $parityDirectory 'corpus'
$expectedDirectory = Join-Path $parityDirectory 'expected'
$repositoryRoot = (Resolve-Path (Join-Path $parityDirectory '../../../..')).Path

# The caps that cspell applies by default and TextLint does not have
$lifted = 100000

# The corpora that are also recorded with CRLF line endings: the ones whose result depends on lines (the directives,
# and the patterns that use line anchors and the dot)
$crlfCorpora = @('directives', 'unterminated', 'custom-patterns', 'markdown-links', 'ignore-regexp', 'directive-lookalikes')

# The corpora that cspell must read as a language, because their file name has the neutral extension .input. TextLint
# reads the same corpus under a source name with the matching extension (see CspellParityTests).
$languageIds = @{ 'markdown-links' = 'markdown' }

# Runs cspell over one file and returns its findings as "LINE:COLUMN WORD [flagged]" lines
function Get-CspellFindings([string] $file, [string] $name, [string] $settings) {
    # A temporary configuration that imports the corpus settings and lifts the caps
    $temp = Join-Path ([IO.Path]::GetTempPath()) "record-cspell-$name-$([guid]::NewGuid().ToString('N')).json"
    @{ import = @($settings); maxDuplicateProblems = $lifted; maxNumberOfProblems = $lifted } |
        ConvertTo-Json | Set-Content -Path $temp -Encoding utf8

    # A corpus that needs a language is checked with that language forced
    $languageArguments = @()
    if ($languageIds.ContainsKey($name)) {
        $languageArguments = @('--language-id', $languageIds[$name])
    }

    try {
        # cspell exits with 1 when it finds problems, which is expected here
        $output = npx cspell lint --no-progress --no-color --no-summary --no-config-search @languageArguments `
            --config $temp --root $corpusDirectory $file 2>&1
    }
    finally {
        Remove-Item $temp -ErrorAction SilentlyContinue
    }

    foreach ($line in $output) {
        $text = "$line"
        if ($text -match '^.+?:(\d+):(\d+) - (.+?) \((.*?)\)(?: fix: .*)?$') {
            $kind = if ($Matches[3] -like 'Forbidden*') { ' flagged' } else { '' }
            "$($Matches[1]):$($Matches[2]) $($Matches[4])$kind"
        }
        elseif ($text.Trim().Length -gt 0) {
            throw "Unexpected cspell output for ${name}: $text"
        }
    }
}

Push-Location $repositoryRoot
try {
    $version = (npx cspell --version | Select-Object -First 1).Trim()
    New-Item -ItemType Directory -Force $expectedDirectory | Out-Null

    foreach ($corpus in Get-ChildItem $corpusDirectory -Filter '*.input') {
        $name = $corpus.BaseName
        $settings = Join-Path $corpusDirectory "$name.settings.yaml"
        if (-not (Test-Path $settings)) { throw "Missing settings file: $settings" }

        $variants = @(@{ Suffix = ''; Description = 'line feed line endings' })
        if ($crlfCorpora -contains $name) {
            $variants += @{ Suffix = '.crlf'; Description = 'carriage return and line feed line endings' }
        }

        foreach ($variant in $variants) {

            # The second variant is a temporary copy with every line ending turned into CRLF
            $file = $corpus.FullName
            if ($variant.Suffix -eq '.crlf') {
                $file = Join-Path $corpusDirectory "$name.crlf-copy.input"
                $text = [IO.File]::ReadAllText($corpus.FullName) -replace "`r`n", "`n" -replace "`n", "`r`n"
                [IO.File]::WriteAllText($file, $text)
            }

            try {
                $lines = @(Get-CspellFindings $file $name $settings)
            }
            finally {
                if ($variant.Suffix -eq '.crlf') { Remove-Item $file -ErrorAction SilentlyContinue }
            }

            $header = @(
                "# Recorded with cspell $version by record-cspell.ps1 from corpus/$name.input ($($variant.Description)) " +
                "and corpus/$name.settings.yaml.",
                '# One finding per line: LINE:COLUMN WORD [flagged]. Do not edit by hand; run the script again.'
            )
            ($header + $lines) | Set-Content -Path (Join-Path $expectedDirectory "$name$($variant.Suffix).cspell") -Encoding utf8
            Write-Host "$name$($variant.Suffix) : $($lines.Count) findings"
        }
    }
}
finally {
    Pop-Location
}
