using System.Collections.Generic;
using System.Globalization;
using Dynamo.D365.ItemTemplates.Metadata;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Receta 1: un Form y un Display Menu Item apuntando a ese Form.
    ///
    /// Si se indica una tabla base ($DynamoBaseTable$), el form se crea con esa tabla como
    /// data source.
    /// </summary>
    public sealed class FormWithMenuItemRecipe : IRecipe
    {
        public string Name
        {
            get { return "FormWithMenuItem"; }
        }

        public void Run(D365Workspace workspace, string elementName, IDictionary<string, string> parameters)
        {
            string baseTable = parameters.Value("$DynamoBaseTable$");
            string pattern = parameters.Value("$DynamoFormPattern$", "SimpleListDetails");
            string label = parameters.Value("$DynamoLabel$", elementName);

            AxForm form = BuildForm(elementName, baseTable, pattern, label);
            workspace.Create(form);

            AxMenuItemDisplay menuItem = BuildMenuItem(elementName, label);
            workspace.Create(menuItem);
        }

        /// <summary>
        /// Declaracion de clase del form.
        ///
        /// A diferencia de una AxClass o una AxTable, que la llevan en SourceCode.Declaration,
        /// un AxForm la lleva como un metodo llamado "classDeclaration". Sin el, el elemento se
        /// crea y se ve bien en el AOT, pero al compilar falla con:
        ///   The 'classDeclaration' is missing from element '&lt;form&gt;'.
        /// </summary>
        private const string ClassDeclaration =
"[Form]\r\npublic class {0} extends FormRun\r\n{{\r\n}}";

        internal static AxForm BuildForm(string formName, string baseTable, string pattern, string label)
        {
            var form = new AxForm
            {
                Name = formName,
                Design = new AxFormDesign
                {
                    Caption = label,
                    Pattern = pattern
                }
            };

            form.Methods.Add(new AxMethod
            {
                Name = "classDeclaration",
                Source = string.Format(CultureInfo.InvariantCulture, ClassDeclaration, formName)
            });

            if (!string.IsNullOrWhiteSpace(baseTable))
            {
                form.DataSources.Add(new AxFormDataSourceRoot
                {
                    Name = baseTable,
                    Table = baseTable,
                    AllowCreate = NoYes.Yes,
                    AllowEdit = NoYes.Yes,
                    AllowDelete = NoYes.Yes
                });

                // Titulo del form tomado del registro activo del data source.
                form.Design.TitleDataSource = baseTable;
            }

            return form;
        }

        internal static AxMenuItemDisplay BuildMenuItem(string formName, string label)
        {
            return MenuItemBuilder.ForForm(formName, label);
        }
    }
}
