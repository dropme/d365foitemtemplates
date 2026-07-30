using System.Collections.Generic;
using System.Globalization;
using Dynamo.D365.ItemTemplates.Metadata;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Tabla de parametros: la tabla de registro unico que lleva la configuracion de un modulo.
    ///
    /// Se crea con el patron completo, que es lo que cuesta recordar:
    ///   - un campo clave Integer siempre en 0, con su indice primario y clustered
    ///   - delete() y validateDelete() bloqueados, para que el registro no se pueda borrar
    ///   - find() que crea el registro la primera vez via Company::createParameter()
    ///
    /// Una AxTable guarda su X++ igual que una AxClass: SourceCode.Declaration con la firma y
    /// el cuerpo vacio, y un AxMethod por metodo. Ambas heredan esas propiedades de tipos
    /// distintos, asi que no comparten builder, pero el armado es el mismo (ver ClassBuilder).
    /// </summary>
    public sealed class TableParametersRecipe : IRecipe
    {
        private const string KeyFieldName = "Key";
        private const string KeyIndexName = "Key";

        public string Name
        {
            get { return "TableParameters"; }
        }

        public void Run(D365Workspace workspace, string elementName, IDictionary<string, string> parameters)
        {
            string label = parameters.Value("$DynamoLabel$", elementName);

            workspace.Create(BuildTable(elementName, label));
        }

        internal static AxTable BuildTable(string tableName, string label)
        {
            var table = new AxTable
            {
                Name = tableName,
                Label = label,
                TableGroup = TableGroup.Parameter,
                TableContents = TableContents.DefaultData,
                CacheLookup = RecordCacheLevel.Found,
                AllowRowVersionChangeTracking = NoYes.Yes,
                ClusteredIndex = KeyIndexName,
                PrimaryIndex = KeyIndexName
            };

            // El registro unico se identifica por Key == 0.
            table.Fields.Add(new AxTableFieldInt
            {
                Name = KeyFieldName,
                Visible = NoYes.No
            });

            var index = new AxTableIndex
            {
                Name = KeyIndexName,
                AllowDuplicates = NoYes.No,
                AlternateKey = NoYes.Yes
            };
            index.Fields.Add(new AxTableIndexField { DataField = KeyFieldName });
            table.Indexes.Add(index);

            table.SourceCode = new AxPropertyCollection
            {
                Declaration = Format(Declaration, tableName)
            };

            table.Methods.Add(new AxMethod { Name = "validateDelete", Source = ValidateDelete });
            table.Methods.Add(new AxMethod { Name = "delete", Source = Delete });
            table.Methods.Add(new AxMethod { Name = "find", Source = Format(Find, tableName) });

            return table;
        }

        // ------------------------------------------------------------------------- X++ ----
        // Las llaves van duplicadas donde la plantilla pasa por string.Format.

        private const string Declaration =
@"public class {0} extends common
{{
}}";

        private const string ValidateDelete =
@"    boolean validateDelete()
    {
        return false;
    }";

        private const string Delete =
@"    void delete()
    {
        throw error(""@SYS23721"");
    }";

        private const string Find =
@"    static {0} find(boolean _forupdate = false)
    {{
        {0} parameter;

        if (_forupdate)
        {{
            parameter.selectForUpdate(_forupdate);
        }}

        select firstonly parameter
            index Key
            where parameter.Key == 0;

        if (!parameter && !parameter.isTmp())
        {{
            Company::createParameter(parameter);
        }}

        return parameter;
    }}";

        private static string Format(string template, string tableName)
        {
            return string.Format(CultureInfo.InvariantCulture, template, tableName);
        }
    }
}
