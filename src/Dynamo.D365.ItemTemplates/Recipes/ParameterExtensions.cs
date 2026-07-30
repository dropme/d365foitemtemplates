using System.Collections.Generic;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Lectura tolerante del replacementsDictionary del template: los CustomParameters que el
    /// .vstemplate no declara simplemente no estan en el diccionario.
    /// </summary>
    internal static class ParameterExtensions
    {
        public static string Value(this IDictionary<string, string> parameters, string key, string fallback = null)
        {
            string value;

            if (parameters != null && parameters.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                return value;

            return fallback;
        }
    }
}
