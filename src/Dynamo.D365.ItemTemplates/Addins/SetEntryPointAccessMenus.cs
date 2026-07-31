using System.ComponentModel.Composition;
using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.Framework.Tools.Extensibility;
using Microsoft.Dynamics.Framework.Tools.MetaModel.Automation.Security;

namespace Dynamo.D365.ItemTemplates.Addins
{
    /// <summary>
    /// Deja todos los entry points del privilegio en solo lectura.
    /// </summary>
    /// <remarks>
    /// Ver la nota de FillPatternMenu sobre DesignerMenuExportMetadata: el metadata va con ese
    /// atributo y nada mas, o MEF descarta el add-in en silencio.
    /// </remarks>
    [Export(typeof(IDesignerMenu))]
    [DesignerMenuExportMetadata(AutomationNodeType = typeof(ISecurityPrivilege))]
    public class SetAsReadMenu : SetEntryPointAccessMenuBase
    {
        public override string Name
        {
            get { return "DynamoSetEntryPointsAsRead"; }
        }

        public override string Caption
        {
            get { return "Set as read"; }
        }

        protected override string LevelName
        {
            get { return "Read"; }
        }

        protected override AccessGrant Grant
        {
            get { return AccessGrant.ConstructGrantRead(); }
        }
    }

    /// <summary>
    /// Deja todos los entry points del privilegio con acceso completo.
    ///
    /// Delete es el nivel mas alto y es acumulativo: incluye Read, Update, Create y Correct.
    /// </summary>
    [Export(typeof(IDesignerMenu))]
    [DesignerMenuExportMetadata(AutomationNodeType = typeof(ISecurityPrivilege))]
    public class SetAsDeleteMenu : SetEntryPointAccessMenuBase
    {
        public override string Name
        {
            get { return "DynamoSetEntryPointsAsDelete"; }
        }

        public override string Caption
        {
            get { return "Set as delete"; }
        }

        protected override string LevelName
        {
            get { return "Delete"; }
        }

        protected override AccessGrant Grant
        {
            get { return AccessGrant.ConstructGrantDelete(); }
        }
    }
}
