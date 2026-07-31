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

# El mismo assembly provee los item templates (via VSIX) y el add-in Fill Pattern (via MEF).
$AddinAssemblyName = 'Dynamo.D365.ItemTemplates.dll'

# Dependencias que no vienen con las herramientas de D365 y que MEF necesita poder resolver
# para inspeccionar el assembly.
$AddinDependencies = @(
    'envdte.dll'
    'Microsoft.VisualStudio.Interop.dll'
    'Microsoft.VisualStudio.TemplateWizardInterface.dll'
)

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

function Get-AddinDirectories {
    <#
        Los add-ins no se cargan desde la carpeta del VSIX: AddinFactory arma su catalogo MEF
        sobre AddinsEnvironmentHelper.AddinDirectories(), y la primera de esas rutas es
        <carpeta de las herramientas de D365>\AddinExtensions. Ahi va el assembly.

        El nombre de la carpeta de la extension es aleatorio por maquina, por eso se busca.
    #>
    param([Parameter(Mandatory)] $Instances)

    $directories = @()

    foreach ($instance in $Instances) {
        $extensions = Join-Path $instance.installationPath 'Common7\IDE\Extensions'

        if (-not (Test-Path $extensions)) { continue }

        $marker = Get-ChildItem $extensions -Recurse -Filter 'Microsoft.Dynamics.Framework.Tools.Extensibility.17.0.dll' -ErrorAction SilentlyContinue |
                  Select-Object -First 1

        if (-not $marker) { continue }

        $directories += Join-Path $marker.Directory.FullName 'AddinExtensions'
    }

    return $directories
}

function Install-Addin {
    param(
        # Sin Mandatory: al desinstalar no hay .vsix del que extraer nada, y un string vacio en
        # un parametro obligatorio hace que PowerShell lo pida por consola y cuelgue el script.
        [string] $VsixPath,
        [Parameter(Mandatory)] $Instances,
        [switch] $Remove
    )

    $directories = Get-AddinDirectories -Instances $Instances

    if ($directories.Count -eq 0) {
        Write-Warning 'No se encontraron las herramientas de D365; se omite el add-in (los item templates si quedan instalados).'
        return
    }

    foreach ($directory in $directories) {
        $target = Join-Path $directory $AddinAssemblyName

        if ($Remove) {
            if (Test-Path $target) {
                Remove-Item $target -Force
                Write-Host "  quitado  $target" -ForegroundColor Green
            }

            foreach ($name in $AddinDependencies) {
                $dep = Join-Path (Join-Path $directory 'Dependencies') $name
                if (Test-Path $dep) { Remove-Item $dep -Force }
            }

            continue
        }

        if (-not (Test-Path $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }

        # El assembly ya viene dentro del .vsix, que es un zip.
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($VsixPath)

        try {
            $entry = $zip.Entries | Where-Object { $_.Name -eq $AddinAssemblyName } | Select-Object -First 1

            if (-not $entry) {
                Write-Warning "  el .vsix no contiene $AddinAssemblyName"
                return
            }

            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            Write-Host "  OK       $target" -ForegroundColor Green

            # Si MEF no puede resolver una dependencia al inspeccionar el assembly, descarta
            # el add-in entero sin decir nada. AddinFactory mira una subcarpeta Dependencies y
            # resuelve desde ahi, asi que se dejan las que no son parte de las herramientas.
            $dependencies = Join-Path $directory 'Dependencies'

            foreach ($name in $AddinDependencies) {
                $dep = $zip.Entries | Where-Object { $_.Name -eq $name } | Select-Object -First 1

                if (-not $dep) { continue }

                if (-not (Test-Path $dependencies)) {
                    New-Item -ItemType Directory -Path $dependencies -Force | Out-Null
                }

                [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
                    $dep, (Join-Path $dependencies $name), $true)

                Write-Host "  OK       Dependencies\$name" -ForegroundColor DarkGray
            }
        }
        finally {
            $zip.Dispose()
        }
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

    Write-Host "Quitando el add-in..."
    Install-Addin -Instances $instances -Remove

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

    # El add-in va aparte: los item templates los resuelve el VSIX, pero el menu Addins se
    # carga por MEF desde la carpeta de las herramientas de D365.
    Write-Host "Instalando el add-in Fill Pattern..."
    Install-Addin -VsixPath $VsixPath -Instances $instances
}
finally {
    if ($downloaded -and (Test-Path $VsixPath)) {
        Remove-Item $VsixPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Listo." -ForegroundColor Green
Write-Host "  Templates:  click derecho en el proyecto > Add > New Item > Dynamics 365 Items > Dynamo"
Write-Host "  Add-in:     click derecho sobre un formulario > Addins > Fill Pattern"
