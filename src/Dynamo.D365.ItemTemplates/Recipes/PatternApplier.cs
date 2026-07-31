using System;
using System.Globalization;
using Dynamo.D365.ItemTemplates.Addins;
using Microsoft.Dynamics.AX.Metadata.MetaModel;
using Microsoft.Dynamics.AX.Metadata.Patterns;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Aplica patrones de form (Simple List, Custom and Quick Filters, ...).
    ///
    /// Setear Pattern y PatternVersion a mano no alcanza y encima obliga a hardcodear una
    /// version que cambia entre PUs. El catalogo sabe cual es la version activa de cada patron
    /// en esta instalacion, y ApplyPattern ademas escribe las propiedades que el patron exige
    /// (Style, ColumnsMode, WidthMode...) y valida que la estructura de controles se
    /// corresponda con el.
    ///
    /// Es lo que hace TRUDUtilsD365 en Kernel/AxHelper.cs.
    /// </summary>
    internal static class PatternApplier
    {
        /// <summary>
        /// Aplica la version activa del patron. Lanza si el patron no existe o si la
        /// estructura de controles no lo cumple: cualquiera de las dos cosas es un error
        /// nuestro, y es mejor verlo que quedarse con un form sin patron.
        /// </summary>
        public static void Apply(IPatternable element, string patternName)
        {
            if (element == null)
                throw new ArgumentNullException("element");

            Pattern pattern = PatternCatalog.TryGetActive(patternName);

            if (pattern == null)
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    "No se encontro una version activa del patron '{0}' en el catalogo.",
                    patternName));

            Apply(element, pattern);
        }

        public static void Apply(IPatternable element, Pattern pattern)
        {
            if (!element.ApplyPattern(pattern))
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    "El patron '{0} {1}' no se pudo aplicar: la estructura de controles no coincide.",
                    pattern.Name,
                    pattern.Version));
        }
    }
}
