using System;
using System.Drawing;
using System.Windows.Forms;

namespace Dynamo.D365.ItemTemplates.Ui
{
    /// <summary>
    /// Pide un valor de texto. Es lo minimo para las recetas que necesitan un dato que el
    /// dialogo de Add > New Item no puede dar: ahi el usuario solo escribe el nombre del
    /// elemento.
    ///
    /// Se muestra desde RunFinished, que corre en el hilo de UI de Visual Studio.
    /// </summary>
    internal static class InputDialog
    {
        /// <summary>
        /// Devuelve false solo si el usuario cancela; en ese caso quien llama deberia abortar
        /// la receta para no dejar elementos a medio crear.
        ///
        /// Aceptar con el campo en blanco devuelve true y una cadena vacia: queda a criterio
        /// de la receta si ese dato era opcional.
        /// </summary>
        public static bool TryPrompt(string title, string prompt, string defaultValue, out string value)
        {
            value = null;

            using (var dialog = new Form())
            using (var label = new Label())
            using (var textBox = new TextBox())
            using (var accept = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = title;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(420, 120);

                // Sin esto el dialogo puede quedar detras de la ventana de Visual Studio.
                dialog.TopMost = true;

                label.SetBounds(12, 15, 396, 32);
                label.Text = prompt;
                label.AutoSize = false;

                textBox.SetBounds(15, 52, 390, 23);
                textBox.Text = defaultValue ?? string.Empty;
                textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

                accept.SetBounds(240, 85, 80, 25);
                accept.Text = "Aceptar";
                accept.DialogResult = DialogResult.OK;

                cancel.SetBounds(325, 85, 80, 25);
                cancel.Text = "Cancelar";
                cancel.DialogResult = DialogResult.Cancel;

                dialog.Controls.AddRange(new Control[] { label, textBox, accept, cancel });
                dialog.AcceptButton = accept;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog() != DialogResult.OK)
                    return false;

                value = textBox.Text == null ? string.Empty : textBox.Text.Trim();

                return true;
            }
        }

        /// <summary>
        /// Igual que TryPrompt, pero si el usuario cancela aborta la receta. Devuelve cadena
        /// vacia si acepto sin completar nada.
        /// </summary>
        public static string PromptOrCancel(string title, string prompt, string defaultValue = null)
        {
            string value;

            if (!TryPrompt(title, prompt, defaultValue, out value))
                throw new OperationCanceledException();

            return value;
        }
    }
}
