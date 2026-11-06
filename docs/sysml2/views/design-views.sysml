// Views rendered into docs/design/generated/ for the Design document.
// Reopens the TextLintTool package so each 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
package TextLintTool {
    view SoftwareStructureView {
        expose TextLint::*;
        expose TextLintTool::*;
        expose DictionaryBuilder::*;
    }

    view TextLintToolView {
        expose TextLintToolSystem;
    }

    view CliView {
        expose CliSubsystem;
    }

    view LintingView {
        expose LintingSubsystem;
    }

    view SelfTestView {
        expose SelfTestSubsystem;
    }

    view UtilitiesView {
        expose UtilitiesSubsystem;
    }
}

// The repository holds three systems: the TextLint library below, the TextLintTool
// command-line tool above, and the DictionaryBuilder at the end. The TextLint library has five
// subsystems, each with its own view; units are shown by their subsystem's view.
package TextLint {
    view TextLintView {
        expose TextLintSystem;
    }

    view ConfigurationView {
        expose ConfigurationSubsystem;
    }

    view FileSelectionView {
        expose FileSelectionSubsystem;
    }

    view DictionariesView {
        expose DictionariesSubsystem;
    }

    view TextScanningView {
        expose TextScanningSubsystem;
    }

    view SpellCheckingView {
        expose SpellCheckingSubsystem;
    }
}

// The DictionaryBuilder is a third system: the build-time converter that turns the word lists into
// the binary dictionary files the library embeds. Its five units sit directly under the system, so
// the one view shows all of them.
package DictionaryBuilder {
    view DictionaryBuilderView {
        expose DictionaryBuilderSystem;
    }
}
