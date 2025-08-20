//-------------------------------------------------------------------------------------------------
//  RuleEngine.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using RuleEngine.Primitives;

namespace RuleEngine
{
    /// <summary>
    /// Interface to define "Event" which can be feed into rule engine
    /// </summary>
    public interface IEvent
    {
        String EventName { get; }
        IEvent CreateInstance(String eventName);
        int GetPropertyId(String propertyName);
        Object GetProperty(int propertyId);
        void SetProperty(int propertyId, Object value);
    }

    /// <summary>
    /// Helper class. An equality comparer that compares objects for reference equality.
    /// </summary>
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        private static readonly ReferenceEqualityComparer<T> instance
                                                            = new ReferenceEqualityComparer<T>();
        public static ReferenceEqualityComparer<T> Instance { get { return instance; } }

        public bool Equals(T left, T right)
        {
            return Object.ReferenceEquals(left, right);
        }

        public int GetHashCode(T value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }
    }

    /// <summary>
    /// Prototype of actor which can register into engine
    /// </summary>
    public delegate void Actor(IEvent e);

    public class Engine
    {
        public String errorMessage { get; private set; }
        internal IEvent MetaEvent { get; private set; }

        public Engine(IEvent metaEvent)
        {
            if ( metaEvent == null )
                errorMessage = "metaEvent is null";
            MetaEvent = metaEvent;
        }

        /// <summary>
        /// Parse and setup new rules, input as JSON format
        /// </summary>
        public bool AddRules(String jsonRules)
        {
            // Parse and validate
            JsonRulesParser jsonParser = new JsonRulesParser();
            List<Rule> rules;
            if ( !jsonParser.Parse(jsonRules, out rules) )
            {
                errorMessage = String.Format("Invalid JSON format: {0}", jsonParser.errorMessage);
                return false;
            }

            // Add rules
            return AddRules(rules);
        }

        /// <summary>
        /// Parse and setup new rules, input as rule list
        /// </summary>
        public bool AddRules(List<Rule> rules)
        {
            // Process each rule
            int i;
            for ( i=0; i<rules.Count; i++ )
                if ( !AddRule(rules[i]) )
                    break;

            // Delete new added rules on error
            if ( i < rules.Count )
            {
                for ( int j=i-1; j>=0; j-- )
                    DeleteRule(rules[j].name);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Delete one rule
        /// </summary>
        public void DeleteRule(string ruleName)
        {
            // Find the event name which generate by this rule, delete by it.
            string eventName;
            if ( !_ruleEventMap.TryGetValue(ruleName, out eventName) )
                return;
            DeleteRuleByEvent(eventName);

            _ruleEventMap.Remove(ruleName);
        }

        /// <summary>
        /// Delete one rule, also delete all Actors registered on the rule generated event
        /// </summary>
        private void DeleteRuleByEvent(String eventName)
        {
            // First delete the actors registered on this event
            _actors.Remove(eventName);

            // Start from the generator of this event, collect all linked primitives as delete
            // candidates
            List<IPrimitive> candidates = new List<IPrimitive>();
            List<int> candidateInvolveCounts = new List<int>();

            if ( _eventGenerators.ContainsKey(eventName) )
            {
                candidates.Add(_eventGenerators[eventName]);
                candidateInvolveCounts.Add(0);
            }

            for ( int i=0; i<candidates.Count; i++ )
            {
                List<IPrimitive> dependees = PrimitiveDependees(candidates[i]);

                // Increase involve count if already in candidates, otherwise add new
                foreach ( IPrimitive dep in dependees )
                {
                    int iExist = candidates.FindIndex(x => Object.ReferenceEquals(x, dep));
                    if ( iExist < 0 )
                    {
                        candidates.Add(dep);
                        candidateInvolveCounts.Add(1);
                    }
                    else
                        candidateInvolveCounts[iExist]++;
                }
            }

            // Ignore primitives which involve counter is less than the depender count in
            // the primitive. Those primitives is shared by other rules, cannot be deleted.
            // Also ignore all dependees of the non-deletable one. 
            List<IPrimitive> nonDeletableList = new List<IPrimitive>();
            for ( int i=0; i<candidates.Count; i++ )
            {
                if ( nonDeletableList.FindIndex(x => Object.ReferenceEquals(x, candidates[i])) >= 0 ||
                     candidateInvolveCounts[i] < candidates[i].DependerCount )
                {
                    nonDeletableList.AddRange(PrimitiveDependees(candidates[i]));
                    candidates.RemoveAt(i);
                    i--;
                }
            }

            // The candidates now contains all primitives to be deleted.
            // If the _sigDispatcher on this event contains targets outside of candidates 
            // collection, that means this event is re-used by other rules and we will pending 
            // delete until deleting that rule.
            if ( _sigDispatcher.ContainsKey(eventName) )
                foreach ( SignalTarget target in _sigDispatcher[eventName].Targets )
                    if ( candidates.FindIndex(x => Object.ReferenceEquals(x, target.Owner)) < 0 )
                        return;

            List<String> moreEventsToDelete = new List<string>();

            // Delete primitives from network
            for ( int i=0; i<candidates.Count; i++ )
            {
                List<SignalSource> srcs = candidates[i].SignalReceiver.ConnectedSources;
                for ( int iSrc=srcs.Count-1; iSrc>=0; iSrc-- )
                {
                    // If we removed one target from an event dispatcher, and the event has no 
                    // actors, this means this event is pending delete, we will try to delete it
                    // again.
                    if ( srcs[iSrc].Owner == _sigDispatcher ) // Is in event dispatcher
                    {
                        String evtName = _sigDispatcher.First(
                            x => Object.ReferenceEquals(x.Value, srcs[iSrc])).Key;
                        if ( !_actors.ContainsKey(evtName) )
                            moreEventsToDelete.Add(evtName);
                    }
                    else
                        (srcs[iSrc].Owner as IPrimitive).DependerCount--;

                    srcs[iSrc].DisconnectFrom(candidates[i].SignalReceiver);
                }

                if ( candidates[i].ExtraDependees != null )
                    foreach ( IPrimitive dep in candidates[i].ExtraDependees )
                        dep.DependerCount--;

                // Delete extra resource possibly used by primitive
                if ( candidates[i] is IDisposable )
                    (candidates[i] as IDisposable).Dispose();
            }

            // Remove this event from dispatcher
            _sigDispatcher.Remove(eventName);

            _eventGenerators.Remove(eventName);

            // Further process any rules which was pending deleted before
            foreach ( String moreEvent in moreEventsToDelete )
                DeleteRuleByEvent(moreEvent);
        }

        /// <summary>
        /// Find all dependees of one primitive
        /// </summary>
        private List<IPrimitive> PrimitiveDependees(IPrimitive pm)
        {
            List<IPrimitive> dependees = new List<IPrimitive>();

            if ( pm.SignalReceiver != null )
                foreach ( SignalSource src in pm.SignalReceiver.ConnectedSources )
                    if ( src.Owner != _sigDispatcher ) // Ignore sources in _sigDispather
                        dependees.Add(src.Owner as IPrimitive);

            if ( pm.ExtraDependees != null )
                dependees.AddRange(pm.ExtraDependees);

            return dependees;
        }

        /// <summary>
        /// Register one event actor
        /// </summary>
        public void RegisterActor(String eventName, Actor actor, bool highPriority = false)
        {
            List<Actor> actors;
            if ( !_actors.TryGetValue(eventName, out actors) )
            {
                actors = new List<Actor>();
                _actors[eventName] = actors;
            }
            if ( highPriority )
                actors.Insert(0, actor);
            else
                actors.Add(actor);
        }

        /// <summary>
        /// Unregister one event actor
        /// </summary>
        public void UnRegisterActor(String eventName, Actor actor)
        {
            List<Actor> actors;
            if ( _actors.TryGetValue(eventName, out actors) )
            {
                int index = actors.FindIndex(x => Object.ReferenceEquals(x, actor));
                if ( index >= 0 )
                {
                    actors.RemoveAt(index);
                    if ( actors.Count == 0 )
                        _actors.Remove(eventName);
                }
            }
        }

        /// <summary>
        /// Feed one event into engine
        /// </summary>
        public void ProcessEvent(IEvent e)
        {
            // Dispatch signals registered on this event
            SignalSource sigSource;
            if ( _sigDispatcher.TryGetValue(e.EventName, out sigSource) )
                sigSource.Trigger(e);

            // If any rule interested in all events, dispatch to it.
            if ( _allEventsDispatcher != null )
                _allEventsDispatcher.Trigger(e);

            // Perform actions registered on this event
            List<Actor> actors;
            if ( _actors.TryGetValue(e.EventName, out actors) )
                foreach ( Actor actor in actors )
                    actor(e);
        }

        /// <summary>
        /// Data item inside the directed graph formed by primitive nodes of one rule
        /// </summary>
        class GraphNode
        {
            // Base node information
            public Rule.Node info;

            // Graph edges
            public List<GraphNode> edgeTo = new List<GraphNode>();
            public int nInboundEdge = 0;

            // Settle down primitive for this node
            public IPrimitive primitive = null;

            // Used to find share primitive
            // SignalSender member of primitive or SignalSource of _sigDispatcher entry
            public SignalSource sigSender = null;
            // Possible SignalSenderOnNegative member of primitive
            public SignalSource sigSenderOnNegative = null;
            // Reverse link edges in graph
            public Dictionary<GraphNode, Rule.Node.ConnectionInfo> connectFroms = 
                       new Dictionary<GraphNode, Rule.Node.ConnectionInfo>(
                           ReferenceEqualityComparer<GraphNode>.Instance);
        }

        /// <summary>
        /// Link nodes together by connectTo, to form directed graph
        /// </summary>
        private void LinkGraphNodes(List<GraphNode> graphNodes)
        {
            foreach ( GraphNode node in graphNodes )
            {
                // Connect by "ConnectTo" defined in each node
                foreach ( var connectTo in node.info.connectTos )
                {
                    GraphNode toNode = graphNodes.Find(x => (x.info.name == connectTo.Key));
                    node.edgeTo.Add(toNode);
                    toNode.nInboundEdge++;

                    toNode.connectFroms[node] = connectTo.Value;
                }

                // Certain primitive depend on other primitive
                if ( node.info.type != "SourceEvent" )
                {
                    List<String> dependees = Primitive.ListExtraDependees(
                                                    node.info.type, node.info.primitiveParameters);
                    if ( dependees != null )
                    {
                        foreach ( String dep in dependees )
                        {
                            GraphNode depNode = graphNodes.Find(x => (x.info.name == dep));
                            depNode.edgeTo.Add(node);
                            node.nInboundEdge++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Topological sort one graph
        /// </summary>
        private bool SortGraph(ref List<GraphNode> graphNodes)
        {
            List<GraphNode> sortedGraph = new List<GraphNode>();
            Queue<GraphNode> processQueue = new Queue<GraphNode>();

            // First put all root nodes into queue
            for ( int i=0; i<graphNodes.Count; i++ )
                if ( graphNodes[i].nInboundEdge == 0 )
                    processQueue.Enqueue(graphNodes[i]);

            // Process queue, add new nodes which has no other inbound edges
            while ( processQueue.Count > 0 )
            {
                GraphNode node = processQueue.Dequeue();
                sortedGraph.Add(node);
                foreach ( GraphNode toNode in node.edgeTo )
                {
                    toNode.nInboundEdge--;
                    if ( toNode.nInboundEdge == 0 )
                        processQueue.Enqueue(toNode);
                }
            }

            // If there is nodes left, that means there is circle
            if ( sortedGraph.Count != graphNodes.Count )
                return false;

            graphNodes = sortedGraph;
            return true;
        }

        /// <summary>
        /// Add one rule into rule engine, create primitives for it.
        /// </summary>
        private bool AddRule(Rule rule)
        {
            // First validate rule
            if ( !rule.Validate(this) )
            {
                errorMessage = rule.errorMessage;
                return false;
            }

            // Add one extra helper node of "SourceEvent" with id "", link to all non-targeted 
            // primitives nodes, to unify primitive processing
            Rule.Node nullSrcEvtNode = null;
            foreach ( Rule.Node node in rule.nodes )
                if ( node.type != "SourceEvent" && !Primitive.Targetable(node.type) )
                {
                    if ( nullSrcEvtNode == null )
                        nullSrcEvtNode = new Rule.Node { type = "SourceEvent", name="" };
                    nullSrcEvtNode.connectTos[node.name] = new Rule.Node.ConnectionInfo();
                }
            if ( nullSrcEvtNode != null )
                rule.nodes.Add(nullSrcEvtNode);

            // Create graph nodes
            List<GraphNode> graph = new List<GraphNode>();
            foreach ( Rule.Node node in rule.nodes )
                graph.Add(new GraphNode{info = node});

            // Link nodes together to form graph
            LinkGraphNodes(graph);

            // Sort graph
            if ( !SortGraph(ref graph) )
            {
                errorMessage = String.Format("rule '{0}' contains circle", rule.name);
                return false;
            }

            // Process nodes, merge to exist rules
            Dictionary<String,IPrimitive> settledPrimitives = new Dictionary<string,IPrimitive>();
            foreach ( GraphNode node in graph )
            {
                if ( node.info.type == "SourceEvent" )
                {
                    if ( node.info.name == "AllEvents" )
                    {
                        if ( _allEventsDispatcher == null )
                            _allEventsDispatcher = new SignalSource(this, this);
                        node.sigSender = _allEventsDispatcher;
                    }
                    else if ( !_sigDispatcher.TryGetValue(node.info.name, out node.sigSender) )
                    {
                        node.sigSender = new SignalSource(this, _sigDispatcher);
                        _sigDispatcher[node.info.name] = node.sigSender;
                    }
                }
                else  // Primitive
                {
                    // Find if we can share exist primitive
                    node.primitive = FindSamePrimitive(node, settledPrimitives);

                    // Create new primitive and connect if no exist one can share
                    if ( node.primitive == null )
                    {
                        node.primitive = Primitive.Create(this, node.info.type);
                        node.primitive.Setup(node.info.primitiveParameters, settledPrimitives);
                        foreach ( var connFrom in node.connectFroms )
                        {
                            GraphNode fromNode = connFrom.Key;
                            if ( connFrom.Value.signalOnNegative )
                            {
                                if ( fromNode.sigSenderOnNegative != null )
                                {
                                    fromNode.sigSenderOnNegative.ConnectTo(
                                                                node.primitive.SignalReceiver,
                                                                connFrom.Value.signalParameter);
                                    if ( fromNode.primitive != null )
                                        fromNode.primitive.DependerCount++;
                                }
                            }
                            else
                            {
                                fromNode.sigSender.ConnectTo(
                                                node.primitive.SignalReceiver,
                                                connFrom.Value.signalParameter);
                                if ( fromNode.primitive != null )
                                    fromNode.primitive.DependerCount++;
                            }
                        }

                        if ( node.primitive is EventGenerator )
                        {
                            string newEventName = (node.primitive as EventGenerator).NewEventName;
                            _eventGenerators[newEventName] = node.primitive;
                        }
                    }

                    node.sigSender = node.primitive.SignalSender;
                    node.sigSenderOnNegative = node.primitive.SignalSenderOnNegative;

                    settledPrimitives[node.info.name] = node.primitive;
                }
            }

            return true;
        }

        /// <summary>
        /// Find exist same primitive to share
        /// 1. Has same primitive type and same parameters
        /// 2. Has same signal sources with same trigger parameter
        /// </summary>
        private IPrimitive FindSamePrimitive(GraphNode node,
                                             Dictionary<String, IPrimitive> settledPrimitives)
        {
            // If there is the same primitive, it must has same signal sources as me, so it must
            // be one target of my first signal source
            var firstConnFrom = node.connectFroms.First();
            List<SignalTarget> candidates;
            if ( firstConnFrom.Value.signalOnNegative )
                candidates = firstConnFrom.Key.sigSenderOnNegative.Targets;
            else
                candidates = firstConnFrom.Key.sigSender.Targets;

            foreach ( var candidate in candidates )
            {
                IPrimitive candidatePm = candidate.Owner as IPrimitive;

                // 1. Has same type and same parameters
                if ( candidatePm.GetType().Name != node.info.type ||
                     !candidatePm.HasSameParameters(node.info.primitiveParameters, 
                                                    settledPrimitives) )
                    continue;

                // 2. Has same signal sources with same trigger parameter
                List<SignalSource> candidateSrcs = candidate.ConnectedSources;
                if ( candidateSrcs.Count != node.connectFroms.Count )
                    continue;

                bool matchedAll = true;
                foreach ( var connFrom in node.connectFroms )
                {
                    // Match one signal source
                    SignalSource connFromSigSrc;
                    if ( connFrom.Value.signalOnNegative )
                        connFromSigSrc = connFrom.Key.sigSenderOnNegative;
                    else
                        connFromSigSrc = connFrom.Key.sigSender;

                    SignalSource candidateSrc = candidateSrcs.Find(
                                                x => Object.ReferenceEquals(x, connFromSigSrc));
                    if ( candidateSrc == null )
                    {
                        matchedAll = false;
                        break;
                    }

                    // Match trigger parameter on the matched signal source
                    Object candidateSigParam = candidateSrc.GetTargetSigParam(candidate);
                    if ( !ObjectEqual(candidateSigParam, connFrom.Value.signalParameter) )
                    {
                        matchedAll = false;
                        break;
                    }
                }
                if ( matchedAll )
                    return candidatePm;
            }

            return null;
        }

        /// <summary>
        /// Determine if two object are of same type and same value. If they are list, also compare
        /// each item in exact order
        /// </summary>
        private bool ObjectEqual(Object obj1, Object obj2)
        {
            if ( obj1.GetType() != obj2.GetType() )
                return false;

            if ( obj1 is List<Object> )
            {
                List<Object> list1 = obj1 as List<Object>;
                List<Object> list2 = obj2 as List<Object>;
                if ( list1.Count != list2.Count )
                    return false;

                for ( int i=0; i<list1.Count; i++ )
                {
                    if ( !ObjectEqual(list1[i], list2[i]) )
                        return false;
                }
                return true;
            }
            else
                return obj1.Equals(obj2);
        }

        // Dictionary which dispath signals, map event to signal source
        private Dictionary<String, SignalSource> _sigDispatcher = 
            new Dictionary<String, SignalSource>();

        // Signal source for rules inspecting all events.
        private SignalSource _allEventsDispatcher = null;

        // Dictionary which dispatch actions, map event to actor
        private Dictionary<String, List<Actor>> _actors = new Dictionary<String, List<Actor>>();

        // Map rule name to the generated event
        private Dictionary<string, string> _ruleEventMap = new Dictionary<string,string>();

        // Save all EventGenerator for rule deleting, map event name to the generator
        private Dictionary<String, IPrimitive> _eventGenerators = 
            new Dictionary<String, IPrimitive>();
    }
}
