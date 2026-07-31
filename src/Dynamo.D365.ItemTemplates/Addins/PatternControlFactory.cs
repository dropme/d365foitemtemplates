using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Dynamics.AX.Metadata.MetaModel;
using Microsoft.Dynamics.AX.Metadata.Patterns;

namespace Dynamo.D365.ItemTemplates.Addins
{
    /// <summary>
    /// Traduce un nodo de patron al control del metamodelo que le corresponde.
    ///
    /// El catalogo describe cada nodo con un Type ("ActionPane", "Grid", "Group",
    /// "QuickFilterControl", "$Field"...). Hay tres familias:
    ///
    ///   - Controles normales: el tipo del metamodelo se deduce por convencion,
    ///     "Grid" -> AxFormGridControl. Cubre la mayoria.
    ///   - Control extensions: no existe una clase por cada uno, se representan con un
    ///     AxFormControl que lleva un AxFormControlExtension con el nombre del tipo.
    ///   - Placeholders ($Field, $Button, $Container...): no son un control concreto sino una
    ///     categoria; dependen de que campo o tabla se quiera poner, asi que no se crean.
    /// </summary>
    internal static class PatternControlFactory
    {
        private static readonly Assembly MetaModelAssembly = typeof(AxFormControl).Assembly;
        private const string MetaModelNamespace = "Microsoft.Dynamics.AX.Metadata.MetaModel.";

        /// <summary>
        /// Devuelve null si el nodo no se puede materializar solo (placeholders y tipos
        /// desconocidos). Quien llama deberia saltearlo, no fallar.
        /// </summary>
        public static AxFormControl TryCreate(PatternNode node, string name)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            string type = node.Type;

            // Placeholder del patron: representa "un campo cualquiera", no un control concreto.
            if (string.IsNullOrEmpty(type) || type.StartsWith("$", StringComparison.Ordinal))
                return null;

            Type clrType = ResolveControlType(type);

            if (clrType != null)
            {
                var control = (AxFormControl)Activator.CreateInstance(clrType);
                control.Name = name;

                return control;
            }

            // Sin clase propia pero termina en Control: es una extension de control.
            if (type.EndsWith("Control", StringComparison.Ordinal))
            {
                return new AxFormControl
                {
                    Name = name,
                    FormControlExtension = new AxFormControlExtension { Name = type }
                };
            }

            return null;
        }

        /// <summary>
        /// "Grid" -> AxFormGridControl, "ActionPane" -> AxFormActionPaneControl. Algunos ya
        /// traen el sufijo Control en el nombre del patron, de ahi el segundo intento.
        /// </summary>
        private static Type ResolveControlType(string patternType)
        {
            foreach (string candidate in new[]
            {
                MetaModelNamespace + "AxForm" + patternType + "Control",
                MetaModelNamespace + "AxForm" + patternType
            })
            {
                Type type = MetaModelAssembly.GetType(candidate, false);

                if (type != null && typeof(AxFormControl).IsAssignableFrom(type) && !type.IsAbstract)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// Nombre para el control nuevo. El patron da un rol semantico en Part
        /// ("SimpleListGrid", "ApplicationBar"); si no lo tiene se usa el tipo. Se desambigua
        /// contra los nombres ya usados porque el nombre es la clave de la coleccion.
        /// </summary>
        public static string SuggestName(PatternNode node, ICollection<string> taken)
        {
            string baseName = !string.IsNullOrWhiteSpace(node.Part)
                ? node.Part
                : (node.Type ?? "Control").TrimStart('$');

            if (!taken.Contains(baseName))
                return baseName;

            for (int i = 2; ; i++)
            {
                string candidate = baseName + i.ToString();

                if (!taken.Contains(candidate))
                    return candidate;
            }
        }
    }
}
