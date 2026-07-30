<#
.SYNOPSIS
    Compila el VSIX y publica un release en GitHub con el .vsix adjunto.

.DESCRIPTION
    Se ejecuta desde una VM de desarrollo de D365, que es donde estan los assemblies de las
    herramientas. Un runner de GitHub no puede compilar este proyecto: los assemblies son de
    Microsoft y no se versionan en el repo (ver .gitignore).

    Pasos:
      1. Verifica que lib\ tenga los assemblies (si no, corre Get-D365Assemblies.ps1).
      2. Compila en Release.
      3. Crea el tag y el release con gh, adjuntando el .vsix.

    Requiere gh CLI autenticado (gh auth login).

.PARAMETER Version
    Version del release, sin la "v" (por ejemplo "1.2.0"). Se usa para el tag y para el
    manifest.

.PARAMETER Notes
    Texto de las notas del release. Si se omite, gh las genera de los commits.

.PARAMETER SkipPublish
    Compila y prepara todo, pero no crea el release. Para revisar antes de publicar.

.EXAMPLE
    .\tools\New-Release.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $Notes,
    [switch] $SkipPublish
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $repoRoot 'src\Dynamo.D365.ItemTemplates\Dynamo.D365.ItemTemplates.csproj'
$manifest = Join-Path $repoRoot 'src\Dynamo.D365.ItemTemplates\source.extension.vsixmanifest'
$lib = Join-Path $repoRoot 'lib'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "La version tiene que ser N.N.N (recibido: '$Version')."
}

# --- assemblies -------------------------------------------------------------------------

if (-not (Test-Path (Join-Path $lib 'Microsoft.Dynamics.AX.Metadata.dll'))) {
    Write-Host 'Faltan los assemblies de D365 en lib\, obteniendolos...'
    & (Join-Path $PSScriptRoot 'Get-D365Assemblies.ps1')
}

# --- version en el manifest -------------------------------------------------------------

$manifestXml = Get-Content $manifest -Raw

if ($manifestXml -notmatch 'Version="(\d+\.\d+\.\d+)"') {
    throw "No se pudo leer la version actual de $manifest"
}

$current = $Matches[1]

if ($current -ne $Version) {
    Write-Host "Actualizando la version del manifest: $current -> $Version"
    $updated = $manifestXml -replace 'Version="\d+\.\d+\.\d+"', "Version=`"$Version`"", 1
    [IO.File]::WriteAllText($manifest, $updated, (New-Object Text.UTF8Encoding $false))
}

# --- build ------------------------------------------------------------------------------

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
           Select-Object -First 1

if (-not $msbuild) { throw 'No se encontro MSBuild.' }

Write-Host "Compilando con $msbuild"
& $msbuild $project -restore -p:Configuration=Release -v:m -nologo

if ($LASTEXITCODE -ne 0) { throw "La compilacion fallo (exit $LASTEXITCODE)." }

$vsix = Join-Path $repoRoot 'src\Dynamo.D365.ItemTemplates\bin\Release\net48\Dynamo.D365.ItemTemplates.vsix'

if (-not (Test-Path $vsix)) { throw "No se genero el VSIX en $vsix" }

Write-Host "VSIX: $vsix" -ForegroundColor Green

if ($SkipPublish) {
    Write-Host 'Se omite la publicacion por -SkipPublish.'
    return
}

# --- release ----------------------------------------------------------------------------

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'No se encontro gh CLI. Instalalo o publica el release a mano adjuntando el .vsix.'
}

$tag = "v$Version"
$arguments = @('release', 'create', $tag, $vsix, '--title', $tag)

if ($Notes) { $arguments += @('--notes', $Notes) } else { $arguments += '--generate-notes' }

Write-Host "Publicando $tag..."
& gh @arguments

if ($LASTEXITCODE -ne 0) { throw "gh release create fallo (exit $LASTEXITCODE)." }

Write-Host ""
Write-Host "Publicado. Para instalarlo en una VM:" -ForegroundColor Green
Write-Host "  irm https://raw.githubusercontent.com/dropme/d365foitemtemplates/main/install/Install-ItemTemplates.ps1 | iex"
