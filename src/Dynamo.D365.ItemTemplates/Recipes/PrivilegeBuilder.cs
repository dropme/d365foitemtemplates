using System;
using System.Collections.Generic;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Privilegios de seguridad con un display menu item como entry point.
    ///
    /// Lo comparten todas las recetas que terminan en un menu item: el privilegio no depende
    /// de la tabla ni del form, solo del menu item al que da acceso.
    /// </summary>
    internal static class PrivilegeBuilder
    {
        public const string DefaultLevels = "View,Maintain";

        /// <summary>
        /// Un privilegio por nivel, nombrados &lt;baseName&gt;&lt;Nivel&gt;.
        /// </summary>
        public static IEnumerable<AxSecurityPrivilege> BuildAll(
            string baseName, string menuItemName, string levels, string label)
        {
            foreach (string level in ParseLevels(levels))
                yield return Build(baseName, menuItemName, level, label);
        }

        public static AxSecurityPrivilege Build(string baseName, string menuItemName, string level, string label)
        {
            var privilege = new AxSecurityPrivilege
            {
                Name = string.Concat(baseName, level),
                Label = label,
                Enabled = NoYes.Yes
            };

            privilege.EntryPoints.Add(new AxSecurityEntryPointReference
            {
                Name = menuItemName,
                ObjectName = menuItemName,
                ObjectType = EntryPointType.MenuItemDisplay,
                Grant = GrantFor(level)
            });

            return privilege;
        }

        /// <summary>
        /// AccessGrant no es un nivel unico sino un permiso por operacion, y los niveles son
        /// acumulativos: Read &lt; Update &lt; Create &lt; Correct &lt; Delete.
        ///
        /// Que incluye cada uno lo define el metamodelo con ConstructGrant*, no nosotros:
        /// armarlo a mano es como se cuelan diferencias (Correct, por ejemplo) entre esto y el
        /// add-in "Set as delete", que deberia dar exactamente el mismo resultado.
        /// </summary>
        private static AccessGrant GrantFor(string level)
        {
            return string.Equals(level, "View", StringComparison.OrdinalIgnoreCase)
                ? AccessGrant.ConstructGrantRead()
                : AccessGrant.ConstructGrantDelete();
        }

        private static IEnumerable<string> ParseLevels(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                raw = DefaultLevels;

            foreach (string level in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = level.Trim();

                if (trimmed.Length > 0)
                    yield return trimmed;
            }
        }
    }
}
