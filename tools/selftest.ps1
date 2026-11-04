# Runs the in-app self-test against a fresh seeded store in %TEMP%. Exit code 0 when every check passed.
. "$PSScriptRoot\common.ps1"
$store = Join-Path $env:TEMP "$AppName-self-$(Split-Path (Split-Path $PSScriptRoot -Parent) -Leaf).db"
Remove-Store $store
$code = Invoke-App -Environment @{ APP_STORE = $store; APP_SEED = '1' } -Arguments '--selftest'
exit $code
