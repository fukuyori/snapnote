param(
    [switch]$SkipPublish,
    [string]$IsccPath,
    [switch]$Sign,
    [string]$SignToolPath,
    [switch]$NoUiAccess
)

$ErrorActionPreference = "Stop"

$UiAccess = -not $NoUiAccess
if ($UiAccess -and -not $Sign) {
    throw "The default uiAccess build requires -Sign. Pass -Sign, or use -NoUiAccess for a plain per-user installer."
}

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ReleaseScript = Join-Path $PSScriptRoot "build-release.ps1"
$InstallerScript = Join-Path $RepoRoot "installer.iss"
$InstallerOutputDir = Join-Path $RepoRoot "installer_output"
$PublishExePath = Join-Path $RepoRoot "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\SnapNoteStudio.exe"

function Resolve-SignToolPath {
    param([string]$Preferred)

    if (-not [string]::IsNullOrWhiteSpace($Preferred)) {
        if (-not (Test-Path $Preferred)) {
            throw "signtool.exe was not found: $Preferred"
        }
        return (Resolve-Path $Preferred).Path
    }

    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin"
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($root in $roots) {
        $candidates = Get-ChildItem -Path $root -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending

        # x64 版を優先し、無ければ x86 版にフォールバックする。
        $found = $candidates | Where-Object { $_.Directory.Name -eq "x64" } | Select-Object -First 1
        if (-not $found) {
            $found = $candidates | Where-Object { $_.Directory.Name -eq "x86" } | Select-Object -First 1
        }

        if ($found) {
            return $found.FullName
        }
    }

    throw "signtool.exe was not found. Install the Windows SDK or pass -SignToolPath `"C:\Path\To\signtool.exe`"."
}

function Get-CodeSignSettings {
    $cert = $env:CODESIGN_CERT
    if ([string]::IsNullOrWhiteSpace($cert)) {
        throw "-Sign was specified but the CODESIGN_CERT environment variable is not set."
    }

    $timestampUrl = $env:CODESIGN_TIMESTAMP_URL
    if ([string]::IsNullOrWhiteSpace($timestampUrl)) {
        $timestampUrl = "http://timestamp.digicert.com"
    }

    # 1) 証明書ファイル (.pfx / .p12) のパス
    if (Test-Path -LiteralPath $cert) {
        return [pscustomobject]@{
            Mode         = "File"
            Certificate  = (Resolve-Path -LiteralPath $cert).Path
            Password     = $env:CODESIGN_PASSWORD
            TimestampUrl = $timestampUrl
        }
    }

    if ($cert -match '\.(pfx|p12)$') {
        throw "CODESIGN_CERT points to a certificate file that does not exist: $cert"
    }

    # 2) 40 桁の拇印 (空白やコロン区切りも許容)
    $thumbprint = ($cert -replace '[\s:]', '')
    if ($thumbprint -match '^[0-9a-fA-F]{40}$') {
        return [pscustomobject]@{
            Mode         = "Thumbprint"
            Certificate  = $thumbprint
            Password     = $null
            TimestampUrl = $timestampUrl
        }
    }

    # 3) それ以外は証明書ストア内のサブジェクト名 (signtool /n) とみなす
    return [pscustomobject]@{
        Mode         = "Subject"
        Certificate  = $cert
        Password     = $null
        TimestampUrl = $timestampUrl
    }
}

function Invoke-CodeSign {
    param(
        [string]$SignTool,
        [object]$Settings,
        [string]$TargetPath
    )

    $signArgs = @("sign", "/fd", "SHA256", "/tr", $Settings.TimestampUrl, "/td", "SHA256")

    switch ($Settings.Mode) {
        "File" {
            $signArgs += @("/f", $Settings.Certificate)
            if (-not [string]::IsNullOrWhiteSpace($Settings.Password)) {
                $signArgs += @("/p", $Settings.Password)
            }
        }
        "Thumbprint" { $signArgs += @("/sha1", $Settings.Certificate) }
        "Subject"    { $signArgs += @("/n", $Settings.Certificate) }
    }

    $signArgs += @("/v", $TargetPath)

    & $SignTool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Code signing failed ($LASTEXITCODE): $TargetPath"
    }
}

function Test-UiAccessManifest {
    param([string]$Path)

    # The embedded manifest is stored uncompressed in the PE resources.
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $text = [System.Text.Encoding]::ASCII.GetString($bytes)
    return $text.Contains('uiAccess="true"')
}

$SignSettings = $null
$SignTool = $null

if ($Sign) {
    $SignSettings = Get-CodeSignSettings
    $SignTool = Resolve-SignToolPath -Preferred $SignToolPath
    Write-Host "Code signing enabled."
    Write-Host "  signtool  : $SignTool"
    Write-Host "  certificate: $($SignSettings.Certificate) ($($SignSettings.Mode))"
    Write-Host "  timestamp : $($SignSettings.TimestampUrl)"
}

if (-not $SkipPublish) {
    & $ReleaseScript -NoUiAccess:$NoUiAccess
}

if (-not (Test-Path $PublishExePath)) {
    throw "Executable was not found: $PublishExePath (run without -SkipPublish)"
}

$exeHasUiAccess = Test-UiAccessManifest -Path $PublishExePath
if ($exeHasUiAccess -and -not $UiAccess) {
    throw "The published executable contains a uiAccess manifest but -NoUiAccess was specified. Re-publish the executable."
}
if ($UiAccess -and -not $exeHasUiAccess) {
    throw "The published executable does not contain a uiAccess manifest. Re-publish it, or pass -NoUiAccess."
}

if ($Sign) {
    Write-Host "Signing application executable..."
    Invoke-CodeSign -SignTool $SignTool -Settings $SignSettings -TargetPath $PublishExePath
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $command = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($command) {
        $IsccPath = $command.Source
    }
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
    )

    $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path $IsccPath)) {
    throw "Inno Setup compiler was not found. Install Inno Setup or pass -IsccPath `"C:\Path\To\ISCC.exe`"."
}

