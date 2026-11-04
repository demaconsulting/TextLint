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

// The repository holds two systems: the TextLint library above and the TextLintTool
// command-line tool. The TextLint library is flat - it has no subsystems or units yet - so
// it needs only the one system view.
package TextLint {
    view TextLintView {
        expose TextLintSystem;
    }
}
