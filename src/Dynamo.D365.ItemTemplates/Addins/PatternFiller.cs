using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Dynamics.AX.Metadata.MetaModel;
using Microsoft.Dynamics.AX.Metadata.Patterns;

namespace Dynamo.D365.ItemTemplates.Addins
{
    /// <summary>
    /// Crea los controles que un patron declara obligatorios y todavia no estan en el form.
    ///
    /// El catalogo describe cada patron como un arbol de nodos, cada uno con su tipo de
    /// control, si es obligatorio (RequireOne) y que subpatrones lo completan. Con eso alcanza
    /// para armar el esqueleto; las propiedades las escribe despues ApplyPattern.
    ///
    /// Lo que no se crea, porque no depende del patron sino del caso de uso:
    ///   - nodos opcionales
    ///   - placeholders ($Field, $Button, ...): dependen de que campo se quiera mostrar
    ///   - data sources y DataGroup: el patron no sabe sobre que tabla va el form
    /// </summary>
    internal static class PatternFiller
    {
        /// <summary>
        /// Resultado del relleno, para poder contarle al usuario que paso.
        /// </summary>
        public sealed class Result
        {
            public List<string> Created { get; private set; }
            public List<string> Skipped { get; private set; }

            public Result()
            {
                Created = new List<string>();
                Skipped = new List<string>();
            }
        }

        public static Result Fill(AxFormDesign design, Pattern pattern)
        {
            if (design == null)
                throw new ArgumentNullException("design");

            if (pattern == null)
                throw new ArgumentNullException("pattern");

            var result = new Result();

            // El nodo raiz del patron describe al propio design, asi que se baja un nivel.
            FillChildren(design, pattern.Root, result);

            return result;
        }

        private static void FillChildren(IFormControlCollection container, PatternNode node, Result result)
        {
            // Un patron puede pedir varios nodos del mismo tipo (por ejemplo un Tab con un
            // TabPage para la grilla y otro para el detalle). Como el match va por tipo, sin
            // llevar cuenta de lo ya emparejado el primer control satisfaria a todos esos
            // nodos y solo se crearia uno.
            var matched = new HashSet<AxFormControl>();

            foreach (PatternNode child in node.SubNodes)
            {
                // Solo los obligatorios: los opcionales son decision de quien diseña el form.
                if (!child.RequireOne)
                    continue;

                AxFormControl existing = FindMatching(container, child, matched);

                if (existing == null)
                {
                    existing = CreateControl(container, child, result);

                    if (existing == null)
                        continue;
                }

                matched.Add(existing);

                // Un nodo puede delegar su contenido en un subpatron (por ejemplo el grupo de
                // filtros del Simple List, que se completa con CustomAndQuickFilters).
                FillSubPatterns(existing, child, result);

                var childContainer = existing as IFormControlCollection;

                if (childContainer != null)
                    FillChildren(childContainer, child, result);
            }
        }

        private static AxFormControl CreateControl(IFormControlCollection container, PatternNode node, Result result)
        {
            var taken = new HashSet<string>(
                container.Controls.Select(c => c.Name).Where(n => n != null),
                StringComparer.OrdinalIgnoreCase);

            string name = PatternControlFactory.SuggestName(node, taken);
            AxFormControl control = PatternControlFactory.TryCreate(node, name);

            if (control == null)
            {
                result.Skipped.Add(string.Format(
                    CultureInfo.CurrentCulture, "{0} ({1})", node.Type, Describe(node)));

                return null;
            }

            container.AddControl(control);
            result.Created.Add(string.Format(
                CultureInfo.CurrentCulture, "{0} ({1})", name, node.Type));

            return control;
        }

        /// <summary>
        /// Si el nodo declara subpatrones, se aplica el primero que exista en el catalogo: son
        /// alternativas, no una lista de cosas a poner todas.
        /// </summary>
        private static void FillSubPatterns(AxFormControl control, PatternNode node, Result result)
        {
            var patternable = control as IPatternable;

            if (patternable == null)
                return;

            foreach (string subPatternName in node.SubPatterns)
            {
                Pattern subPattern = PatternCatalog.TryGetActive(subPatternName);

                if (subPattern == null)
                    continue;

                var subContainer = control as IFormControlCollection;

                if (subContainer != null)
                    FillChildren(subContainer, subPattern.Root, result);

                return;
            }
        }

        /// <summary>
        /// Un control ya cumple el nodo si el propio catalogo lo reconoce como tal. Comparar
        /// por tipo a mano seria fragil: MatchesIdentity contempla las reglas del patron.
        ///
        /// <paramref name="alreadyMatched"/> son los controles que ya cumplen otro nodo de
        /// este mismo nivel; cada nodo tiene que quedarse con uno distinto.
        /// </summary>
        private static AxFormControl FindMatching(
            IFormControlCollection container, PatternNode node, HashSet<AxFormControl> alreadyMatched)
        {
            return container.Controls.FirstOrDefault(c =>
            {
                if (alreadyMatched.Contains(c))
                    return false;

                var patternable = c as IPatternable;

                return patternable != null && node.Identity.MatchesIdentity(patternable);
            });
        }

        private static string Describe(PatternNode node)
        {
            return string.IsNullOrWhiteSpace(node.Part) ? "sin part" : node.Part;
        }
    }
}
