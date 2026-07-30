# Item templates compuestos para D365 F&O

Entradas propias en *Add > New Item* de un proyecto FinanceOperations que crean varios
elementos del AOT ya conectados entre sí.

| Template | Categoría | Crea |
| --- | --- | --- |
| Form + Display Menu Item | User Interface | `AxForm` (con data source opcional) + `AxMenuItemDisplay` apuntando al form |
| Form + Display Menu Item + Privilegios | User Interface | Lo anterior + un `AxSecurityPrivilege` por nivel de acceso. Para un form sobre tablas que ya existen |
| Table + Form + Menu Item + Privilegios | Data Model | `AxTable` + `AxForm` con la tabla como data source + `AxMenuItemDisplay` + un `AxSecurityPrivilege` por nivel de acceso |
| Table Parameters | Data Model | Tabla de parámetros con el patrón completo: campo clave `Key`, índice primario y clustered, `delete()`/`validateDelete()` bloqueados y `find()` que crea el registro único |
| SysOperation: Controller + Service | Code | `<Nombre>Controller` + `<Nombre>Service`, enlazadas por `classStr`/`methodStr`. `process()` sin parámetros |
| SysOperation: … + Contract | Code | Lo anterior + `<Nombre>Contract` con `[DataContractAttribute]`, y `process()` recibiéndolo |
| SysOperation: … + Menu Item | Code | Controller + service + `AxMenuItemDisplay` que llama al controller (`ObjectType = Class`) |
| SysOperation: … + Menu Item + Privilegios | Code | Lo anterior + un `AxSecurityPrivilege` por nivel, con el menu item como entry point |

Las cuatro variantes de SysOperation son **la misma receta**: cambian solo los flags
`$DynamoIncludeContract$`, `$DynamoIncludeMenuItem$` y `$DynamoIncludePrivileges$`. Cualquier
otra combinación (por ejemplo contract *y* menu item) es copiar un `.vstemplate` y cambiar los
valores — no requiere recompilar.

Los privilegios se llaman `<Nombre><Nivel>` y salen de `$DynamoPrivilegeLevels$` (por defecto
`View,Maintain`). `View` concede solo lectura; cualquier otro nivel concede acceso completo.

En los templates de SysOperation el usuario escribe el **nombre base**; los sufijos los pone
la receta. Si escribe el nombre ya con sufijo (`MiProcesoController`), se lo quita para que las
tres clases queden parejas.

## Instalación

Con Visual Studio **cerrado**, en PowerShell:

```powershell
irm https://raw.githubusercontent.com/dropme/d365foitemtemplates/main/install/Install-ItemTemplates.ps1 | iex
```

Descarga el `.vsix` del último release y lo instala en todas las instancias de Visual Studio
2022 de la máquina. Después, los templates aparecen en *click derecho en el proyecto > Add >
New Item*.

Para pasar parámetros hay que bajar el script primero:

```powershell
irm https://raw.githubusercontent.com/dropme/d365foitemtemplates/main/install/Install-ItemTemplates.ps1 -OutFile install.ps1

.\install.ps1 -Version v1.2.0     # una versión puntual
.\install.ps1 -Uninstall          # desinstalar
.\install.ps1 -VsixPath .\mi.vsix # un .vsix propio, para probar
```

### "cannot be loaded because running scripts is disabled"

Por defecto Windows no ejecuta archivos `.ps1`. Habilitalo solo para esa ventana:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

O ejecutá el script sin tocar la política:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

El `irm | iex` de arriba **no** se ve afectado: la política aplica a archivos en disco, y ahí
el script nunca llega a serlo.

El script desinstala la versión anterior antes de instalar, porque `VSIXInstaller` rechaza una
extensión que ya está presente en vez de reemplazarla.

### Clases X++

Una `AxClass` se guarda partida en dos: `SourceCode.Declaration` (atributos y firma de la
clase, con el cuerpo vacío) y un `AxMethod` por método, cada uno con su `Source` completo.

Microsoft arma eso con `BuildHelper.ParseSourceCodeString`, que parsea el X++ y lo reparte
solo, pero vive en `Tools.BuildTasks`. Como las recetas conocen sus plantillas de antemano,
[ClassBuilder](src/Dynamo.D365.ItemTemplates/Recipes/ClassBuilder.cs) recibe las partes ya
separadas y evita esa dependencia.

## Estado

El assembly compila y el VSIX se genera. **Falta probarlo en una VM con las herramientas de
D365 instaladas**: acá no hay forma de ejecutar el wizard.

```powershell
msbuild src\Dynamo.D365.ItemTemplates\Dynamo.D365.ItemTemplates.csproj -restore
# -> src\Dynamo.D365.ItemTemplates\bin\Debug\net48\Dynamo.D365.ItemTemplates.vsix
```

Los `.ico` son placeholders generados (un cuadrado de color). Reemplazalos cuando haya
íconos de verdad.

## Cómo funciona

