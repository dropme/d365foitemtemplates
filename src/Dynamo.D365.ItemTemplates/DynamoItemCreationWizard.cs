using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Dynamo.D365.ItemTemplates.Metadata;
using Dynamo.D365.ItemTemplates.Recipes;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;

namespace Dynamo.D365.ItemTemplates
{
    /// <summary>
    /// Wizard unico para todos los templates compuestos de DYNAMO.
    ///
    /// La receta a ejecutar llega por el CustomParameter $DynamoRecipe$ definido en el
    /// .vstemplate. Agregar un template que combine elementos con una receta ya existente no
    /// requiere recompilar; una receta nueva si, porque hay que registrarla en Recipes.
    ///
    /// Por que la creacion pasa en RunFinished y no en RunStarted
    /// ----------------------------------------------------------
    /// Es lo que hace el wizard de Microsoft (ItemCreationWizard, ver decompiled/). RunStarted
    /// corre antes de que VS termine de procesar el template; recien en RunFinished el
    /// proyecto activo esta en un estado consistente para agregarle elementos. Aca RunStarted
    /// solo valida y guarda estado.
    ///
    /// Toda la logica de creacion vive en Recipes + D365Workspace, sin dependencias de VS mas
    /// alla del DTE. Si el sistema de item templates deja de aceptar wizards de terceros entre
    /// PUs, las mismas recetas se cuelgan de un DesignerMenuBase (Add-in) sin cambios.
    /// </summary>
    public class DynamoItemCreationWizard : IWizard
    {
        private const string RecipeParameter = "$DynamoRecipe$";

        private static readonly IRecipe[] Recipes =
        {
            new FormWithMenuItemRecipe(),
            new FormWithPrivilegesRecipe(),
            new SimpleListRecipe(),
            new TableSuiteRecipe(),
            new TableParametersRecipe(),
            new SysOperationRecipe()
        };

        private DTE _dte;
        private string _elementName;
        private IRecipe _recipe;
        private IDictionary<string, string> _parameters;

        public void RunStarted(
            object automationObject,
            Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind,
            object[] customParams)
        {
            _dte = automationObject as DTE;
            _parameters = replacementsDictionary;

            // El wizard de Microsoft usa $rootname$, no $safeitemname$: es el nombre tal cual
            // lo escribio el usuario, que es tambien el nombre del elemento del AOT.
            _elementName = replacementsDictionary.Value("$rootname$")
                ?? replacementsDictionary.Value("$safeitemname$");

            if (string.IsNullOrWhiteSpace(_elementName))
                throw new WizardBackoutException("No se pudo determinar el nombre del elemento.");

            _recipe = ResolveRecipe(replacementsDictionary, customParams);
        }

        public void RunFinished()
        {
            try
            {
                var workspace = new D365Workspace(_dte);

                _recipe.Run(workspace, _elementName, _parameters);

                // Un solo Commit al final: es lo que permite que los elementos se repartan en
                // las carpetas por tipo en vez de quedar colgando de la raiz del proyecto.
                workspace.Commit();
            }
            catch (WizardBackoutException)
            {
                // El usuario cancelo: propagar sin ruido, VS no muestra error.
                throw;
            }
            catch (OperationCanceledException)
            {
                // Cancelo alguno de los dialogos de parametros de la receta.
                throw new WizardBackoutException();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "DYNAMO - Error creando los elementos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                throw new WizardBackoutException(ex.Message, ex);
            }
        }

        /// <summary>
        /// La receta sale de $DynamoRecipe$. Si el .vstemplate no lo declara, se cae al nombre
        /// del archivo, que es como Microsoft resuelve el tipo de elemento (customParams[0]
        /// es la ruta completa del .vstemplate).
        /// </summary>
        private static IRecipe ResolveRecipe(IDictionary<string, string> parameters, object[] customParams)
        {
            string name = parameters.Value(RecipeParameter);

            if (string.IsNullOrWhiteSpace(name))
            {
                string templatePath = customParams != null && customParams.Length > 0
                    ? customParams[0] as string
                    : null;

                if (templatePath != null)
                    name = Path.GetFileNameWithoutExtension(templatePath);
            }

            IRecipe recipe = Recipes.FirstOrDefault(
                r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            if (recipe == null)
                throw new InvalidOperationException(string.Format(
                    "Receta desconocida: '{0}'. Revisa {1} en el .vstemplate.", name, RecipeParameter));

            return recipe;
        }

        // ------------------------------------------------------- resto de IWizard ----
        // Con <TemplateContent/> sin ProjectItem, VS no llama a ShouldAddProjectItem ni a
        // ProjectItemFinishedGenerating. Se implementan igual por contrato.

        public bool ShouldAddProjectItem(string filePath)
        {
            return false;
        }

        public void ProjectFinishedGenerating(Project project)
        {
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
        }

        public void BeforeOpeningFile(ProjectItem projectItem)
        {
        }
    }
}
