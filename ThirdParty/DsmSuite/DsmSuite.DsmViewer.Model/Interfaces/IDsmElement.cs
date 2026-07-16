// SPDX-License-Identifier: GPL-3.0-or-later
namespace DsmSuite.DsmViewer.Model.Interfaces
{
    public interface IDsmElement : IComparable
    {
        /// <summary>
        /// Unique and non-modifiable Number identifying the element.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Number identifying sequential order of the element in element tree.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Type of element.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Name of the element.
        /// </summary>
        string Name { get; }

        // Named properties found for this element
        IDictionary<string, string> Properties { get; }

        // Property names found across all elements
        IEnumerable<string> DiscoveredElementPropertyNames();

        /// <summary>
        /// Full name of the element based on its position in the element hierarchy
        /// </summary>
        string Fullname { get; }

        string GetRelativeName(IDsmElement element);

        bool IsDeleted { get; }
        bool IsBookmarked { get; set; }
        bool IsRoot { get; }

        /// <summary>
        /// Has the element any children that are in the tree (see <see cref="IsIncludedInTree"/>).
        /// </summary>
        bool HasChildren { get; }

        /// <summary>
        /// Tree children of the element (see <see cref="IsIncludedInTree"/>).
        /// </summary>
        IList<IDsmElement> Children { get; }

        IList<IDsmElement> AllChildren { get; }

        int IndexOfChild(IDsmElement child);

        bool ContainsChildWithName(string name);

        /// <summary>
        /// Parent of the element.
        /// </summary>
        IDsmElement Parent { get; }

        /// <summary>
        /// Is the selected element a recursive child of this element.
        /// </summary>
        bool IsRecursiveChildOf(IDsmElement element);

        /// <summary>
        /// Is the element expanded in the viewer. This is only meaningful for elements with children.<br/>
        /// If an element is expanded, each of its children has a line in the matrix and the element
        /// itself hasn't, but is used to vertically group the children.<br/>
        /// If an element is not expanded, its children are not displayed and the element itself has
        /// a row in the matrix.<br/>
        /// This property is only relevant and should only be changed for elements that are in the
        /// tree (see <see cref="IsIncludedInTree"/>).
        /// </summary>
        bool IsExpanded { get; set; }

        /// <summary>
        /// Set or clear the IsExpanded property recursively for elements in the tree.
        /// </summary>
        void ExpandRecursively(bool expanded);

        /// <summary>
        /// Is the element match in search.
        /// </summary>
        bool IsMatch { get; set; }

        /// <summary>
        /// Is the element included in the tree. The tree is the set of elements that are in scope
        /// for the viewer, i.e. that are not filtered out.
        /// </summary>
        bool IsIncludedInTree { get; set; }
    }
}
