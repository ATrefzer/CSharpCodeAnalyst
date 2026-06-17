using System.Windows.Controls;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Features.Graph;

namespace CSharpCodeAnalyst.Features.WebGraph;

/// <summary>
///     Builds WPF context menus for the web graph view, reusing the same command objects
///     the MSAGL view uses. Mirrors the menu-building logic in GraphViewer (node menu with
///     separators, edge menu with sub-menu groups, global menu over the selection) so both
///     views behave identically. The MSAGL menu code is left untouched (it is transitional).
/// </summary>
internal static class WebContextMenuFactory
{
    /// <summary>Node menu: commands for the right-clicked element, with separators.</summary>
    public static ContextMenu BuildForNode(IReadOnlyList<ICodeElementContextCommand> commands, CodeElement element)
    {
        var menu = new ContextMenu();
        var lastItemIsSeparator = true;

        foreach (var cmd in commands)
        {
            if (cmd is SeparatorCommand)
            {
                if (!lastItemIsSeparator)
                {
                    menu.Items.Add(new Separator());
                    lastItemIsSeparator = true;
                }

                continue;
            }

            if (!cmd.IsVisible || !cmd.CanHandle(element))
            {
                continue;
            }

            var menuItem = new MenuItem { Header = cmd.Label, Icon = CreateIcon(cmd.Icon) };
            menuItem.Click += (_, _) => cmd.Invoke(element);
            menu.Items.Add(menuItem);
            lastItemIsSeparator = false;
        }

        return menu;
    }

    /// <summary>Edge menu: commands for the bundled relationships, with sub-menu groups.</summary>
    public static ContextMenu BuildForEdge(IReadOnlyList<IRelationshipContextCommand> commands,
        string sourceId, string targetId, List<Relationship> relationships)
    {
        var menu = new ContextMenu();
        if (relationships.Count == 0)
        {
            return menu;
        }

        var subMenus = new Dictionary<string, MenuItem>();
        foreach (var cmd in commands)
        {
            if (!cmd.CanHandle(relationships))
            {
                continue;
            }

            var menuItem = new MenuItem { Header = cmd.Label, Icon = CreateIcon(cmd.Icon) };
            menuItem.Click += (_, _) => cmd.Invoke(sourceId, targetId, relationships);

            if (!string.IsNullOrEmpty(cmd.SubMenuGroup))
            {
                if (!subMenus.TryGetValue(cmd.SubMenuGroup, out var parentMenu))
                {
                    parentMenu = new MenuItem { Header = cmd.SubMenuGroup };
                    subMenus[cmd.SubMenuGroup] = parentMenu;
                    menu.Items.Add(parentMenu);
                }

                parentMenu.Items.Add(menuItem);
            }
            else
            {
                menu.Items.Add(menuItem);
            }
        }

        return menu;
    }

    /// <summary>Background menu: global commands operating on the current selection.</summary>
    public static ContextMenu BuildForGlobal(IReadOnlyList<IGlobalCommand> commands, List<CodeElement> selectedElements)
    {
        var menu = new ContextMenu();
        foreach (var command in commands)
        {
            if (!command.CanHandle(selectedElements))
            {
                continue;
            }

            var menuItem = new MenuItem { Header = command.Label, Icon = CreateIcon(command.Icon) };
            menuItem.Click += (_, _) => command.Invoke(selectedElements);
            menu.Items.Add(menuItem);
        }

        return menu;
    }

    private static Image? CreateIcon(System.Windows.Media.ImageSource? source)
    {
        if (source is null)
        {
            return null;
        }

        return new Image { Width = 16, Height = 16, Source = source };
    }
}
