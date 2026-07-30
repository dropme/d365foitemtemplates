<#
.SYNOPSIS
    Instala los item templates de D365 F&O en Visual Studio 2022.

.DESCRIPTION
    Descarga el .vsix del ultimo release de GitHub y lo instala en todas las instancias de
    Visual Studio 2022 de la maquina (o en las que se indiquen).

    Visual Studio tiene que estar cerrado: el instalador no puede modificar una instancia en
    uso.

.PARAMETER Version
    Tag del release a instalar (por ejemplo "v1.2.0"). Por defecto, el ultimo.

.PARAMETER VsixPath
    Instala un .vsix local en vez de descargarlo. Util para probar una compilacion propia.

.PARAMETER Uninstall
    Desinstala la extension en vez de instalarla.

.PARAMETER Force
    No aborta aunque Visual Studio este abierto.

.EXAMPLE
    .\Install-ItemTemplates.ps1

.EXAMPLE
    # Instalacion directa, sin clonar el repo
    irm https://raw.githubusercontent.com/dropme/d365foitemtemplates/main/install/Install-ItemTemplates.ps1 | iex

.EXAMPLE
    .\Install-ItemTemplates.ps1 -VsixPath ..\src\Dynamo.D365.ItemTemplates\bin\Debug\net48\Dynamo.D365.ItemTemplates.vsix

.EXAMPLE
    .\Install-ItemTemplates.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $VsixPath,
    [switch] $Uninstall,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

$Repo = 'dropme/d365foitemtemplates'

# Identity/@Id del source.extension.vsixmanifest. Es lo que identifica la extension ante VS:
# si cambia alla, hay que cambiarlo aca.
$ExtensionId = 'Dynamo.D365.ItemTemplates.9f3c1a4e-8b21-4d7a-9c55-2f0e6d1b7a83'

# Windows PowerShell 5.1 negocia TLS 1.0 por defecto; GitHub solo acepta 1.2+.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Get-VisualStudioInstances {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

    if (-not (Test-Path $vswhere)) {
        throw 'No se encontro vswhere.exe. Hace falta Visual Studio 2022.'
    }

    $json = & $vswhere -all -prerelease -version '[17.0,18.0)' -format json | Out-String
    $instances = $json | ConvertFrom-Json

    if (-not $instances) {
        throw 'No se encontro ninguna instalacion de Visual Studio 2022.'
    }

    return $instances
}

function Assert-VisualStudioClosed {
    $running = Get-Process devenv -ErrorAction SilentlyContinue

    if (-not $running) { return }

    if ($Force) {
        Write-Warning 'Visual Studio esta abierto. Continuo por -Force, pero la instalacion puede fallar.'
        return
    }

    throw 'Visual Studio esta abierto. Cerralo y volve a ejecutar (o usa -Force).'
}

function Get-ReleaseVsix {
    $uri = if ($Version) {
        "https://api.github.com/repos/$Repo/releases/tags/$Version"
    } else {
        "https://api.github.com/repos/$Repo/releases/latest"
    }

    Write-Host "Consultando release en $Repo..."

    try {
        $release = Invoke-RestMethod -Uri $uri -Headers @{ 'User-Agent' = 'd365fo-itemtemplates-installer' }
    }
    catch {
        throw "No se pudo obtener el release ($uri): $($_.Exception.Message)"
    }

    $asset = $release.assets | Where-Object { $_.name -like '*.vsix' } | Select-Object -First 1

    if (-not $asset) {
        throw "El release '$($release.tag_name)' no tiene ningun .vsix adjunto."
    }

    $target = Join-Path ([IO.Path]::GetTempPath()) $asset.name

    Write-Host "Descargando $($asset.name) ($($release.tag_name))..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $target -UseBasicParsing

    return $target
}

function Invoke-VsixInstaller {
    param(
        [Parameter(Mandatory)] $Instance,
        [Parameter(Mandatory)] [string[]] $Arguments
    )

    $installer = Join-Path $Instance.installationPath 'Common7\IDE\VSIXInstaller.exe'

    if (-not (Test-Path $installer)) {
        Write-Warning "  $($Instance.displayName): no se encontro VSIXInstaller.exe, se omite."
        return
    }

    $all = $Arguments + @('/quiet', "/instanceIds:$($Instance.instanceId)")
    $process = Start-Process -FilePath $installer -ArgumentList $all -Wait -PassThru -NoNewWindow

    switch ($process.ExitCode) {
        0        { Write-Host "  OK   $($Instance.displayName)" -ForegroundColor Green }
        1001     { Write-Host "  --   $($Instance.displayName): ya estaba instalada" -ForegroundColor Yellow }
        1002     { Write-Host "  --   $($Instance.displayName): no estaba instalada" -ForegroundColor Yellow }
        default  { Write-Warning "  FALLO $($Instance.displayName): VSIXInstaller devolvio $($process.ExitCode)" }
    }
}

# ---------------------------------------------------------------------------------------

Assert-VisualStudioClosed

$instances = Get-VisualStudioInstances
Write-Host "Instancias de Visual Studio 2022 encontradas: $($instances.Count)"

if ($Uninstall) {
    Write-Host "Desinstalando $ExtensionId..."

    foreach ($instance in $instances) {
        Invoke-VsixInstaller -Instance $instance -Arguments @("/uninstall:$ExtensionId")
    }

    Write-Host "`nListo." -ForegroundColor Green
    return
}

$downloaded = $false

if (-not $VsixPath) {
    $VsixPath = Get-ReleaseVsix
    $downloaded = $true
}

if (-not (Test-Path $VsixPath)) {
    throw "No existe el archivo: $VsixPath"
}

$VsixPath = (Resolve-Path $VsixPath).Path

try {
    # Desinstalar primero: VSIXInstaller no reemplaza una version ya instalada, la rechaza.
    Write-Host "Quitando version anterior (si la hay)..."

    foreach ($instance in $instances) {
        Invoke-VsixInstaller -Instance $instance -Arguments @("/uninstall:$ExtensionId")
    }

    Write-Host "Instalando $(Split-Path $VsixPath -Leaf)..."

    foreach ($instance in $instances) {
        Invoke-VsixInstaller -Instance $instance -Arguments @("`"$VsixPath`"")
    }
}
finally {
    if ($downloaded -and (Test-Path $VsixPath)) {
        Remove-Item $VsixPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Listo. Abri Visual Studio y busca los templates en:" -ForegroundColor Green
Write-Host "  click derecho en el proyecto > Add > New Item > Dynamics 365 Items"
