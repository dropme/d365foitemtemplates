<#
.SYNOPSIS
    Valida los .vstemplate del repo.

.DESCRIPTION
    Comprueba lo que se rompe en silencio: un template mal armado no da error de compilacion,
    simplemente no aparece en Add > New Item, o aparece y falla al ejecutarse.

    Se valida:
      - XML bien formado y ProjectType correcto
      - que el <Icon> exista en la carpeta del template
      - que el WizardExtension apunte al assembly y a la clase correctos
      - que $DynamoRecipe$ este declarado y su valor sea una receta que exista en el codigo

    No necesita los assemblies de D365, asi que corre en CI.

.EXAMPLE
    .\tools\Test-Templates.ps1
#>
[CmdletBinding()]
param(
    [string] $TemplatesPath = (Join-Path $PSScriptRoot '..\ItemTemplates'),
    [string] $SourcePath = (Join-Path $PSScriptRoot '..\src')
)

$ErrorActionPreference = 'Stop'

$expectedAssembly = 'Dynamo.D365.ItemTemplates'
$expectedClass = 'Dynamo.D365.ItemTemplates.DynamoItemCreationWizard'
$expectedProjectType = 'FinanceOperations'

# Recetas declaradas en el codigo: get { return "X"; } dentro de las clases *Recipe.
$knownRecipes = Get-ChildItem $SourcePath -Recurse -Filter '*Recipe.cs' |
    ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        if ($content -match 'get\s*\{\s*return\s*"([^"]+)"\s*;\s*\}') { $Matches[1] }
    } |
    Sort-Object -Unique

if (-not $knownRecipes) {
    Write-Error 'No se encontro ninguna receta en el codigo. Reviso el patron de busqueda?'
    exit 1
}

Write-Host "Recetas en el codigo: $($knownRecipes -join ', ')"
Write-Host ""

$templates = Get-ChildItem $TemplatesPath -Recurse -Filter '*.vstemplate'

if (-not $templates) {
    Write-Error "No se encontro ningun .vstemplate bajo $TemplatesPath"
    exit 1
}

$errors = @()

foreach ($template in $templates) {
    $relative = $template.FullName.Substring((Resolve-Path $TemplatesPath).Path.Length).TrimStart('\')
    $problems = @()

    try {
        $xml = [xml](Get-Content $template.FullName -Raw)
    }
    catch {
        $errors += "$relative : XML invalido -- $($_.Exception.Message)"
        Write-Host "  FALLO  $relative" -ForegroundColor Red
        continue
    }

    $ns = New-Object Xml.XmlNamespaceManager $xml.NameTable
    $ns.AddNamespace('vs', 'http://schemas.microsoft.com/developer/vstemplate/2005')

    $data = $xml.SelectSingleNode('/vs:VSTemplate/vs:TemplateData', $ns)

    if (-not $data) {
        $problems += 'falta TemplateData'
    }
    else {
        $projectType = $data.SelectSingleNode('vs:ProjectType', $ns).'#text'
        if ($projectType -ne $expectedProjectType) {
            $problems += "ProjectType es '$projectType', se esperaba '$expectedProjectType'"
        }

        $iconNode = $data.SelectSingleNode('vs:Icon', $ns)
        if ($iconNode) {
            $icon = Join-Path $template.Directory.FullName $iconNode.'#text'
            if (-not (Test-Path $icon)) {
                $problems += "el icono '$($iconNode.'#text')' no existe"
            }
        }
    }

    $wizard = $xml.SelectSingleNode('/vs:VSTemplate/vs:WizardExtension', $ns)

    if (-not $wizard) {
        $problems += 'falta WizardExtension'
    }
    else {
        $assembly = $wizard.SelectSingleNode('vs:Assembly', $ns).'#text'
        $class = $wizard.SelectSingleNode('vs:FullClassName', $ns).'#text'

        if ($assembly -ne $expectedAssembly) { $problems += "Assembly es '$assembly'" }
        if ($class -ne $expectedClass) { $problems += "FullClassName es '$class'" }
    }

    $recipeParam = $xml.SelectNodes('//vs:CustomParameter', $ns) |
        Where-Object { $_.Name -eq '$DynamoRecipe$' } |
        Select-Object -First 1

    if (-not $recipeParam) {
        $problems += 'falta el CustomParameter $DynamoRecipe$'
    }
    elseif ($knownRecipes -notcontains $recipeParam.Value) {
        $problems += "la receta '$($recipeParam.Value)' no existe en el codigo"
    }

    if ($problems.Count -gt 0) {
        Write-Host "  FALLO  $relative" -ForegroundColor Red
        foreach ($problem in $problems) {
            Write-Host "         - $problem" -ForegroundColor Red
            $errors += "$relative : $problem"
        }
    }
    else {
        Write-Host "  OK     $relative" -ForegroundColor Green
    }
}

Write-Host ""

if ($errors.Count -gt 0) {
    Write-Host "$($errors.Count) problema(s) en $($templates.Count) template(s)." -ForegroundColor Red
    exit 1
}

Write-Host "$($templates.Count) template(s), todo OK." -ForegroundColor Green
