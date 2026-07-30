using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Arma un AxClass a partir de codigo X++.
    ///
    /// Una AxClass se guarda partida en dos: SourceCode.Declaration (los atributos y la firma
    /// de la clase, con el cuerpo vacio) y un AxMethod por metodo, cada uno con su Source
    /// completo. Es la misma division que se ve en el XML del elemento.
    ///
    /// Microsoft hace esto con BuildHelper.ParseSourceCodeString, que parsea el X++ y lo
    /// reparte solo, pero vive en Tools.BuildTasks. Como las recetas conocen sus plantillas de
    /// antemano, alcanza con pasar las partes ya separadas y evitamos esa dependencia.
    /// </summary>
    internal sealed class ClassBuilder
    {
        private readonly AxClass _axClass;

        private ClassBuilder(string name, string declaration)
        {
            _axClass = new AxClass
            {
                Name = name,
                SourceCode = new AxPropertyCollection { Declaration = declaration }
            };
        }

        /// <summary>
        /// <paramref name="declaration"/> es la firma de la clase con el cuerpo vacio, por
        /// ejemplo "public class Foo extends Bar\n{\n}".
        /// </summary>
        public static ClassBuilder Declare(string name, string declaration)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name");

            return new ClassBuilder(name, declaration);
        }

        /// <summary>
        /// <paramref name="source"/> es el metodo completo, incluida su firma.
        /// </summary>
        public ClassBuilder WithMethod(string name, string source)
        {
            _axClass.Methods.Add(new AxMethod
            {
                Name = name,
                Source = source
            });

            return this;
        }

        public AxClass Build()
        {
            return _axClass;
        }

        /// <summary>
        /// Formatea una plantilla con el nombre base de la receta. Se usa InvariantCulture
        /// porque el resultado es codigo, no texto para el usuario.
        /// </summary>
        public static string Format(string template, string baseName)
        {
            return string.Format(CultureInfo.InvariantCulture, template, baseName);
        }

        /// <summary>
        /// Quita el sufijo de rol si el usuario ya lo escribio: para "MiProcesoController" el
        /// nombre base es "MiProceso", asi las tres clases quedan parejas.
        /// </summary>
        public static string StripRoleSuffix(string name, IEnumerable<string> suffixes)
        {
            foreach (string suffix in suffixes)
            {
                if (name.Length > suffix.Length &&
                    name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return name.Substring(0, name.Length - suffix.Length);
                }
            }

            return name;
        }
    }
}
