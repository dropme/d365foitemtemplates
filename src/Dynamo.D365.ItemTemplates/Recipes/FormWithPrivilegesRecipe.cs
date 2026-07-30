using System.Collections.Generic;
using Dynamo.D365.ItemTemplates.Metadata;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Receta 3: form + display menu item + privilegios de seguridad que referencian al menu
    /// item.
    ///
    /// Es la receta 1 mas la seguridad, para el caso de un form sobre tablas que ya existen:
    /// no crea ninguna tabla, pero deja el form publicado y accesible desde un rol.
    /// </summary>
    public sealed class FormWithPrivilegesRecipe : IRecipe
    {
        public string Name
        {
            get { return "FormWithPrivileges"; }
        }

        public void Run(D365Workspace workspace, string elementName, IDictionary<string, string> parameters)
        {
            string baseTable = parameters.Value("$DynamoBaseTable$");
            string pattern = parameters.Value("$DynamoFormPattern$", "SimpleListDetails");
            string label = parameters.Value("$DynamoLabel$", elementName);

            // 1. Form, con la tabla base como data source si se indico alguna.
            workspace.Create(FormWithMenuItemRecipe.BuildForm(elementName, baseTable, pattern, label));

            // 2. Display menu item apuntando al form.
            workspace.Create(FormWithMenuItemRecipe.BuildMenuItem(elementName, label));

            // 3. Un privilegio por nivel de acceso, con el menu item como entry point.
            string levels = parameters.Value("$DynamoPrivilegeLevels$", PrivilegeBuilder.DefaultLevels);

            foreach (AxSecurityPrivilege privilege in PrivilegeBuilder.BuildAll(elementName, elementName, levels, label))
                workspace.Create(privilege);
        }
    }
}
