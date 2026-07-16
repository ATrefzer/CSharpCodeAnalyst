// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Queries
{
    /// <summary>
    /// Contains some but not all queries the application executes on the model
    /// </summary>
    /// TODO Can this class be made more useful?
    /// DsmApplication does some queries as well and dispatches others here. Can we make this class more useful
    /// by forwarding once and dispatching here?
    public class DsmQueries
    {

        private readonly IDsmModel _model;
        public DsmQueries(IDsmModel model)
        {
            _model = model;
        }

        /// <summary>
        /// Return sub-elements of <c>element</c> that are providers for elements outside
        /// of <c>element</c>. The result is ordered by provider name.
        /// </summary>
        public IEnumerable<WeightedElement> FindElementsProvidedBy(IDsmElement element)
        {
            var elements = _model.FindIngoingRelations(element)
                .OrderBy(x => x.Provider.Fullname)
                .GroupBy(x => x.Provider.Fullname)
                .Select( g => new WeightedElement(g.First().Provider, g.Sum(x => x.Weight)) )
                .ToList();

            return elements;
        }

        /// <summary>
        /// Return elements outside of <c>element</c> that are providers for elements within
        /// <c>element</c>. The result is ordered by provider name.
        /// </summary>
        public IEnumerable<WeightedElement> FindProvidersFor(IDsmElement element)
        {
            var elements = _model.FindOutgoingRelations(element)
                .OrderBy(x => x.Provider.Fullname)
                .GroupBy(x => x.Provider.Fullname)
                .Select( g => new WeightedElement(g.First().Provider, g.Sum(x => x.Weight)) )
                .ToList();

            return elements;
        }

        /// <summary>
        /// Return elements outside of <c>element</c> that are consumer of elements within
        /// <c>element</c>. The result is ordered by consumer name.
        /// </summary>
        public IEnumerable<WeightedElement> FindConsumersOf(IDsmElement element)
        {
            var elements = _model.FindIngoingRelations(element)
                .OrderBy(x => x.Consumer.Fullname)
                .GroupBy(x => x.Consumer.Fullname)
                .Select( g => new WeightedElement(g.First().Consumer, g.Sum(x => x.Weight)) )
                .ToList();

            return elements;
        }

        /// <summary>
        /// Return the consumers in the relations between (sub-elements of) <c>consumer</c> and
        /// <c>provider</c>. The result is ordered by consumer name.
        /// </summary>
        public IEnumerable<WeightedElement> FindRelationConsumers(IDsmElement consumer, IDsmElement provider)
        {
            var elements = _model.FindRelations(consumer, provider)
                .OrderBy(x => x.Consumer.Fullname)
                .GroupBy(x => x.Consumer.Fullname)
                .Select(g => new WeightedElement(g.First().Consumer, g.Sum(x => x.Weight)))
                .ToList();

            return elements;
        }

        /// <summary>
        /// Return the providers in the relations between (sub-elements of) <c>consumer</c> and
        /// <c>provider</c>. The result is ordered by provider name.
        /// </summary>
        public IEnumerable<WeightedElement> FindRelationProviders(IDsmElement consumer, IDsmElement provider)
        {
            var elements = _model.FindRelations(consumer, provider)
                .OrderBy(x => x.Provider.Fullname)
                .GroupBy(x => x.Provider.Fullname)
                .Select(g => new WeightedElement(g.First().Provider, g.Sum(x => x.Weight)))
                .ToList();

            return elements;
        }

        /// <summary>
        /// Return the relations between (sub-elements of) <c>consumer</c> and
        /// <c>provider</c>. The result is ordered by provider name, consumer name.
        /// </summary>
        public IEnumerable<IDsmRelation> FindRelations(IDsmElement consumer, IDsmElement provider)
        {
            var relations = _model.FindRelations(consumer, provider)
                .OrderBy(x => x.Provider.Fullname)
                .ThenBy(x => x.Consumer.Fullname)
                .ToList();
            return relations;
        }

        /// <summary>
        /// Return the relations consuming (a sub-elements of) <c>element</c>.
        /// </summary>
        public IEnumerable<IDsmRelation> FindConsumingRelations(IDsmElement element)
        {
            return _model.FindIngoingRelations(element);
        }

        /// <summary>
        /// Return the relations providing to (a sub-elements of) <c>element</c>.
        /// </summary>
        public IEnumerable<IDsmRelation> FindProvidingRelations(IDsmElement element)
        {
            return _model.FindOutgoingRelations(element);
        }

        /// <summary>
        /// Return the relations between sub-elements of <c>element</c>.
        /// </summary>
        public IEnumerable<IDsmRelation> FindInternalRelations(IDsmElement element)
        {
            return _model.FindInternalRelations(element);
        }

        /// <summary>
        /// Return the relations between (a sub-element of) <c>element</c> and an element
        /// outside of <c>element</c>.
        /// </summary>
        public IEnumerable<IDsmRelation> FindExternalRelations(IDsmElement element)
        {
            return _model.FindExternalRelations(element);
        }
    }
}
