// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.Common.Util;
using DsmSuite.DsmViewer.Application.Actions.Base;
using DsmSuite.DsmViewer.Application.Actions.Management;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Application.Sorting;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Actions.Element
{
    /// <summary>
    /// Recursively sort the subtree rooted at an element. Only sorts elements that are in the tree.
    /// </summary>
    public class ElementSortRecursiveAction : IMultiAction
    {
        private readonly IDsmModel _model;
        private readonly IActionContext _actionContext;
        private readonly IDsmElement _element;
        private readonly string _algorithm;
        private List<IAction> _actions; // The sort actions on descendant elements (possibly empty)

        public const ActionType RegisteredType = ActionType.ElementSortRecursive;

        public ElementSortRecursiveAction(IDsmModel model, IActionContext context,
                IReadOnlyDictionary<string, string> data, IEnumerable<IAction> actions)
        {
            _model = model;
            _actionContext = context;
            if (_model != null  &&  data != null)
            {
                ActionReadOnlyAttributes attributes = new ActionReadOnlyAttributes(_model, data);

                _element = attributes.GetElement(nameof(_element));
                _algorithm = attributes.GetString(nameof(_algorithm));
                _actions = new List<IAction>(actions);
            }
        }

        public ElementSortRecursiveAction(IDsmModel model, IDsmElement element, string algorithm)
        {
            _model = model;
            _element = element;
            _algorithm = algorithm;
            _actions = new();
        }

        public ActionType Type => RegisteredType;
        public string Title => "Partition element recursively";
        public string Description => $"element={_element.Fullname} algorithm={_algorithm}";

        private void doRecursive(IDsmElement element)
        {
            if (!element.HasChildren)
                return;

            Logger.LogInfo(element.Fullname);

            ElementSortAction action = new(_model, element, _algorithm);
            action.Do();
            _actions.Add(action);

            foreach (IDsmElement child in element.AllChildren)
                doRecursive(child);
        }

        public object Do()
        {
            _actions.Clear();
            doRecursive(_element);
            return null;
        }

        public void Undo()
        {
            foreach (IAction action in _actions)
                action.Undo();
        }

        public bool IsValid()
        {
            return _model != null  &&  _element != null  &&  _algorithm != null  &&  _actions != null;
        }

        public IReadOnlyDictionary<string, string> Data
        {
            get
            {
                ActionAttributes attributes = new ActionAttributes();
                attributes.SetInt(nameof(_element), _element.Id);
                attributes.SetString(nameof(_algorithm), _algorithm);
                return attributes.Data;
            }
        }

        public IEnumerable<IAction> Actions { get { return _actions; } }
    }
}
