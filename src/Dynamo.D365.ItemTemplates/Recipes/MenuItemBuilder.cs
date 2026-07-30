using Microsoft.Dynamics.AX.Metadata.Core.MetaModel;
using Microsoft.Dynamics.AX.Metadata.MetaModel;

namespace Dynamo.D365.ItemTemplates.Recipes
{
    /// <summary>
    /// Display menu items. Lo comparten las recetas que publican un form y las que publican
    /// una clase: cambia el ObjectType, el resto es igual.
    /// </summary>
    internal static class MenuItemBuilder
    {
        public static AxMenuItemDisplay ForForm(string formName, string label)
        {
            return Build(formName, formName, MenuItemObjectType.Form, label);
        }

        public static AxMenuItemDisplay ForClass(string menuItemName, string className, string label)
        {
            return Build(menuItemName, className, MenuItemObjectType.Class, label);
        }

        private static AxMenuItemDisplay Build(
            string name, string objectName, MenuItemObjectType objectType, string label)
        {
            return new AxMenuItemDisplay
            {
                Name = name,
                Object = objectName,
                ObjectType = objectType,
                Label = label
            };
        }
    }
}
