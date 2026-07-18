// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Application.Actions.Base;
using DsmSuite.DsmViewer.Application.Actions.Management;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Application.Queries;
using DsmSuite.DsmViewer.Model.Interfaces;
using System.Collections.Generic;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace DsmSuite.DsmViewer.Application.Actions.Filtering
{
    public class ShowElementContextAction : IAction
    {
        private readonly IDsmModel _model;
        private readonly IDsmElement _provider;
        private readonly IActionContext _actionContext;
        private List<int> _before;

        public const ActionType RegisteredType = ActionType.ShowElementContext;

        public ShowElementContextAction(IDsmModel model, IActionContext context, IReadOnlyDictionary<string, string> data)
        {
            _model = model;
            _actionContext = context;

            if (_model != null  &&  data != null)
            {
                ActionReadOnlyAttributes atts = new(_model, data);

                _provider = atts.GetElement(nameof(_provider));
                _before = atts.GetListIntCompact(nameof(_before));
            }
        }

        public ShowElementContextAction(IDsmModel model, IDsmElement provider)
        {
            _model = model;
            _provider = provider;
            _before = null;
        }

        public ActionType Type => RegisteredType;
        public string Title => "ShowElementContext";
        public string Description => $"provider={_provider?.Fullname}";

        public object Do()
        {
            DsmQueries queries = new DsmQueries(_model);

            _before = new();
            foreach (IDsmElement e in _model.GetElements())
                if (e.IsIncludedInTree)
                    _before.Add(e.Id);
            _before.Sort();

            _model.IncludeInTree(_model.RootElement, false);
            _model.IncludeInTree(_provider, true);
            foreach (WeightedElement consumer in queries.FindConsumersOf(_provider))
                _model.IncludeInTree(consumer.Element, true);
            foreach (WeightedElement provider in queries.FindProvidersFor(_provider))
                _model.IncludeInTree(provider.Element, true);

            return null;
        }

        public void Undo()
        {
            _model.IncludeInTree(_model.RootElement, false);
            foreach (int i in _before)
                _model.GetElementById(i).IsIncludedInTree = true;
        }

        public bool IsValid()
        {
            return _model != null  &&  _provider != null;
        }

        public IReadOnlyDictionary<string, string> Data
        {
            get
            {
                ActionAttributes attributes = new();
                attributes.SetInt(nameof(_provider), _provider.Id);
                attributes.SetListIntCompact(nameof(_before), _before);
                return attributes.Data;
            }
        }
    }
}