Todo lo de abajo está verificado contra el código decompilado de las herramientas, que quedó
se obtuvo decompilándolas con [ILSpy](https://github.com/icsharpcode/ILSpy). Ese material no
se versiona (es código de Microsoft); para reproducirlo:

```powershell
dotnet tool install -g ilspycmd --version 8.2.0.7535
ilspycmd -t Microsoft.Dynamics.Framework.Tools.ProjectSystem.ItemCreationWizard `
         lib\Microsoft.Dynamics.Framework.Tools.ProjectSystem.17.0.dll
```

### El .vstemplate no copia archivos

`<TemplateContent/>` va vacío, igual que en los templates de Microsoft: no se copia ningún
archivo al proyecto. Todo lo crea el wizard vía la API de metadatos. Lo único que agregamos
es un `<CustomParameter>` que le dice al wizard qué receta correr:

```xml
<TemplateContent>
  <CustomParameters>
    <CustomParameter Name="$DynamoRecipe$" Value="FormWithMenuItem" />
  </CustomParameters>
</TemplateContent>
```

El valor llega a `RunStarted` dentro de `replacementsDictionary`.

### La creación pasa en RunFinished, no en RunStarted

Es lo que hace `ItemCreationWizard` de Microsoft:
`RunStarted` solo guarda estado; recién en `RunFinished` el proyecto activo está en un estado
consistente para agregarle elementos.

El nombre del elemento sale de **`$rootname$`**, no de `$safeitemname$`.

### Cómo se crea un elemento

Microsoft usa `VSProjectUtil.AddElementToActiveProject(...)`, que es `internal` y por lo tanto
inalcanzable desde un assembly de terceros. La misma secuencia se arma con tipos públicos:

```csharp
var projectNode = (VSProjectNode)dteProject.Object;
var modelInfo = projectNode.GetProjectsModelInfo(throwIfNotExists: true);

var form = new AxForm { Name = name };
DesignMetaModelService.Instance.Create(form, new ModelSaveInfo(modelInfo));   // escribe el XML en el modelo

// ...y al final, una sola vez para todos los elementos creados:
projectNode.AddModelElementsToProject(metadataReferences, openItemOnAdd: false);
```

El orden importa: primero `Create` (persiste), después agregar al proyecto, que referencia el
archivo por ruta.

### Por qué una sola llamada al final, y no una por elemento

`IDynamicsProjectService.AddElementToActiveProject(metadata)` es más simple, pero **siempre
cuelga el elemento de la raíz del proyecto**: internamente pasa el `ID` del nodo raíz como
padre y no tiene parámetro para otra cosa. Los elementos quedan sueltos, fuera de las carpetas
*Tables*, *Classes*, *Forms*.

`VSProjectNode.AddModelElementsToProject` es el camino que usa *Add existing element*: respeta
la opción **Organize elements in project**, crea las carpetas por tipo y reparte cada elemento
en la suya. Toma la lista completa, porque necesita ver todos los tipos juntos. De ahí que
`D365Workspace` separe `Create` (por elemento) de `Commit` (una vez, al final).

`GetProjectsModelInfo` viene de `IDynamicsProject` (`MetaModel.Core`, público) y resuelve el
modelo del proyecto sin tener que leer el `.rnrproj` a mano — `Project.Properties.Item("Model")`
no sirve, el sistema de proyectos de D365 no expone esa propiedad por automation.

Esto agrega dos referencias (`ProjectSystem.17.0` y `ProjectSupport.17.0`, de donde sale la
clase base `ProjectNode`). La clase y el método son públicos, pero son parte de las
herramientas: si en alguna PU dejan de resolver, el plan B es `IDynamicsProject.AddItemFromModelStore`
—público, en `MetaModel.Core`— creando las carpetas a mano.

### Recetas compuestas

Microsoft ya tiene un wizard que crea varios elementos de una:
`WorkflowItemController`. El patrón es una llamada por
elemento, cada una con su initializer. `TableSuiteRecipe` es ese mismo patrón.

## Estructura

```
ItemTemplates/FinanceOperations/   <- coincide con <ProjectType>
  Dynamics 365 Items/                <- nodo padre del árbol, el mismo que usa Microsoft
    Dynamo/                          <- todo lo nuestro junto
      User Interface/ Data Model/ Code/
src/Dynamo.D365.ItemTemplates/
  Dynamo.D365.ItemTemplates.pkgdef   <- binding path para resolver el assembly del wizard
  DynamoItemCreationWizard.cs        <- IWizard, delgado: resuelve la receta y la corre
  Metadata/D365Workspace.cs          <- crear / persistir / agregar al proyecto
  Recipes/                           <- una clase por receta, más los builders compartidos
install/Install-ItemTemplates.ps1  <- instalador para las VMs
tools/
  Get-D365Assemblies.ps1             <- trae los assemblies de D365 a lib\
  Test-Templates.ps1                 <- valida los .vstemplate (corre en CI)
  New-Release.ps1                    <- compila y publica el release
lib/                               <- assemblies de D365 (NO versionado, ver abajo)
```

La **carpeta** bajo `ItemTemplates\FinanceOperations\` determina bajo qué nodo del árbol
aparece el template, y hay que replicar la jerarquía de Microsoft exactamente. Si se omite el
nivel `Dynamics 365 Items`, las categorías cuelgan sueltas de `FinanceOperations`, al lado del
nodo de Microsoft en vez de dentro. `NumberOfParentCategoriesToRollUp=1` hace que los
templates también aparezcan al seleccionar `Dynamo`, sin entrar a cada categoría.

### El .pkgdef, o por qué el template no hacía nada

Declarar el asset `Microsoft.VisualStudio.Assembly` **no alcanza**: instala el assembly en la
carpeta de la extensión, pero cuando un `.vstemplate` nombra su `<WizardExtension>` por nombre
simple, VS solo lo busca en la GAC, en `PrivateAssemblies` y en las rutas registradas como
*binding paths*. Al no encontrarlo, el diálogo acepta el template y **no pasa nada** — falla
en silencio, sin error.

`Dynamo.D365.ItemTemplates.pkgdef` registra la carpeta de la extensión:

```
[$RootKey$\BindingPaths\{74d91b4d-3679-4403-ae61-82fc7176a7fd}]
"$PackageFolder$"=""
```

Se escribe a mano porque `GeneratePkgDefFile` lo genera a partir de atributos de registro
sobre un VSPackage, y esta extensión no tiene ninguno: es solo templates y un `IWizard`.

`D365Workspace` y `Recipes` no dependen de `IWizard`: solo necesitan el `DTE`. Si el sistema
de proyectos deja de aceptar wizards de terceros entre PUs, las mismas recetas se cuelgan de
un `DesignerMenuBase` (Add-in, punto de extensión soportado) sin cambios. `IDynamicsProjectService`
vive justamente en el assembly de add-ins.

## Compilar

Hace falta una máquina con las **herramientas de desarrollo de D365** instaladas: el proyecto
referencia assemblies que vienen con ellas.

```powershell
.\tools\Get-D365Assemblies.ps1      # copia los assemblies a lib\ (una sola vez)
msbuild src\Dynamo.D365.ItemTemplates\Dynamo.D365.ItemTemplates.csproj -restore
```

`Get-D365Assemblies.ps1` **busca** la carpeta de la extensión de D365 en vez de hardcodearla:
su nombre es aleatorio por máquina y por versión de las herramientas. Para apuntar a una
instalación sin copiar nada:

```powershell
msbuild ... /p:D365ToolsPath="C:\...\Common7\IDE\Extensions\<random>"
```

Las referencias van con `Copy Local = No` a propósito: esos assemblies ya están cargados en el
proceso de Visual Studio. Redistribuirlos rompe la identidad de tipos.

### Por qué lib/ no se versiona

Son binarios de Microsoft y este repo es público, así que quedan fuera (ver `.gitignore`) y
cada quien los obtiene de su propia instalación con `Get-D365Assemblies.ps1`. Por el mismo
motivo quedan fuera el código decompilado de las herramientas y las notas internas de trabajo.

La consecuencia es que **un runner hospedado por GitHub no puede compilar el VSIX**. Por eso:

- `validate.yml` corre en cada push y valida los `.vstemplate` — eso sí funciona sin los
  assemblies, y es lo que se rompe en silencio (un template mal armado no da error de
  compilación: simplemente no aparece en *Add > New Item*).
- El release se publica desde una VM de desarrollo.

## Publicar una versión

Desde una VM de desarrollo, con [gh CLI](https://cli.github.com/) autenticado:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass   # si da "running scripts is disabled"
.\tools\New-Release.ps1 -Version 1.0.0
```

Actualiza la versión del manifest, compila en Release y crea el tag y el release con el
`.vsix` adjunto — que es lo que después baja el instalador.

Alternativa: `release.yml` hace lo mismo al pushear un tag `vN.N.N`, pero necesita un **runner
self-hosted** registrado en una VM de D365 con la etiqueta `d365`. Sin ese runner el workflow
queda en cola sin ejecutarse; usá el script.

El import de `Microsoft.VsSDK.targets` es manual y va **después** de `Sdk.targets` — el paquete
`Microsoft.VSSDK.BuildTools` no engancha sus targets solo en proyectos SDK-style.

## Por qué VSIX y no copiar los templates a mano

Copiar los `.vstemplate` a la carpeta de Microsoft + `devenv /installvstemplates` sirve para
validar rápido que la entrada aparece, pero **no es despliegue**: cualquier update de las
herramientas reemplaza esa carpeta y se pierde todo. Además hay que resolver el assembly del
wizard por separado (GAC o `PrivateAssemblies`).

El VSIX resuelve el assembly solo, sobrevive a los updates y se versiona. El assembly no está
firmado, así que el `<Assembly>` del `WizardExtension` usa el nombre simple.

## Advertencia

`<ProjectType>FinanceOperations</ProjectType>` y el comportamiento del sistema de proyectos de
D365 frente a un wizard de terceros no están documentados como punto de extensión. Puede
romperse entre PUs. De ahí que la lógica de creación esté aislada del `IWizard`.
