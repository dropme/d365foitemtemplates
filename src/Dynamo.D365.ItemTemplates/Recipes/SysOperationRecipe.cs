using System.Collections.Generic;
using System.Globalization;
using Dynamo.D365.ItemTemplates.Metadata;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Clases del SysOperation framework: controller + service, y opcionalmente un contract.
    ///
    /// El usuario escribe el nombre base (por ejemplo "DynTorqueInboundLogCleanup") y se crean
    /// &lt;base&gt;Controller, &lt;base&gt;Service y &lt;base&gt;Contract. Si escribe el nombre
    /// ya con uno de esos sufijos, se lo quita para que las tres queden parejas.
    ///
    /// Que se cree cada pieza opcional lo deciden $DynamoIncludeContract$,
    /// $DynamoIncludeMenuItem$ y $DynamoIncludePrivileges$, asi todos los templates de
    /// SysOperation comparten esta receta y difieren solo en parametros del .vstemplate.
    ///
    /// Las llaves van duplicadas en las plantillas porque pasan por string.Format.
    /// </summary>
    public sealed class SysOperationRecipe : IRecipe
    {
        private const string ControllerSuffix = "Controller";
        private const string ServiceSuffix = "Service";
        private const string ContractSuffix = "Contract";

        private static readonly string[] RoleSuffixes = { ControllerSuffix, ServiceSuffix, ContractSuffix };

        public string Name
        {
            get { return "SysOperation"; }
        }

        public void Run(D365Workspace workspace, string elementName, IDictionary<string, string> parameters)
        {
            string baseName = ClassBuilder.StripRoleSuffix(elementName, RoleSuffixes);
            string caption = parameters.Value("$DynamoLabel$", baseName);

            bool includeContract = ParseBool(parameters.Value("$DynamoIncludeContract$"), fallback: false);
            bool includeMenuItem = ParseBool(parameters.Value("$DynamoIncludeMenuItem$"), fallback: false);
            bool includePrivileges = ParseBool(parameters.Value("$DynamoIncludePrivileges$"), fallback: false);

            // El contract va primero: el service lo referencia en la firma de process().
            if (includeContract)
                workspace.Create(BuildContract(baseName));

            workspace.Create(BuildService(baseName, includeContract));
            workspace.Create(BuildController(baseName, caption));

            if (!includeMenuItem)
                return;

            // El menu item apunta al controller, que es el que tiene el main(Args).
            string menuItemName = parameters.Value("$DynamoMenuItemName$", baseName);

            workspace.Create(MenuItemBuilder.ForClass(menuItemName, baseName + ControllerSuffix, caption));

            // Los privilegios solo tienen sentido con un menu item al que referenciar.
            if (!includePrivileges)
                return;

            string levels = parameters.Value("$DynamoPrivilegeLevels$", PrivilegeBuilder.DefaultLevels);

            foreach (AxSecurityPrivilege privilege in PrivilegeBuilder.BuildAll(baseName, menuItemName, levels, caption))
                workspace.Create(privilege);
        }

        // ------------------------------------------------------------------ controller ----

        private const string ControllerDeclaration =
@"public class {0}Controller extends SysOperationServiceController
{{
}}";

        private const string ControllerNew =
@"    protected void new()
    {{
        super(classStr({0}Service), methodStr({0}Service, process), SysOperationExecutionMode::Synchronous);
    }}";

        private const string ControllerDefaultCaption =
@"    public ClassDescription defaultCaption()
    {{
        return ""{1}"";
    }}";

        private const string ControllerConstruct =
@"    public static {0}Controller construct(SysOperationExecutionMode _executionMode = SysOperationExecutionMode::Synchronous)
    {{
        {0}Controller controller = new {0}Controller();

        controller.parmExecutionMode(_executionMode);

        return controller;
    }}";

        private const string ControllerMain =
@"    public static void main(Args _args)
    {{
        {0}Controller controller = {0}Controller::construct();

        controller.parmArgs(_args);
        controller.startOperation();
    }}";

        internal static AxClass BuildController(string baseName, string caption)
        {
            string defaultCaption = string.Format(
                CultureInfo.InvariantCulture, ControllerDefaultCaption, baseName, EscapeXppString(caption));

            return ClassBuilder
                .Declare(baseName + ControllerSuffix, ClassBuilder.Format(ControllerDeclaration, baseName))
                .WithMethod("new", ClassBuilder.Format(ControllerNew, baseName))
                .WithMethod("defaultCaption", defaultCaption)
                .WithMethod("construct", ClassBuilder.Format(ControllerConstruct, baseName))
                .WithMethod("main", ClassBuilder.Format(ControllerMain, baseName))
                .Build();
        }

        // --------------------------------------------------------------------- service ----

        private const string ServiceDeclaration =
@"public class {0}Service extends SysOperationServiceBase
{{
}}";

        private const string ServiceProcessWithContract =
@"    public void process({0}Contract _contract)
    {{

    }}";

        // Sin {0}, asi que no pasa por Format y las llaves van simples.
        private const string ServiceProcessWithoutContract =
@"    public void process()
    {

    }";

        internal static AxClass BuildService(string baseName, bool includeContract)
        {
            // Sin contract, process() no lleva parametro: la firma tiene que coincidir con lo
            // que el controller registra via methodStr().
            string process = includeContract
                ? ClassBuilder.Format(ServiceProcessWithContract, baseName)
                : ServiceProcessWithoutContract;

            return ClassBuilder
                .Declare(baseName + ServiceSuffix, ClassBuilder.Format(ServiceDeclaration, baseName))
                .WithMethod("process", process)
                .Build();
        }

        // -------------------------------------------------------------------- contract ----

        private const string ContractDeclaration =
@"[DataContractAttribute]
public class {0}Contract
{{
}}";

        internal static AxClass BuildContract(string baseName)
        {
            // Sin parm* todavia: los agrega quien use el template, segun lo que necesite pasar.
            return ClassBuilder
                .Declare(baseName + ContractSuffix, ClassBuilder.Format(ContractDeclaration, baseName))
                .Build();
        }

        // ------------------------------------------------------------------------ varios ----

        private static string EscapeXppString(string value)
        {
            return value == null ? string.Empty : value.Replace("\"", "\\\"");
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }
    }
}
