// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Application.Actions.Base;
using DsmSuite.DsmViewer.Application.Actions.Management;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Application.Sorting;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Actions.Element
{
    public class ElementSortAction : IAction
    {
        private readonly IDsmModel _model;
        private readonly IActionContext _actionContext;
        private readonly IDsmElement _element;
        private readonly string _algorithm;
        private List<int> _order;

        public const ActionType RegisteredType = ActionType.ElementSort;

        public ElementSortAction(IDsmModel model, IActionContext context, IReadOnlyDictionary<string, string> data)
        {
            _model = model;
            _actionContext = context;
            if (_model != null  &&  data != null)
            {
                ActionReadOnlyAttributes attributes = new ActionReadOnlyAttributes(_model, data);

                _element = attributes.GetElement(nameof(_element));
                _algorithm = attributes.GetString(nameof(_algorithm));
                _order = attributes.GetListInt(nameof(_order));
            }
        }

        public ElementSortAction(IDsmModel model, IDsmElement element, string algorithm)
        {
            _model = model;
            _element = element;
            _algorithm = algorithm;
            _order = null;
        }

        public ActionType Type => RegisteredType;
        public string Title => "Partition element";
        public string Description => $"element={_element.Fullname} algorithm={_algorithm}";

        public object Do()
        {
            ISortAlgorithm sortAlgorithm = SortAlgorithmFactory.CreateAlgorithm(_model, _element, _algorithm);
            SortResult sortResult = sortAlgorithm.Sort();
            _model.ReorderChildren(_element, sortResult);
            _order = sortResult.GetOrder();

            _model.AssignElementOrder();

            return null;
        }

        public void Undo()
        {
            SortResult sortResult = new SortResult(_order);
            sortResult.InvertOrder();
            _model.ReorderChildren(_element, sortResult);

            _model.AssignElementOrder();
        }

        public bool IsValid()
        {
            return (_model != null) &&
                   (_element != null) &&
                   (_algorithm != null) &&
                   (_order != null);
        }

        public IReadOnlyDictionary<string, string> Data
        {
            get
            {
                ActionAttributes attributes = new ActionAttributes();
                attributes.SetInt(nameof(_element), _element.Id);
                attributes.SetString(nameof(_algorithm), _algorithm);
                attributes.SetListInt(nameof(_order), _order);
                return attributes.Data;
            }
        }
    }
}
