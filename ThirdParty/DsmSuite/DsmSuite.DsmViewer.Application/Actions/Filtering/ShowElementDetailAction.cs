// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Application.Actions.Base;
using DsmSuite.DsmViewer.Application.Actions.Management;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Model.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace DsmSuite.DsmViewer.Application.Actions.Filtering
{
    public class ShowElementDetailAction : IAction
    {
        private readonly IDsmModel _model;
        private readonly IDsmElement _provider, _consumer;
        private readonly IActionContext _actionContext;
        private List<int> _before;

        public const ActionType RegisteredType = ActionType.ShowElementDetail;

        public ShowElementDetailAction(IDsmModel model, IActionContext context, IReadOnlyDictionary<string, string> data)
        {
            _model = model;
            _actionContext = context;

            if (_model != null  &&  data != null)
            {
                int? id;
                ActionReadOnlyAttributes atts = new(_model, data);

                id = atts.GetNullableInt(nameof(_provider));
                if (id.HasValue)
                    _provider = _model.GetElementById(id.Value);
                id = atts.GetNullableInt(nameof(_consumer));
                if (id.HasValue)
                    _consumer = _model.GetElementById(id.Value);
                _before = atts.GetListIntCompact(nameof(_before));
            }
        }

        public ShowElementDetailAction(IDsmModel model, IDsmElement provider, IDsmElement consumer)
        {
            _model = model;
            _provider = provider;
            _consumer = consumer;
            _before = null;
        }
        
        public ActionType Type => RegisteredType;
        public string Title => "ShowElementDetail";
        public string Description => $"provider={_provider?.Fullname} consumer={_consumer?.Fullname}";

        public object Do()
        {
            _before = new();
            foreach (IDsmElement e in _model.GetElements())
                if (e.IsIncludedInTree)
                    _before.Add(e.Id);
            _before.Sort();

            _model.IncludeInTree(_model.RootElement, false);
            if (_provider != null)
                _model.IncludeInTree(_provider, true);
            if (_consumer != null)
                _model.IncludeInTree(_consumer, true);
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
            return _model != null  &&  (_provider != null  ||  _consumer != null); 
        }

        public IReadOnlyDictionary<string, string> Data
        {
            get
            {
                ActionAttributes attributes = new();
                if (_provider != null)
                    attributes.SetInt(nameof(_provider), _provider.Id);
                if (_consumer != null)
                    attributes.SetInt(nameof(_consumer), _consumer.Id);
                attributes.SetListIntCompact(nameof(_before), _before);
                return attributes.Data;
            }
        }
    }
}
