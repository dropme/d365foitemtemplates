<#
.SYNOPSIS
    Copia a lib\ los assemblies de las herramientas de D365 necesarios para compilar.

.DESCRIPTION
    El proyecto referencia assemblies que vienen con las herramientas de desarrollo de
    Dynamics 365 F&O. No se versionan en el repo -- son binarios de Microsoft -- asi que hay
    que traerlos de la instalacion local antes de compilar por primera vez.

    Viven en la carpeta de extension de Visual Studio, cuyo nombre es aleatorio por maquina y
    por version de las herramientas, por eso se busca en vez de hardcodearla.

    Alternativa: compilar apuntando directo a la instalacion, sin copiar nada:
        msbuild ... /p:D365ToolsPath="<carpeta de la extension>"

.EXAMPLE
    .\tools\Get-D365Assemblies.ps1

.EXAMPLE
    .\tools\Get-D365Assemblies.ps1 -ExtensionPath "C:\...\Extensions\q4ii425k.yfl"
#>
[CmdletBinding()]
param(
    # Carpeta de la extension de D365. Si se omite, se busca.
    [string] $ExtensionPath,

    # Destino. Por defecto lib\ en la raiz del repo.
    [string] $Destination = (Join-Path $PSScriptRoot '..\lib')
)

$ErrorActionPreference = 'Stop'

# Assemblies que el .csproj referencia, mas los que arrastran por herencia.
$required = @(
    'Microsoft.Dynamics.AX.Metadata.dll'
    'Microsoft.Dynamics.AX.Metadata.Core.dll'
    'Microsoft.Dynamics.Framework.Tools.MetaModel.Core.17.0.dll'
    'Microsoft.Dynamics.Framework.Tools.Extensibility.17.0.dll'
    'Microsoft.Dynamics.Framework.Tools.ProjectSystem.17.0.dll'
    'Microsoft.Dynamics.Framework.Tools.ProjectSupport.17.0.dll'
)

function Find-ExtensionPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

    if (-not (Test-Path $vswhere)) {
        throw 'No se encontro vswhere.exe. Instala Visual Studio 2022 o pasa -ExtensionPath.'
    }

    $installations = & $vswhere -all -prerelease -version '[17.0,18.0)' -property installationPath

    foreach ($installation in $installations) {
        $extensions = Join-Path $installation 'Common7\IDE\Extensions'

        if (-not (Test-Path $extensions)) { continue }

        # El nombre de la carpeta es aleatorio: se identifica por su contenido.
        $marker = Get-ChildItem $extensions -Recurse -Filter 'Microsoft.Dynamics.Framework.Tools.ProjectSystem.17.0.dll' -ErrorAction SilentlyContinue |
                  Select-Object -First 1

        if ($marker) { return $marker.Directory.FullName }
    }

    throw 'No se encontraron las herramientas de desarrollo de D365 en ninguna instalacion de Visual Studio 2022.'
}

if (-not $ExtensionPath) {
    Write-Host 'Buscando las herramientas de desarrollo de D365...'
    $ExtensionPath = Find-ExtensionPath
}

if (-not (Test-Path $ExtensionPath)) {
    throw "La carpeta indicada no existe: $ExtensionPath"
}

Write-Host "Herramientas: $ExtensionPath"

if (-not (Test-Path $Destination)) {
    New-Item -ItemType Directory -Path $Destination | Out-Null
}

$missing = @()

foreach ($name in $required) {
    $source = Join-Path $ExtensionPath $name

    if (-not (Test-Path $source)) {
        # Algunos assemblies cuelgan de subcarpetas segun la version de las herramientas.
        $found = Get-ChildItem $ExtensionPath -Recurse -Filter $name -ErrorAction SilentlyContinue |
                 Select-Object -First 1

        if (-not $found) { $missing += $name; continue }

        $source = $found.FullName
    }

    Copy-Item $source -Destination $Destination -Force
    Write-Host ("  OK  {0}" -f $name)
}

if ($missing.Count -gt 0) {
    Write-Warning "No se encontraron estos assemblies:`n  $($missing -join "`n  ")"
    Write-Warning 'Puede que la version de las herramientas sea distinta. Revisa los nombres en el .csproj.'
    exit 1
}

Write-Host ""
Write-Host "Listo. Ya se puede compilar:" -ForegroundColor Green
Write-Host "  msbuild src\Dynamo.D365.ItemTemplates\Dynamo.D365.ItemTemplates.csproj -restore"
