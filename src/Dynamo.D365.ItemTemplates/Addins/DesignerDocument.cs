using System;
using System.Globalization;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.Dynamics.Framework.Tools.MetaModel.Core;

namespace Dynamo.D365.ItemTemplates.Addins
{
    /// <summary>
    /// Los add-ins leen y escriben el elemento a traves del metamodelo, es decir el archivo
    /// guardado en el modelo, no lo que se ve en el diseñador.
    ///
    /// Con cambios sin guardar eso trae dos problemas, y ninguno de los dos avisa solo:
    ///
    ///   - se trabaja sobre datos viejos. Borrar los controles de un form y ejecutar Fill
    ///     Pattern sin guardar hace que el add-in vea los controles que ya no estan, no cree
    ///     nada, y ApplyPattern falle porque esa estructura no es la del patron.
    ///   - al guardar, el add-in pisa los cambios que habia en el diseñador.
    ///
    /// Por eso conviene guardar antes de tocar nada.
    /// </summary>
    internal static class DesignerDocument
    {
        /// <summary>
        /// Devuelve false si el usuario prefiere cancelar; en ese caso el add-in no deberia
        /// hacer nada.
        /// </summary>
        public static bool EnsureSaved(string title)
        {
            Document document = TryGetActiveDocument();

            if (document == null || document.Saved)
                return true;

            DialogResult answer = MessageBox.Show(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "'{0}' tiene cambios sin guardar.{1}{1}" +
                    "Este add-in trabaja sobre el elemento guardado, asi que con cambios pendientes " +
                    "el resultado seria incorrecto y ademas se perderian.{1}{1}" +
                    "Guardar y continuar?",
                    document.Name, Environment.NewLine),
                title,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.OK)
                return false;

            document.Save();

            return true;
        }

        private static Document TryGetActiveDocument()
        {
            try
            {
                return CoreUtility.GetCurrentActiveDocument();
            }
            catch (Exception)
            {
                // Sin documento activo, o el shell todavia no esta listo: no es motivo para
                // frenar el add-in, solo se pierde la advertencia.
                return null;
            }
        }
    }
}
