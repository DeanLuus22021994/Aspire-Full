<#
.SYNOPSIS
    Runs an agent with the specified configuration and input.

.DESCRIPTION
    Wrapper script that delegates to Invoke-PythonScript.ps1 for running agents.
    Maintained for backwards compatibility.

.PARAMETER Config
    Path to the agent configuration file.

.PARAMETER Input
    Input message to send to the agent.

.EXAMPLE
    .\run_agent.ps1 -Config "config.yaml" -Input "Hello, agent!"

.NOTES
    For more options, use Invoke-PythonScript.ps1 directly.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Config,

    [Parameter(Mandatory = $true)]
    [string]$Input
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot

# Delegate to unified script runner
& "$ScriptDir\Invoke-PythonScript.ps1" -ScriptName "run_agent" -Config $Config -Input $Input
