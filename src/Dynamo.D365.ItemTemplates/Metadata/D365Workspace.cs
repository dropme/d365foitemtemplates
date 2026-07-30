using System;
using System.Collections.Generic;
using System.Globalization;
using EnvDTE;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;
using Microsoft.Dynamics.Framework.Tools.MetaModel.Core;
using Microsoft.Dynamics.Framework.Tools.ProjectSystem;

namespace Dynamo.D365.ItemTemplates.Metadata
{
    /// <summary>
    /// Capa de acceso al modelo de D365. Crea elementos del AOT, los persiste en el modelo del
    /// proyecto activo y los agrega al .rnrproj.
    ///
    /// No depende de IWizard ni de nada del sistema de item templates: solo necesita el DTE
    /// para resolver el proyecto activo. Eso permite colgar las mismas recetas de un
    /// DesignerMenuBase (Add-in, punto de extension soportado) si el camino de los item
    /// templates deja de funcionar entre PUs.
    ///
    /// Uso:
    ///     workspace.Create(element);   // una vez por elemento
    ///     workspace.Commit();          // una sola vez, al final
    ///
    /// Por que Create y Commit estan separados
    /// ---------------------------------------
    /// Agregar los elementos al proyecto de a uno los deja todos colgando de la raiz. El
    /// metodo que respeta la opcion "Organize elements in project" -- la que crea las carpetas
    /// Tables, Classes, Forms... -- es VSProjectNode.AddModelElementsToProject, y toma la
    /// lista completa: necesita ver todos los tipos juntos para crear las carpetas una sola
    /// vez y repartir cada elemento en la suya.
    /// </summary>
    public sealed class D365Workspace
    {
        private readonly DTE _dte;
        private readonly List<MetadataReference> _pending = new List<MetadataReference>();

        private VSProjectNode _projectNode;
        private ModelInfo _modelInfo;
        private ModelSaveInfo _saveInfo;

        public D365Workspace(DTE dte)
        {
            if (dte == null)
                throw new ArgumentNullException("dte");

            _dte = dte;
        }

        /// <summary>
        /// Nodo del proyecto activo. Es el mismo cast que hace VSProjectUtil.GetActiveProjectNode.
        /// </summary>
        private VSProjectNode ProjectNode
        {
            get
            {
                if (_projectNode == null)
                {
                    Project project = GetActiveProject();

                    if (project == null)
                        throw new InvalidOperationException(
                            "No hay un proyecto activo. Selecciona un proyecto de Dynamics 365 antes de continuar.");

                    _projectNode = project.Object as VSProjectNode;

                    if (_projectNode == null)
                        throw new InvalidOperationException(string.Format(
                            CultureInfo.CurrentCulture,
                            "El proyecto activo '{0}' no es un proyecto de Dynamics 365.",
                            project.Name));
                }

                return _projectNode;
            }
        }

        /// <summary>
        /// Modelo del proyecto activo, resuelto por el propio sistema de proyectos a partir de
        /// la propiedad MSBuild "Model" del .rnrproj.
        /// </summary>
        public ModelInfo ModelInfo
        {
            get
            {
                // throwIfNotExists: el mensaje que tira el sistema de proyectos es mas preciso
                // que cualquiera que podamos armar aca.
                return _modelInfo ?? (_modelInfo = ProjectNode.GetProjectsModelInfo(true));
            }
        }

        private ModelSaveInfo SaveInfo
        {
            get { return _saveInfo ?? (_saveInfo = new ModelSaveInfo(ModelInfo)); }
        }

        /// <summary>
        /// Persiste el elemento en el modelo (escribe su XML bajo la carpeta del modelo) y lo
        /// deja encolado para que <see cref="Commit"/> lo agregue al proyecto.
        /// </summary>
        public T Create<T>(T element) where T : class, ISingleKeyedMetadata<string>
        {
            if (element == null)
                throw new ArgumentNullException("element");

            DesignMetaModelService.Instance.Create(element, SaveInfo);

            _pending.Add(new MetadataReference(
                element.GetPrimaryKey(),
                element.GetType(),
                ModelInfo));

            return element;
        }

        /// <summary>
        /// Agrega al proyecto todos los elementos creados hasta ahora, en una sola pasada.
        ///
        /// AddModelElementsToProject es el mismo camino que usa el sistema de proyectos para
        /// "Add existing element": respeta la opcion "Organize elements in project", crea las
        /// carpetas por tipo y valida que cada elemento pertenezca al modelo del proyecto.
        /// </summary>
        public void Commit()
        {
            if (_pending.Count == 0)
                return;

            ProjectNode.AddModelElementsToProject(_pending, openItemOnAdd: false);
            _pending.Clear();
        }

        private Project GetActiveProject()
        {
            var activeProjects = _dte.ActiveSolutionProjects as Array;

            if (activeProjects != null && activeProjects.Length > 0)
                return activeProjects.GetValue(0) as Project;

            // Fallback: el item seleccionado en Solution Explorer.
            if (_dte.SelectedItems != null && _dte.SelectedItems.Count > 0)
            {
                SelectedItem item = _dte.SelectedItems.Item(1);

                if (item != null)
                    return item.Project ?? (item.ProjectItem != null ? item.ProjectItem.ContainingProject : null);
            }

            return null;
        }
    }
}
