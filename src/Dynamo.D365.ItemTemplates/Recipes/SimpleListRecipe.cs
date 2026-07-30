using System;
using System.Collections.Generic;
using System.Globalization;
using Dynamo.D365.ItemTemplates.Metadata;
using Dynamo.D365.ItemTemplates.Ui;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Form con patron Simple List, con o sin tabla, menu item y privilegios.
    ///
    /// Cubre las seis combinaciones que existen como template; cada una es el mismo codigo con
    /// distintos flags en el .vstemplate:
    ///
    ///   $DynamoCreateTable$      crea la tabla ademas del form
    ///   $DynamoBaseTable$        tabla existente sobre la que va el form (si no se crea una)
    ///   $DynamoIncludeMenuItem$  display menu item apuntando al form
    ///   $DynamoIncludePrivileges$ privilegios con el menu item como entry point
    /// </summary>
    public sealed class SimpleListRecipe : IRecipe
    {
        public string Name
        {
            get { return "SimpleList"; }
        }

        public void Run(D365Workspace workspace, string elementName, IDictionary<string, string> parameters)
        {
            bool createTable = ParseBool(parameters.Value("$DynamoCreateTable$"), fallback: false);
            bool includeMenuItem = ParseBool(parameters.Value("$DynamoIncludeMenuItem$"), fallback: false);
            bool includePrivileges = ParseBool(parameters.Value("$DynamoIncludePrivileges$"), fallback: false);

            string label = parameters.Value("$DynamoLabel$", elementName);
            string keyFieldName = parameters.Value("$DynamoKeyField$", "Code");

            // Cuando la receta crea la tabla, el nombre que escribio el usuario es el de la
            // tabla y el form toma el mismo nombre. Si no, ese nombre es el del form y hay que
            // averiguar sobre que tabla va el grid: el dialogo de Add > New Item solo pide un
            // nombre, asi que se pregunta aparte.
            string formName = createTable ? parameters.Value("$DynamoFormName$", elementName) : elementName;
            string tableName = createTable
                ? elementName
                : parameters.Value("$DynamoBaseTable$") ?? AskForBaseTable(formName);

            if (createTable)
                workspace.Create(BuildTable(tableName, keyFieldName, label));

            workspace.Create(SimpleListFormBuilder.Build(formName, tableName, label));

            if (!includeMenuItem)
                return;

            workspace.Create(MenuItemBuilder.ForForm(formName, label));

            // Los privilegios necesitan un menu item al que referenciar.
            if (!includePrivileges)
                return;

            string levels = parameters.Value("$DynamoPrivilegeLevels$", PrivilegeBuilder.DefaultLevels);

            foreach (AxSecurityPrivilege privilege in PrivilegeBuilder.BuildAll(formName, formName, levels, label))
                workspace.Create(privilege);
        }

        /// <summary>
        /// Tabla pensada para que el grid del Simple List tenga algo que mostrar: el field
        /// group tiene que llamarse igual que el DataGroup del grid
        /// (<see cref="SimpleListFormBuilder.OverviewGroupName"/>), o el grid sale vacio.
        /// </summary>
        internal static AxTable BuildTable(string tableName, string keyFieldName, string label)
        {
            string indexName = keyFieldName + "Idx";
            const string descriptionFieldName = "Description";

            var table = new AxTable
            {
                Name = tableName,
                Label = label,
                TableType = TableType.Regular,
                CreatedDateTime = NoYes.Yes,
                CreatedBy = NoYes.Yes,
                ModifiedDateTime = NoYes.Yes,
                ModifiedBy = NoYes.Yes,
                CacheLookup = RecordCacheLevel.Found,
                ClusteredIndex = indexName,
                PrimaryIndex = indexName,
                ReplacementKey = indexName,
                TitleField1 = keyFieldName,
                TitleField2 = descriptionFieldName
            };

            table.SourceCode = new AxPropertyCollection
            {
                Declaration = string.Format(
                    CultureInfo.InvariantCulture,
                    "public class {0} extends common\r\n{{\r\n}}",
                    tableName)
            };

            table.Fields.Add(new AxTableFieldString
            {
                Name = keyFieldName,
                StringSize = 20,
                Mandatory = NoYes.Yes
            });

            table.Fields.Add(new AxTableFieldString
            {
                Name = descriptionFieldName,
                StringSize = 60
            });

            var index = new AxTableIndex
            {
                Name = indexName,
                AllowDuplicates = NoYes.No,
                AlternateKey = NoYes.Yes
            };
            index.Fields.Add(new AxTableIndexField { DataField = keyFieldName });
            table.Indexes.Add(index);

            var overview = new AxTableFieldGroup { Name = SimpleListFormBuilder.OverviewGroupName };
            overview.Fields.Add(new AxTableFieldGroupField { DataField = keyFieldName });
            overview.Fields.Add(new AxTableFieldGroupField { DataField = descriptionFieldName });
            table.FieldGroups.Add(overview);

            return table;
        }

        /// <summary>
        /// La tabla es opcional: un form no necesariamente tiene data source. Si se deja en
        /// blanco, el form se crea con la estructura del Simple List pero sin origen de datos,
        /// para completarlo en el diseñador. Cancelar si aborta la receta.
        /// </summary>
        private static string AskForBaseTable(string formName)
        {
            return InputDialog.PromptOrCancel(
                "DYNAMO - Form Simple List",
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Tabla sobre la que va el grid de '{0}'.{1}Dejalo en blanco para crear el form sin data source.",
                    formName,
                    Environment.NewLine));
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }
    }
}
