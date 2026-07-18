// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.Common.Util;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Interfaces
{
    public interface IDsmApplication
    {
        event EventHandler<bool> Modified;
        event EventHandler ActionPerformed;

        Task AsyncImportDsiModel(string dsiFilename, string dsmFilename, bool autoPartition, bool compressDsmFile, IProgress<ProgressInfo> progress);
        // Changed 2026-07 for CSharpCodeAnalyst: AsyncImportSqlModel removed together with SqlImporter.

        Task AsyncOpenModel(string dsmFilename, Progress<ProgressInfo> progress);
        Task AsyncSaveModel(string dsmFilename, Progress<ProgressInfo> progress);
        bool IsModified { get; }
        bool CanUndo();
        string GetUndoActionDescription();
        void Undo();
        void GotoAction(IAction action);
        bool CanRedo();
        string GetRedoActionDescription();
        void Redo();
        IDsmElement RootElement { get; }
        IEnumerable<WeightedElement> GetElementConsumers(IDsmElement element);
        IEnumerable<WeightedElement> GetElementProvidedElements(IDsmElement element);
        IEnumerable<WeightedElement> GetElementProviders(IDsmElement element);
        IEnumerable<WeightedElement> GetRelationProviders(IDsmElement consumer, IDsmElement provider);
        IEnumerable<WeightedElement> GetRelationConsumers(IDsmElement consumer, IDsmElement provider);
        IEnumerable<IDsmRelation> FindResolvedRelations(IDsmElement consumer, IDsmElement provider);
        IEnumerable<IDsmRelation> FindRelations(IDsmElement consumer, IDsmElement provider);
        int GetRelationCount(IDsmElement consumer, IDsmElement provider);
        IEnumerable<IDsmRelation> FindIngoingRelations(IDsmElement element);
        IEnumerable<IDsmRelation> FindOutgoingRelations(IDsmElement element);
        IEnumerable<IDsmRelation> FindInternalRelations(IDsmElement element);
        IEnumerable<IDsmRelation> FindExternalRelations(IDsmElement element);
        int GetHierarchicalCycleCount(IDsmElement element);
        int GetSystemCycleCount(IDsmElement element);
        IDsmElement NextSibling(IDsmElement element);
        IDsmElement PreviousSibling(IDsmElement element);
        bool HasChildren(IDsmElement element);
        void Sort(IDsmElement element, string algorithm);
        void SortRecursively(IDsmElement element, string algorithm);
        IEnumerable<string> GetSupportedSortAlgorithms();
        void MoveUp(IDsmElement element);
        void MoveDown(IDsmElement element);
        IEnumerable<string> GetElementTypes();


        int GetDependencyWeight(IDsmElement consumer, IDsmElement provider);
        CycleType IsCyclicDependency(IDsmElement consumer, IDsmElement provider);
        IList<IDsmElement> SearchElements(string searchText, IDsmElement searchInElement, bool caseSensitive, string elementTypeFilter, bool markMatchingElements);
        IDsmElement GetElementByFullname(string fullname);
        IDsmElement CreateElement(string name, string type, IDsmElement parent, int index);
        void DeleteElement(IDsmElement element);
        void ChangeElementName(IDsmElement element, string name);
        void ChangeElementType(IDsmElement element, string type);
        void ChangeElementParent(IDsmElement element, IDsmElement newParent, int index);
        void CutElement(IDsmElement element);
        void CopyElement(IDsmElement element);
        void PasteElement(IDsmElement newParent, int index);

        IDsmRelation CreateRelation(IDsmElement consumer, IDsmElement provider, string type, int weight);
        void DeleteRelation(IDsmRelation relation);
        void ChangeRelationType(IDsmRelation relation, string type);
        void ChangeRelationWeight(IDsmRelation relation, int weight);
        IEnumerable<string> GetRelationTypes();
        void MakeSnapshot(string name);

        /// <summary>
        /// Return the undoable actions.
        /// </summary>
        IEnumerable<IAction> GetActions();
        void ClearActions();
        /// <summary>
        /// Return the undoable and redoable actions in a single order.
        /// </summary>
        IEnumerable<IAction> GetAllActions();

        int GetElementSize(IDsmElement element);
        int GetElementCount();
        void ShowElementDetail(IDsmElement consumer, IDsmElement provider);
        void ShowElementContext(IDsmElement provider);
    }
}
