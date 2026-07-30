using System;
using System.Globalization;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Form con el patron Simple List: action pane, grupo de filtros con quick filter, y un
    /// grid sobre el data source.
    ///
    /// La estructura sale de TRUDUtilsD365 (FormBuilder/FormBuilderParms.cs, DoFormCreate,
    /// case FormTemplateType.SimpleList), que es la referencia mas usada para esto:
    ///
    ///   Design
    ///     MainActionPane   (AxFormActionPaneControl)
    ///     FilterGroup      (patron CustomAndQuickFilters)
    ///       QuickFilter    (extension QuickFilterControl -> targetControlName = MainGrid)
    ///     MainGrid         (AxFormGridControl sobre el data source)
    ///       Overview       (grupo con DataGroup = Overview)
    ///
    /// Ojo con los nombres: el quick filter apunta al grid por nombre ("MainGrid"), y el grupo
    /// del grid usa el field group "Overview" de la tabla. Si esos nombres no coinciden con lo
    /// que hay en la tabla, el form compila pero el grid sale vacio.
    /// </summary>
    internal static class SimpleListFormBuilder
    {
        public const string GridName = "MainGrid";
        public const string OverviewGroupName = "Overview";

        /// <summary>
        /// Nombres de patron tal como los espera el catalogo (sin espacios; el diseñador los
        /// muestra como "Simple List" y "Custom and Quick Filters"). La version la resuelve
        /// <see cref="PatternApplier"/>.
        /// </summary>
        public const string PatternName = "SimpleList";

        private const string FilterGroupPatternName = "CustomAndQuickFilters";

        /// <summary>
        /// A diferencia de una AxClass, un AxForm lleva su declaracion como un metodo llamado
        /// "classDeclaration". Sin el, el form compila con
        /// "The 'classDeclaration' is missing from element".
        /// </summary>
        private const string ClassDeclaration =
"[Form]\r\npublic class {0} extends FormRun\r\n{{\r\n}}";

        public static AxForm Build(string formName, string tableName, string label)
        {
            if (string.IsNullOrWhiteSpace(formName))
                throw new ArgumentNullException("formName");

            // Sin tabla no hay data source ni grid ligado: el form se arma igual, pero el grid
            // queda sin origen para que se complete a mano. Antes esto reventaba con
            // "Value cannot be null. Parameter name: item.Name" al agregar el data source.
            bool hasTable = !string.IsNullOrWhiteSpace(tableName);

            var form = new AxForm
            {
                Name = formName,
                Design = new AxFormDesign
                {
                    Caption = label,
                    DataSource = hasTable ? tableName : null,
                    TitleDataSource = hasTable ? tableName : null
                }
            };

            form.Methods.Add(new AxMethod
            {
                Name = "classDeclaration",
                Source = string.Format(CultureInfo.InvariantCulture, ClassDeclaration, formName)
            });

            if (hasTable)
            {
                form.DataSources.Add(new AxFormDataSourceRoot
                {
                    Name = tableName,
                    Table = tableName,
                    InsertIfEmpty = NoYes.No
                });
            }

            AxFormGroupControl filterGroup = BuildFilterGroup();

            form.Design.Controls.Add(new AxFormActionPaneControl { Name = "MainActionPane" });
            form.Design.Controls.Add(filterGroup);
            form.Design.Controls.Add(BuildGrid(hasTable ? tableName : null));

            // Los patrones se aplican al final, con la estructura ya armada: ApplyPattern
            // valida que los controles se correspondan con el patron.
            PatternApplier.Apply(filterGroup, FilterGroupPatternName);
            PatternApplier.Apply(form.Design, PatternName);

            return form;
        }

        private static AxFormGroupControl BuildFilterGroup()
        {
            var filterGroup = new AxFormGroupControl { Name = "FilterGroup" };

            var quickFilter = new AxFormControlExtension { Name = "QuickFilterControl" };

            quickFilter.ExtensionProperties.Add(new AxFormControlExtensionProperty
            {
                Name = "targetControlName",
                Type = CompilerBaseType.String,
                Value = GridName
            });

            filterGroup.Controls.Add(new AxFormControl
            {
                Name = "QuickFilter",
                FormControlExtension = quickFilter
            });

            return filterGroup;
        }

        private static AxFormGridControl BuildGrid(string tableName)
        {
            var grid = new AxFormGridControl
            {
                Name = GridName,
                DataSource = tableName
            };

            // El nombre del grupo tiene que coincidir con un field group de la tabla, si no el
            // grid queda vacio aunque el form compile.
            grid.Controls.Add(new AxFormGroupControl
            {
                Name = OverviewGroupName,
                DataGroup = tableName == null ? null : OverviewGroupName,
                DataSource = tableName
            });

            return grid;
        }
    }
}
