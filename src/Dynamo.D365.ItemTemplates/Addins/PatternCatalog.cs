using System;
using System.Linq;
using Microsoft.Dynamics.AX.Metadata.Patterns;

namespace Dynamo.D365.ItemTemplates.Addins
{
    /// <summary>
    /// Acceso al catalogo de patrones de form.
    ///
    /// PatternFactory trae las definiciones estandar embebidas en el propio assembly (162, de
    /// las cuales 63 activas), asi que no depende de nada en disco. Construirla es caro, por
    /// eso se hace una sola vez por sesion.
    /// </summary>
    internal static class PatternCatalog
    {
        private static readonly Lazy<PatternFactory> Factory =
            new Lazy<PatternFactory>(() => new PatternFactory(true));

        /// <summary>
        /// Version activa del patron en esta instalacion, o null si no existe. Cada patron
        /// suele tener varias versiones y solo una activa; hardcodear el numero es un error
        /// porque cambia entre PUs.
        /// </summary>
        public static Pattern TryGetActive(string patternName)
        {
            if (string.IsNullOrWhiteSpace(patternName))
                return null;

            return Factory.Value
                .GetPatternsByName(patternName, false)
                .FirstOrDefault(p => p.Active);
        }
    }
}