$IsccArgs = @()

if (-not $UiAccess) {
    $IsccArgs += "/DNOUIACCESS"
}

if ($Sign) {
    # $q は Inno Setup が二重引用符に、$f は署名対象ファイル名に置き換える。
    # PowerShell 側で引用符を含めないことで、引数の受け渡しを安定させる。
    $signCommand = "`$q$SignTool`$q sign /fd SHA256 /tr $($SignSettings.TimestampUrl) /td SHA256"

    switch ($SignSettings.Mode) {
        "File" {
            $signCommand += " /f `$q$($SignSettings.Certificate)`$q"
            if (-not [string]::IsNullOrWhiteSpace($SignSettings.Password)) {
                $signCommand += " /p `$q$($SignSettings.Password)`$q"
            }
        }
        "Thumbprint" { $signCommand += " /sha1 $($SignSettings.Certificate)" }
        "Subject"    { $signCommand += " /n `$q$($SignSettings.Certificate)`$q" }
    }

    $signCommand += " /v `$f"

    # /DSIGN で installer.iss の SignTool / SignedUninstaller 指令を有効化する。
    # これによりインストーラー本体とアンインストーラーの双方が署名される。
    $IsccArgs += "/DSIGN"
    $IsccArgs += "/Ssnapnotesign=$signCommand"
}

$IsccArgs += $InstallerScript

& $IsccPath @IsccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed ($LASTEXITCODE)."
}

if (-not (Test-Path $InstallerOutputDir)) {
    throw "Installer output directory was not created: $InstallerOutputDir"
}

Write-Host "Installer output:"
$setupFile = Get-ChildItem -Path $InstallerOutputDir -Filter "SnapNoteStudio_Setup_*.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($setupFile) {
    Write-Host $setupFile.FullName

    if ($Sign) {
        Write-Host "Verifying signatures..."
        & $SignTool verify /pa /all $PublishExePath
        if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $PublishExePath" }
        & $SignTool verify /pa /all $setupFile.FullName
        if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $($setupFile.FullName)" }
    }
}
