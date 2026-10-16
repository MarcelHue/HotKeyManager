# Code style rules (~/code-stil/code-style.md) for PowerShell, enforced since 2026-10-15.
# Every finding counts as an error: Invoke-ScriptAnalyzer -Path . -Recurse -Settings <this file> must report nothing.
@{
    Severity     = @('Error', 'Warning', 'Information')
    # Console output is the purpose of these scripts.
    ExcludeRules = @('PSAvoidUsingWriteHost')
    Rules        = @{
        PSAvoidLongLines = @{
            Enable            = $true
            MaximumLineLength = 120
        }
    }
}
