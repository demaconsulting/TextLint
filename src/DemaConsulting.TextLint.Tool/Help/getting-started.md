---
# textlint:ignore wrold
title: Getting started
summary: Install textlint, check your files, and find the other topics.
aliases:
  - start
  - quickstart
  - examples
  - install
tags:
  - tutorial
order: 1
see-also:
  - command-line
  - configuration
  - self-validation
examples:
  - name: install-local
    kind: snippet
    language: shell
    save-as: install-local.sh
    description: Add the tool to the tool manifest of a repository.
    content: |
      dotnet new tool-manifest
      dotnet tool install --local DemaConsulting.TextLint.Tool
  - name: run-in-ci
    kind: snippet
    language: shell
    save-as: run-in-ci.sh
    description: Restore the manifest tools in CI, then run the tool.
    content: |
      dotnet tool restore
      dotnet {{tool}}
  - name: version
    kind: command
    language: shell
    description: Print the version and exit.
    content: '{{tool}} --version'
  - name: check
    kind: command
    language: shell
    description: Check the files that .textlint.yaml selects.
    content: '{{tool}}'
    expect: 'Checked 1 file(s): 0 issue(s)'
  - name: typo
    kind: command
    language: shell
    description: Check a file that has a typo. Exits 1.
    content: '{{tool}} notes.txt'
    exit: 1
    expect: "notes.txt(1,7): error: Unknown word 'wrold'"
  - name: validate
    kind: command
    language: shell
    description: Run the built-in self-tests. Exits 0 when every test passes.
    content: '{{tool}} --validate'
    expect: 'Failed: 0'
  - name: topic
    kind: command
    language: shell
    description: Print a help topic, here the settings file.
    content: '{{tool}} help configuration'
    expect: 'ignorePaths'
---
{{tool}} {{version}} is a .NET tool.
It reads the settings file and the files it selects, and writes only the
files that `--log` and `--results` name; `--validate` uses a temporary
folder.

1. **Install it.** Installing needs a .NET SDK, and running the tool needs
   the .NET 10 runtime, which the .NET 10 SDK includes. In CI, set it up
   first when the runner lacks it, for example with
   `actions/setup-dotnet` and `dotnet-version: 10.x`.
   For a repository or CI, install it as a local tool. Run the commands
   that [[#install-local]] prints once, without
   `dotnet new tool-manifest` when `.config/dotnet-tools.json` exists, and
   commit that file. In CI, run `dotnet tool restore` first, then run the
   tool as `dotnet {{tool}}`, as in the commands that
   [[#run-in-ci]] prints. For one machine, install it globally and
   run it as `{{tool}}`:

   ```bash
   dotnet tool install -g DemaConsulting.TextLint.Tool
   ```

2. **Configure it.** Put `.textlint.yaml` in the root folder of the
   repository. It selects the files to check and lists the words of the
   project; see [[configuration]].
3. **Check.** The command that [[#check]] prints checks the files that
   the settings select, and exits 0 when it finds nothing.
   The command that [[#typo]] prints shows a finding: one line
   `file(line,column): error: message` for each issue, then a summary
   line, and exit code 1. See [[spell-checking]] for what is checked.
4. **Prove it works.** The command that [[#validate]] prints runs the
   built-in self-tests; see [[self-validation]].

Print any help topic with `{{tool}} help <topic>`, as in the command that
[[#topic]] prints. Add `--search <words>` to find a topic, and `--json` to
get any help answer as JSON.
