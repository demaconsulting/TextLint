// Views rendered into docs/design/generated/ for the Design document.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage
// (not a 'view def' definition), and it - not 'render' - is what scopes the
// rendered diagram content.
//
// The repository holds three systems: the TextLint library, the TextLintTool command-line
// tool, and the DictionaryBuilder. The TextLint library has five subsystems, each with its own
// view; units are shown by their subsystem's view. The DictionaryBuilder's units sit directly
// under the system, so the one view shows all of them.
package TextLintDesignViews {
    private import StandardViewDefinitions::*;
    private import TextLint::*;
    private import TextLintTool::*;
    private import DictionaryBuilder::*;

    view SoftwareStructureView : InterconnectionView {
        expose TextLint::TextLintSystem;
        expose TextLintTool::TextLintToolSystem;
        expose DictionaryBuilder::DictionaryBuilderSystem;
        filter not (@SysML::Documentation);
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

    view DictionaryBuilderView {
        expose DictionaryBuilderSystem;
    }
}
