// Views rendered into docs/design/generated/ for the Design document.
// Reopens the TextLintTool package so each 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
package TextLintTool {
    view SoftwareStructureView {
        expose TextLint::*;
        expose TextLintTool::*;
    }

    view TextLintToolView {
        expose TextLintToolSystem;
    }

    view CliView {
        expose CliSubsystem;
    }

    view SelfTestView {
        expose SelfTestSubsystem;
    }

    view UtilitiesView {
        expose UtilitiesSubsystem;
    }
}

// The repository holds two systems: the TextLint library below and the TextLintTool
// command-line tool above. The TextLint library has two subsystems, each with its own view;
// units are shown by their subsystem's view.
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
}
