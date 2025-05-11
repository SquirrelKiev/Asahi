namespace Asahi.Modules;

public static class ComponentExtensions
{
    public static void DisableAllComponents(this IComponentContainer components)
    {
        foreach (var component in components.Components)
        {
            if (component is ButtonBuilder button)
            {
                button.WithDisabled(true);
            }
            else if (component is SelectMenuBuilder selectMenu)
            {
                selectMenu.WithDisabled(true);
            }
            else if (component is IComponentContainer container)
            {
                DisableAllComponents(container);
                if (component is SectionBuilder { Accessory: ButtonBuilder buttonAccessory })
                {
                    buttonAccessory.WithDisabled(true);
                }
            }
        }
    }
}
