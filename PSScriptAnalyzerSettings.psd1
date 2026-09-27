@{
    # What the pre-commit hook fails a commit on. Informationals are left to the reader: the positional-parameter
    # notes in tools/test-all.ps1 are its own smoke commands read as they are written.
    Severity     = @('Error', 'Warning')

    ExcludeRules = @(
        # Every script here talks to a developer at the console; Write-Host is the point, not a leak.
        'PSAvoidUsingWriteHost',
        # The state-changing functions are private helpers of scripts that take -Force and -WhatIf at their own top
        # level where it matters; SupportsShouldProcess on each would be ceremony without a caller.
        'PSUseShouldProcessForStateChangingFunctions'
    )
}
