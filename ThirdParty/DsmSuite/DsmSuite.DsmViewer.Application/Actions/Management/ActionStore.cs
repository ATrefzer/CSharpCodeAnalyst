// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using DsmSuite.Common.Util;
using DsmSuite.DsmViewer.Application.Actions.Element;
using DsmSuite.DsmViewer.Application.Actions.Filtering;
using DsmSuite.DsmViewer.Application.Actions.Relation;
using DsmSuite.DsmViewer.Application.Actions.Snapshot;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Actions.Management
{
    /// <summary>
    /// Stores/restores user actions to/from the model.
    /// </summary>
    /// <remarks>
    /// The ActionType enum tags are used as the action string for the model.
    /// </remarks>
    public class ActionStore
    {
        // Translate ActionType's to the implementing classes.
        // The ActionType enum tags are used as the action string for the model.
        // TODO Isn't a string->Type dictionary more suitable?
        // TODO Make certain (compile/runtime) all actions are indeed in the table (cut/copy/paste currently aren't)
        // otherwise we can't load model that we saved.
        private readonly Dictionary<ActionType, Type> _types;

        public ActionStore()
        {
            _types = new Dictionary<ActionType, Type>();
            RegisterActionTypes();
        }


        /// <summary>
        /// Loads all actions from the model into the ActionManager.
        /// Unrecognized actions are logged but otherwise ignored.
        /// If any action cannot be loaded correctly, the ActionManager is cleared.
        /// </summary>
        public void LoadFromModel(IActionManager actionManager, IDsmModel model)
        {
            actionManager.Clear();
            foreach (IDsmAction action in model.GetActions())
            {
                IAction instance = LoadAction(actionManager, model, action);
                if (instance != null)
                    actionManager.Add(instance);
                else
                    Logger.LogError($"Cannot instantiate action {action.Id} {action.Type}.");
            }

            if (!actionManager.Validate())
            {
                Logger.LogWarning($"Invalid action found.");
                actionManager.Clear();
            }
        }


        private IAction LoadAction(IActionManager actionManager, IDsmModel model, IDsmAction action)
        {
            ActionType actionType;
            Type type;
            object[] args;

            if (!ActionType.TryParse(action.Type, out actionType))
                Logger.LogWarning($"Unknown action {action.Type} in model.");
            if (!_types.TryGetValue(actionType, out type))
                Logger.LogError($"Action {action.Type} not in table.");

            if (action.Actions == null)
                args = [model, actionManager.GetContext(), action.Data];
            else
            {
                List<IAction> subactions = new();
                foreach (IDsmAction subaction in action.Actions)
                {
                    IAction a = LoadAction(actionManager, model, subaction);
                    if (a == null)
                        return null;
                    subactions.Add(a);
                }

                args = [model, actionManager.GetContext(), action.Data, subactions];
            }

            return Activator.CreateInstance(type, args) as IAction;
        }


        /// <summary>
        /// Saves all actions in the actionManager to the model.
        /// </summary>
        public void SaveToModel(IActionManager actionManager, IDsmModel model)
        {
            if (actionManager.Validate())
            {
                model.ClearActions();
                foreach (IAction action in actionManager.GetActionsInChronologicalOrder())
                {
                    model.AddAction(action.Type.ToString(), action.Data, new MultiActionDTO(action).Actions);
                }
            }
        }


        // Strings here must be kept in sync with MultiAction.
        private void RegisterActionTypes()
        {
            //TODO cut/copy/paste actions not present
            _types[ElementChangeNameAction.RegisteredType] = typeof(ElementChangeNameAction);
            _types[ElementChangeTypeAction.RegisteredType] = typeof(ElementChangeTypeAction);
            _types[ElementChangeParentAction.RegisteredType] = typeof(ElementChangeParentAction);
            _types[ElementCreateAction.RegisteredType] = typeof(ElementCreateAction);
            _types[ElementDeleteAction.RegisteredType] = typeof(ElementDeleteAction);
            _types[ElementMoveDownAction.RegisteredType] = typeof(ElementMoveDownAction);
            _types[ElementMoveUpAction.RegisteredType] = typeof(ElementMoveUpAction);
            _types[ElementSortAction.RegisteredType] = typeof(ElementSortAction);
            _types[ElementSortRecursiveAction.RegisteredType] = typeof(ElementSortRecursiveAction);

            _types[ShowElementDetailAction.RegisteredType] = typeof(ShowElementDetailAction);
            _types[ShowElementContextAction.RegisteredType] = typeof(ShowElementContextAction);

            _types[RelationChangeTypeAction.RegisteredType] = typeof(RelationChangeTypeAction);
            _types[RelationChangeWeightAction.RegisteredType] = typeof(RelationChangeWeightAction);
            _types[RelationCreateAction.RegisteredType] = typeof(RelationCreateAction);
            _types[RelationDeleteAction.RegisteredType] = typeof(RelationDeleteAction);

            _types[MakeSnapshotAction.RegisteredType] = typeof(MakeSnapshotAction);
        }
    }
}
