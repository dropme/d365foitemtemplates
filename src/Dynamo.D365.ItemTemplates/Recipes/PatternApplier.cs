using System;
using System.Globalization;
using System.Linq;
using Microsoft.Dynamics.AX.Metadata.MetaModel;
using Microsoft.Dynamics.AX.Metadata.Patterns;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Aplica patrones de form (Simple List, Custom and Quick Filters, ...).
    ///
    /// Setear Pattern y PatternVersion a mano no alcanza y encima obliga a hardcodear una
    /// version que cambia entre PUs. El catalogo de patrones sabe cual es la version activa de
    /// cada uno en esta instalacion, y ApplyPattern ademas valida que la estructura de
    /// controles se corresponda con el patron.
    ///
    /// Es lo que hace TRUDUtilsD365 en Kernel/AxHelper.cs.
    /// </summary>
    internal static class PatternApplier
    {
        // Construir la factory carga las definiciones de todos los patrones, asi que se hace
        // una sola vez por sesion.
        private static readonly Lazy<PatternFactory> Factory =
            new Lazy<PatternFactory>(() => new PatternFactory(true));

        /// <summary>
        /// Aplica la version activa del patron. Lanza si el patron no existe o si la
        /// estructura de controles no lo cumple: cualquiera de las dos cosas es un error
        /// nuestro, y es mejor verlo que quedarse con un form sin patron.
        /// </summary>
        public static void Apply(IPatternable element, string patternName)
        {
            if (element == null)
                throw new ArgumentNullException("element");

            Pattern pattern = Factory.Value
                .GetPatternsByName(patternName, false)
                .FirstOrDefault(p => p.Active);

            if (pattern == null)
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    "No se encontro una version activa del patron '{0}' en el catalogo.",
                    patternName));

            if (!element.ApplyPattern(pattern))
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    "El patron '{0} {1}' no se pudo aplicar: la estructura de controles no coincide.",
                    patternName,
                    pattern.Version));
        }
    }
}
