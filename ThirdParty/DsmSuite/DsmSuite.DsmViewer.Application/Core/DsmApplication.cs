// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using DsmSuite.Analyzer.Model.Core;
using DsmSuite.Common.Util;
using DsmSuite.DsmViewer.Application.Actions.Element;
using DsmSuite.DsmViewer.Application.Actions.Filtering;
using DsmSuite.DsmViewer.Application.Actions.Management;
using DsmSuite.DsmViewer.Application.Actions.Relation;
using DsmSuite.DsmViewer.Application.Actions.Snapshot;
using DsmSuite.DsmViewer.Application.Import.Common;
using DsmSuite.DsmViewer.Application.Import.Dsi;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Application.Metrics;
using DsmSuite.DsmViewer.Application.Queries;
using DsmSuite.DsmViewer.Application.Sorting;
using DsmSuite.DsmViewer.Model.Core;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Core
{
    public class DsmApplication : IDsmApplication
    {
        private IDsmModel _dsmModel;
        private ActionManager _actionManager;
        private readonly ActionStore _actionStore;
        private readonly DsmQueries _queries;
        private readonly DsmMetrics _metrics;

        public event EventHandler<bool> Modified;
        public event EventHandler ActionPerformed;

        public DsmApplication(IDsmModel dsmModel)
        {
            _dsmModel = dsmModel;
            _queries = new DsmQueries(dsmModel);
            _metrics = new DsmMetrics();
            _actionStore = new ActionStore();
            _actionManager = new ActionManager();
            _actionManager.ActionPerformed += OnActionPerformed;
        }


        /// <summary>
        /// Set a new model and actionManager. The current model and actionmanager are
        /// discarded.
        /// </summary>
        private void LoadModel(IDsmModel model) {
            _dsmModel = model;
            _actionManager = new ActionManager();
            _actionStore.LoadFromModel(_actionManager, _dsmModel);
            _actionManager.ActionPerformed += OnActionPerformed;
            IsModified = false;
            Modified?.Invoke(this, IsModified);
        }


        private void OnActionPerformed(object sender, EventArgs e)
        {
            ActionPerformed?.Invoke(this, e);
            IsModified = true;
            Modified?.Invoke(this, IsModified);
        }

        public bool CanUndo()
        {
            return _actionManager.CanUndo();
        }

        public string GetUndoActionDescription()
        {
            return _actionManager.GetCurrentUndoAction()?.Description;
        }

        public void Undo()
        {
            _actionManager.Undo();
        }
        public void GotoAction(IAction action)
        {
            _actionManager.Goto(action);
        }

        public bool CanRedo()
        {
            return _actionManager.CanRedo();
        }

        public string GetRedoActionDescription()
        {
            return _actionManager.GetCurrentRedoAction()?.Description;
        }

        public void Redo()
        {
            _actionManager.Redo();
        }

        public async Task AsyncImportDsiModel(string dsiFilename, string dsmFilename,
                bool autoPartition, bool compressDsmFile, IProgress<ProgressInfo> progress)
        {
            IDsmModel model = await Task.Run( () =>
                    ImportDsiModel(dsiFilename, dsmFilename, autoPartition, compressDsmFile, progress) );
            LoadModel(model);
        }


        /// <summary>
        /// Create a model by loading data from <c>dsiFilename</c>, save it as dsm to <c>dsmFilename</c>
        /// and return the model.
        /// </summary>
        private IDsmModel ImportDsiModel(string dsiFilename, string dsmFilename,
                bool autoPartition, bool compressDsmFile, IProgress<ProgressInfo> progress)
        {
            Assembly assembly = Assembly.GetEntryAssembly();

            //---- Read file into DsiModel
            DsiModel dsiModel = new DsiModel("Builder", new List<string>(), assembly);
            dsiModel.Load(dsiFilename, progress);

            //---- Create DsmModel from DsiModel
            IDsmModel model = new DsmModel("Viewer", assembly);
            DsiImporter importer = new DsiImporter(dsiModel, model, autoPartition);
            importer.Import(progress);

            //---- Save DsmModel
            model.SaveModel(dsmFilename, compressDsmFile, progress);

            return model;
        }


        // Changed 2026-07 for CSharpCodeAnalyst: AsyncImportSqlModel / ImportSqlModel and the
        // SqlImporter they used were removed. The .sql import is not offered by the embedded
        // matrix view, and dropping it removes the Dapper and Microsoft.Data.Sqlite dependencies.


        public async Task AsyncOpenModel(string dsmFilename, Progress<ProgressInfo> progress)
        {
            IDsmModel model = new DsmModel("Viewer", Assembly.GetExecutingAssembly());
            await Task.Run(() => model.LoadModel(dsmFilename, progress));
            LoadModel(model);
        }


        public async Task AsyncSaveModel(string dsmFilename, Progress<ProgressInfo> progress)
        {
            _actionStore.SaveToModel(_actionManager, _dsmModel);
            await Task.Run(() => _dsmModel.SaveModel(dsmFilename, _dsmModel.IsCompressed, progress));
            IsModified = false;
            Modified?.Invoke(this, IsModified);
        }


        public IDsmElement RootElement => _dsmModel.RootElement;

        public bool IsModified { get; private set; }

        public IEnumerable<WeightedElement> GetElementConsumers(IDsmElement element)
        {
            return _queries.FindConsumersOf(element);
        }

        public IEnumerable<WeightedElement> GetElementProvidedElements(IDsmElement element)
        {
            return _queries.FindElementsProvidedBy(element);
        }

        public IEnumerable<WeightedElement> GetElementProviders(IDsmElement element)
        {
            return _queries.FindProvidersFor(element);
        }

        public IEnumerable<IDsmRelation> FindResolvedRelations(IDsmElement consumer, IDsmElement provider)
        {
            return _queries.FindRelations(consumer, provider);
        }

        public IEnumerable<IDsmRelation> FindRelations(IDsmElement consumer, IDsmElement provider)
        {
            return _dsmModel.FindRelations(consumer, provider);
        }

        public int GetRelationCount(IDsmElement consumer, IDsmElement provider)
        {
            return _dsmModel.GetRelationCount(consumer, provider);
        }

        public IEnumerable<IDsmRelation> FindIngoingRelations(IDsmElement element)
        {
            return _queries.FindConsumingRelations(element);
        }

        public IEnumerable<IDsmRelation> FindOutgoingRelations(IDsmElement element)
        {
            return _queries.FindProvidingRelations(element);
        }

        public IEnumerable<IDsmRelation> FindInternalRelations(IDsmElement element)
        {
            return _queries.FindInternalRelations(element);
        }

        public IEnumerable<IDsmRelation> FindExternalRelations(IDsmElement element)
        {
            return _queries.FindExternalRelations(element);
        }

        public IEnumerable<WeightedElement> GetRelationProviders(IDsmElement consumer, IDsmElement provider)
        {
            return _queries.FindRelationProviders(consumer, provider);
        }

        public IEnumerable<WeightedElement> GetRelationConsumers(IDsmElement consumer, IDsmElement provider)
        {
            return _queries.FindRelationConsumers(consumer, provider);
        }

        public int GetHierarchicalCycleCount(IDsmElement element)
        {
            return _dsmModel.GetHierarchicalCycleCount(element);
        }

        public int GetSystemCycleCount(IDsmElement element)
        {
            return _dsmModel.GetSystemCycleCount(element);
        }

        public IDsmElement NextSibling(IDsmElement element)
        {
            return _dsmModel.NextSibling(element);
        }

        public IDsmElement PreviousSibling(IDsmElement element)
        {
            return _dsmModel.PreviousSibling(element);
        }

        public bool IsFirstChild(IDsmElement element)
        {
            return _dsmModel.PreviousSibling(element) == null;
        }

        public bool IsLastChild(IDsmElement element)
        {
            return _dsmModel.NextSibling(element) == null;
        }

        public bool HasChildren(IDsmElement element)
        {
            return element?.Children.Count > 0;
        }

        public void Sort(IDsmElement element, string algorithm)
        {
            ElementSortAction action = new ElementSortAction(_dsmModel, element, algorithm);
            _actionManager.Execute(action);
        }

        public void SortRecursively(IDsmElement element, string algorithm)
        {
            IAction action = new ElementSortRecursiveAction(_dsmModel, element, algorithm);
            _actionManager.Execute(action);
        }

        public IEnumerable<string> GetSupportedSortAlgorithms()
        {
            return SortAlgorithmFactory.GetSupportedAlgorithms();
        }

        public void MoveUp(IDsmElement element)
        {
            ElementMoveUpAction action = new ElementMoveUpAction(_dsmModel, element);
            _actionManager.Execute(action);
        }

        public void MoveDown(IDsmElement element)
        {
            ElementMoveDownAction action = new ElementMoveDownAction(_dsmModel, element);
            _actionManager.Execute(action);
        }

        public IEnumerable<string> GetElementTypes()
        {
            return _dsmModel.GetElementTypes();
        }

        public int GetDependencyWeight(IDsmElement consumer, IDsmElement provider)
        {
            return _dsmModel.GetDependencyWeight(consumer, provider);
        }

        public int GetDirectDependencyWeight(IDsmElement consumer, IDsmElement provider)
        {
            return _dsmModel.GetDirectDependencyWeight(consumer, provider);
        }

        public CycleType IsCyclicDependency(IDsmElement consumer, IDsmElement provider)
        {
            return _dsmModel.IsCyclicDependency(consumer, provider);
        }

        public IList<IDsmElement> SearchElements(string searchText, IDsmElement searchInElement, bool caseSensitive, string elementTypeFilter, bool markMatchingElements)
        {
            return _dsmModel.SearchElements(searchText, searchInElement, caseSensitive, elementTypeFilter, markMatchingElements);
        }

        public IDsmElement GetElementByFullname(string text)
        {
            return _dsmModel.GetElementByFullname(text);
        }

        public IDsmElement CreateElement(string name, string type, IDsmElement parent, int index)
        {
            ElementCreateAction action = new ElementCreateAction(_dsmModel, name, type, parent, index);
            return _actionManager.Execute(action) as IDsmElement;
        }

        public void DeleteElement(IDsmElement element)
        {
            ElementDeleteAction action = new ElementDeleteAction(_dsmModel, element);
            _actionManager.Execute(action);
        }

        public void ChangeElementName(IDsmElement element, string name)
        {
            ElementChangeNameAction action = new ElementChangeNameAction(_dsmModel, element, name);
            _actionManager.Execute(action);
        }

        public void ChangeElementType(IDsmElement element, string type)
        {
            ElementChangeTypeAction action = new ElementChangeTypeAction(_dsmModel, element, type);
            _actionManager.Execute(action);
        }

        public void ChangeElementParent(IDsmElement element, IDsmElement newParent, int index)
        {
            if (_dsmModel.IsChangeElementParentAllowed(element, newParent))
            {
                ElementChangeParentAction action = new ElementChangeParentAction(_dsmModel, element, newParent, index);
                _actionManager.Execute(action);
            }
        }

        public void CutElement(IDsmElement element)
        {
            ElementCutAction action = new ElementCutAction(_dsmModel, element, _actionManager.GetContext());
            _actionManager.Execute(action);
        }

        public void CopyElement(IDsmElement element)
        {
            ElementCopyAction action = new ElementCopyAction(_dsmModel, element, _actionManager.GetContext());
            _actionManager.Execute(action);
        }

        public void PasteElement(IDsmElement newParent, int index)
        {
            ElementPasteAction action = new ElementPasteAction(_dsmModel, newParent, index, _actionManager.GetContext());
            _actionManager.Execute(action);
        }


        public IDsmRelation CreateRelation(IDsmElement consumer, IDsmElement provider, string type, int weight)
        {
            RelationCreateAction action = new RelationCreateAction(_dsmModel, consumer.Id, provider.Id, type, weight);
            return _actionManager.Execute(action) as IDsmRelation;
        }

        public void DeleteRelation(IDsmRelation relation)
        {
            RelationDeleteAction action = new RelationDeleteAction(_dsmModel, relation);
            _actionManager.Execute(action);
        }

        public void ChangeRelationType(IDsmRelation relation, string type)
        {
            RelationChangeTypeAction action = new RelationChangeTypeAction(_dsmModel, relation, type);
            _actionManager.Execute(action);
        }

        public void ChangeRelationWeight(IDsmRelation relation, int weight)
        {
            RelationChangeWeightAction action = new RelationChangeWeightAction(_dsmModel, relation, weight);
            _actionManager.Execute(action);
        }

        public IEnumerable<string> GetRelationTypes()
        {
            return _dsmModel.GetRelationTypes();
        }

        public void ShowElementDetail(IDsmElement provider, IDsmElement consumer)
        {
            IAction action = new ShowElementDetailAction(_dsmModel, provider, consumer);
            _actionManager.Execute(action);
        }

        public void ShowElementContext(IDsmElement provider)
        {
            IAction action = new ShowElementContextAction(_dsmModel, provider);
            _actionManager.Execute(action);
        }

        public void MakeSnapshot(string description)
        {
            MakeSnapshotAction action = new MakeSnapshotAction(_dsmModel, description);
            _actionManager.Execute(action);
        }

        public IEnumerable<IAction> GetActions()
        {
            return _actionManager.GetActionsInReverseChronologicalOrder();
        }

        public IEnumerable<IAction> GetAllActions()
        {
            return _actionManager.GetActionsInChronologicalOrder()
                .Concat(_actionManager.GetRedoActionsInChronologicalOrder());
        }

        public void ClearActions()
        {
            _actionManager.Clear();
            _dsmModel.ClearActions();
            IsModified = true;
            Modified?.Invoke(this, IsModified);
        }

        public int GetElementSize(IDsmElement element)
        {
            return _metrics.GetElementSize(element);
        }

        public int GetElementCount()
        {
            return _dsmModel.GetElementCount();
        }
    }
}
