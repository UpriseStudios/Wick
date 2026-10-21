# Builds the signed Play Store AAB (unity/Builds/Wick-release.aab).
# Run it yourself in PowerShell from the repo root:
#   powershell -ExecutionPolicy Bypass -File tools/build/build-release.ps1
# It asks for the keystore passwords and hands them to this one Unity process only; nothing is saved.
param(
    [string]$Keystore = "$env:USERPROFILE\keys\wick-upload.jks",
    [string]$Alias = "wick-upload",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
)

if (-not (Test-Path $Keystore)) { Write-Error "No keystore at $Keystore"; exit 1 }

function Read-Secret([string]$prompt) {
    $secure = Read-Host $prompt -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

$env:WICK_KEYSTORE_PATH = $Keystore
$env:WICK_KEY_ALIAS = $Alias
$env:WICK_KEYSTORE_PASS = Read-Secret "Keystore password"
$env:WICK_KEY_PASS = Read-Secret "Key password (Enter if the same)"
if ([string]::IsNullOrEmpty($env:WICK_KEY_PASS)) { $env:WICK_KEY_PASS = $env:WICK_KEYSTORE_PASS }

try {
    $log = Join-Path $PSScriptRoot "..\..\unity\Builds\release-build.log"
    $p = Start-Process -FilePath $Unity -Wait -PassThru -NoNewWindow -ArgumentList @(
        "-batchmode", "-quit", "-projectPath", "unity", "-buildTarget", "Android",
        "-executeMethod", "Wick.Editor.AndroidBuild.BuildRelease", "-logFile", $log)
    Select-String -Path $log -Pattern "AndroidBuild:" | ForEach-Object { $_.Line }
    if ($p.ExitCode -ne 0) { Write-Error "Build failed (exit $($p.ExitCode)); see $log"; exit $p.ExitCode }
}
finally {
    Remove-Item Env:WICK_KEYSTORE_PASS, Env:WICK_KEY_PASS -ErrorAction SilentlyContinue
}
