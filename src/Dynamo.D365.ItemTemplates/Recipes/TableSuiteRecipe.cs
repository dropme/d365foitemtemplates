using System;
using System.Collections.Generic;
using Dynamo.D365.ItemTemplates.Metadata;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Receta 2: tabla + form (con la tabla como data source) + display menu item +
    /// privilegios de seguridad que referencian al menu item.
    ///
    /// El orden de creacion no es arbitrario: cada elemento referencia por nombre al anterior,
    /// y el metadata provider valida las referencias al persistir.
    /// </summary>
    public sealed class TableSuiteRecipe : IRecipe
    {
        public string Name
        {
            get { return "TableSuite"; }
        }

        public void Run(D365Workspace workspace, string elementName, IDictionary<string, string> parameters)
        {
            string tableName = elementName;
            string formName = parameters.Value("$DynamoFormName$", tableName);
            string keyFieldName = parameters.Value("$DynamoKeyField$", "Code");
            string label = parameters.Value("$DynamoLabel$", tableName);
            string pattern = parameters.Value("$DynamoFormPattern$", "SimpleList");

            // 1. Tabla, con campo clave, indice unico y grupo Overview.
            workspace.Create(BuildTable(tableName, keyFieldName, label));

            // 2. Form con la tabla como data source.
            workspace.Create(FormWithMenuItemRecipe.BuildForm(formName, tableName, pattern, label));

            // 3. Display menu item apuntando al form.
            workspace.Create(FormWithMenuItemRecipe.BuildMenuItem(formName, label));

            // 4. Un privilegio por nivel de acceso, con el menu item como entry point.
            string levels = parameters.Value("$DynamoPrivilegeLevels$", PrivilegeBuilder.DefaultLevels);

            foreach (AxSecurityPrivilege privilege in PrivilegeBuilder.BuildAll(tableName, formName, levels, label))
                workspace.Create(privilege);
        }

        internal static AxTable BuildTable(string tableName, string keyFieldName, string label)
        {
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
                FormRef =  tableName 
            };

            return table;
        }

    }
}
