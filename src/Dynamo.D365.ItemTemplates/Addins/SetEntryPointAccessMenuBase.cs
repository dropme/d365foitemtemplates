using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;
using Microsoft.Dynamics.Framework.Tools.Extensibility;
using Microsoft.Dynamics.Framework.Tools.MetaModel.Automation.Security;
using Microsoft.Dynamics.Framework.Tools.MetaModel.Core;

namespace Dynamo.D365.ItemTemplates.Addins
{
    /// <summary>
    /// Pone el mismo access level en todos los entry points de un privilegio.
    ///
    /// Hacerlo a mano es tedioso y facil de dejar a medias: cada entry point tiene su propia
    /// propiedad y un privilegio suele tener varios.
    ///
    /// Los niveles son acumulativos (Read &lt; Update &lt; Create &lt; Correct &lt; Delete), y
    /// quien define que incluye cada uno es AccessGrant.ConstructGrant*, no nosotros.
    ///
    /// Trabaja sobre el metamodelo: si el privilegio esta abierto en el diseñador hay que
    /// recargarlo para ver los cambios.
    /// </summary>
    public abstract class SetEntryPointAccessMenuBase : DesignerMenuBase
    {
        /// <summary>Grant a aplicar, ya armado por el metamodelo.</summary>
        protected abstract AccessGrant Grant { get; }

        /// <summary>Nombre del nivel, solo para los mensajes.</summary>
        protected abstract string LevelName { get; }

        public override void OnClick(AddinDesignerEventArgs eventArgs)
        {
            try
            {
                Run(eventArgs);
            }
            catch (Exception ex)
            {
                // Sin esto, una excepcion en un add-in no se ve por ningun lado: parece que el
                // comando no hizo nada.
                MessageBox.Show(ex.ToString(), Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string Title
        {
            get { return "DYNAMO - " + Caption; }
        }

        private void Run(AddinDesignerEventArgs eventArgs)
        {
            var selected = eventArgs.SelectedElement as ISecurityPrivilege;

            if (selected == null)
            {
                MessageBox.Show(
                    "Selecciona un privilegio.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            // Con cambios sin guardar se leeria el privilegio viejo del disco, y al guardar se
            // perderia lo que haya en el diseñador.
            if (!DesignerDocument.EnsureSaved(Title))
                return;

            string privilegeName = selected.Name;

            var provider = DesignMetaModelService.Instance.CurrentMetadataProvider;
            AxSecurityPrivilege privilege = provider.SecurityPrivileges.Read(privilegeName);

            if (privilege == null)
            {
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, "No se pudo leer el privilegio '{0}'.", privilegeName),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            if (privilege.EntryPoints.Count == 0)
            {
                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "El privilegio '{0}' no tiene entry points.", privilegeName),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            AccessGrant grant = Grant;
            var changed = new StringBuilder();
            int count = 0;

            foreach (AxSecurityEntryPointReference entryPoint in privilege.EntryPoints)
            {
                entryPoint.Grant = grant;
                count++;

                changed.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "   {0} ({1}){2}", entryPoint.ObjectName, entryPoint.ObjectType, Environment.NewLine);
            }

            ModelInfo modelInfo = provider.SecurityPrivileges.GetModelInfo(privilegeName).FirstOrDefault();

            if (modelInfo == null)
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    "No se pudo determinar el modelo del privilegio '{0}'.", privilegeName));

            provider.SecurityPrivileges.Update(privilege, new ModelSaveInfo(modelInfo));

            MessageBox.Show(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Privilegio: {0}{1}Access level: {2}{1}{1}Entry points actualizados ({3}):{1}{4}{1}" +
                    "Cerra y volve a abrir el privilegio para ver los cambios.",
                    privilegeName, Environment.NewLine, LevelName, count, changed),
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
