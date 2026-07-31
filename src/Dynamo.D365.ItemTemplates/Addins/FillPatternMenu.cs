using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Dynamo.D365.ItemTemplates.Recipes;
using Microsoft.Dynamics.AX.Metadata.MetaModel;
using Microsoft.Dynamics.AX.Metadata.Patterns;
using Microsoft.Dynamics.Framework.Tools.Extensibility;
using Microsoft.Dynamics.Framework.Tools.MetaModel.Automation.Forms;
using Microsoft.Dynamics.Framework.Tools.MetaModel.Core;

namespace Dynamo.D365.ItemTemplates.Addins
{
    /// <summary>
    /// Add-in "Fill Pattern": completa los controles obligatorios del patron que tenga puesto
    /// el form. Aparece en el menu contextual del designer, en Addins.
    ///
    /// Los add-ins se descubren por MEF: AddinFactory arma un DirectoryCatalog sobre las
    /// carpetas que devuelve AddinsEnvironmentHelper.AddinDirectories(), asi que este assembly
    /// tiene que estar en una de ellas (ver install\Install-ItemTemplates.ps1, que registra la
    /// carpeta de la extension como AddInPath).
    ///
    /// Trabaja sobre el metamodelo, no sobre el designer abierto: lee el AxForm, lo completa y
    /// lo guarda. Por eso hay que recargar el form para ver los controles nuevos.
    /// </summary>
    /// <remarks>
    /// El metadata va con DesignerMenuExportMetadata y nada mas. Es un [MetadataAttribute] que
    /// aporta las dos propiedades que pide IDesignerMenuMetadata (AutomationNodeType y
    /// CanSelectMultiple, que por defecto es false).
    ///
    /// Agregarle ademas [ExportMetadata("AutomationNodeType", ...)] duplica las claves y MEF
    /// descarta el export: el menu no aparece y no hay ningun error en ningun lado.
    /// </remarks>
    [Export(typeof(IDesignerMenu))]
    [DesignerMenuExportMetadata(AutomationNodeType = typeof(IForm))]
    public class FillPatternMenu : DesignerMenuBase
    {
        private const string DialogTitle = "DYNAMO - Fill Pattern";

        public override string Name
        {
            get { return "DynamoFillPattern"; }
        }

        public override string Caption
        {
            get { return "Fill Pattern"; }
        }

        public override void OnClick(AddinDesignerEventArgs eventArgs)
        {
            try
            {
                Run(eventArgs);
            }
            catch (Exception ex)
            {
                // Sin este catch, una excepcion en un add-in se pierde: Visual Studio no
                // muestra nada y parece que el comando no hizo nada.
                MessageBox.Show(ex.ToString(), DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run(AddinDesignerEventArgs eventArgs)
        {
            var selected = eventArgs.SelectedElement as IForm;

            if (selected == null)
            {
                MessageBox.Show(
                    "Selecciona un formulario.",
                    DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            // Antes que nada: con cambios sin guardar se leeria el form viejo del disco.
            if (!DesignerDocument.EnsureSaved(DialogTitle))
                return;

            string formName = selected.Name;
            string patternName = selected.FormDesign == null ? null : selected.FormDesign.Pattern;

            if (string.IsNullOrWhiteSpace(patternName))
            {
                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "El formulario '{0}' no tiene un patron asignado.{1}{1}" +
                        "Asignalo en las propiedades del Design y volve a ejecutar Fill Pattern.",
                        formName, Environment.NewLine),
                    DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            Pattern pattern = PatternCatalog.TryGetActive(patternName);

            if (pattern == null)
            {
                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "No se encontro una version activa del patron '{0}' en el catalogo.",
                        patternName),
                    DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;
            }

            var provider = DesignMetaModelService.Instance.CurrentMetadataProvider;
            AxForm form = provider.Forms.Read(formName);

            if (form == null)
            {
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, "No se pudo leer el formulario '{0}'.", formName),
                    DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            if (form.Design == null)
                form.Design = new AxFormDesign();

            PatternFiller.Result result = PatternFiller.Fill(form.Design, pattern);

            if (result.Created.Count == 0)
            {
                MessageBox.Show(
                    BuildSummary(formName, pattern, result, saved: false),
                    DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            // Deja el patron marcado y escribe las propiedades que exige. Si la estructura
            // quedo incompleta (por ejemplo por placeholders que no se pueden crear solos),
            // ApplyPattern falla y conviene que se vea.
            PatternApplier.Apply(form.Design, pattern);

            ModelInfo modelInfo = provider.Forms.GetModelInfo(formName).FirstOrDefault();

            if (modelInfo == null)
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    "No se pudo determinar el modelo del formulario '{0}'.", formName));

            provider.Forms.Update(form, new ModelSaveInfo(modelInfo));

            MessageBox.Show(
                BuildSummary(formName, pattern, result, saved: true),
                DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string BuildSummary(string formName, Pattern pattern, PatternFiller.Result result, bool saved)
        {
            var summary = new StringBuilder();

            summary.AppendFormat(CultureInfo.CurrentCulture,
                "Formulario: {0}{2}Patron: {1} {3}{2}{2}",
                formName, pattern.FriendlyName, Environment.NewLine, pattern.Version);

            if (result.Created.Count == 0)
            {
                summary.AppendLine("No faltaba ningun control obligatorio.");
            }
            else
            {
                summary.AppendFormat(CultureInfo.CurrentCulture,
                    "Controles creados ({0}):{1}", result.Created.Count, Environment.NewLine);

                foreach (string created in result.Created)
                    summary.AppendFormat(CultureInfo.CurrentCulture, "   {0}{1}", created, Environment.NewLine);
            }

            if (result.Skipped.Count > 0)
            {
                summary.AppendLine();
                summary.AppendFormat(CultureInfo.CurrentCulture,
                    "Sin crear, dependen de los datos del form ({0}):{1}",
                    result.Skipped.Count, Environment.NewLine);

                foreach (string skipped in result.Skipped)
                    summary.AppendFormat(CultureInfo.CurrentCulture, "   {0}{1}", skipped, Environment.NewLine);
            }

            if (saved)
            {
                summary.AppendLine();
                summary.AppendLine("Cerra y volve a abrir el formulario para ver los cambios.");
            }

            return summary.ToString();
        }
    }
}
