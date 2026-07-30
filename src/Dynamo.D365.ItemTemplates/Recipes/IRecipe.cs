using System.Collections.Generic;
using Dynamo.D365.ItemTemplates.Metadata;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Una receta crea un conjunto de elementos del AOT ya conectados entre si.
    ///
    /// Deliberadamente no sabe nada de IWizard: recibe un D365Workspace y los parametros ya
    /// resueltos. Eso permite ejecutar la misma receta desde un item template o desde un
    /// Add-in (DesignerMenuBase) sin tocar una linea.
    /// </summary>
    public interface IRecipe
    {
        /// <summary>Valor de $DynamoRecipe$ que selecciona esta receta.</summary>
        string Name { get; }

        /// <summary>
        /// Crea los elementos. <paramref name="parameters"/> son los CustomParameters del
        /// .vstemplate, ya fusionados con lo que haya elegido el usuario en el dialogo.
        /// </summary>
        void Run(D365Workspace workspace, string elementName, IDictionary<string, string> parameters);
    }
}
