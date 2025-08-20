using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RuleEngine.Primitives;

namespace RuleEngine
{
    /// <summary>
    /// Represent one rule in C# structure
    /// </summary>
    public class Rule
    {
        /// <summary>
        /// Represent one "SourceEvent" or "Primitive" in rule
        /// </summary>
        public class Node
        {
            /// <summary>
            /// Describe one connection between nodes
            /// </summary>
            public class ConnectionInfo
            {
                public bool signalOnNegative;  // Trigger signal on negative condition
                public Object signalParameter; // Parameter when trigger signal
            }

            // Primitive type or "SourceEvent"
            public String type;

            // Primitive.Name or SourceEvent.EventName
            public String name;

            // Parameters to create primitive
            public Dictionary<String, Object> primitiveParameters;

            // Connections, map target primitive name to connection information
            public Dictionary<String, ConnectionInfo> connectTos = 
                            new Dictionary<String, ConnectionInfo>();
        }

        // Identify this rule
        public String name;

        // List of rule nodes
        public List<Node> nodes = new List<Node>();

        // Save error message for Validate
        public String errorMessage { get; private set; }

        /// <summary>
        /// Validate rule
        /// 1. In a rule scope, every node name should be unique
        /// 2. Every "ConnectTo" must target to defined node, should not connect to itself
        /// 3. If "ConnectTo" using macro, the macro must be valid
        /// 4. Every primitive type used is defined in rule engine
        /// 5. Every primitive has correct parameters
        /// 6. Except "NonTargeted" primitive, every primitive must be targeted.
        /// </summary>
        public bool Validate(Engine engine)
        {
            // Used by #5 when checking parameters, while primitive might need check dependencies
            Dictionary<String, IPrimitive> knownPrimitives = new Dictionary<string, IPrimitive>();
            // Used by #6. First include all primitives, then remove one after determine it is
            // targeted, the remain are untargeted.
            Dictionary<String, int> unTargetedPms = new Dictionary<string, int>();

            foreach ( Node node in nodes )
            {
                if ( node.type != "SourceEvent" )
                {
                    knownPrimitives[node.name] = null;
                    if ( Primitive.Targetable(node.type) )
                        unTargetedPms[node.name] = 1;
                }
            }

            for ( int iNode=0; iNode<nodes.Count; iNode++ )
            {
                // 1: node name should be unique
                for ( int iSearch=iNode+1; iSearch<nodes.Count; iSearch++ )
                {
                    if ( nodes[iSearch].name == nodes[iNode].name )
                    {
                        errorMessage = String.Format("rule '{0}' has duplicate node name '{1}'",
                                                     name, nodes[iNode].name);
                        return false;
                    }
                }

                // 2. Every "ConnectTo" must target to defined node, should not connect to itself
                foreach ( var connectTo in nodes[iNode].connectTos )
                {
                    // Should not connectTo itself
                    if ( connectTo.Key == nodes[iNode].name )
                    {
                        errorMessage = String.Format("rule '{0}' node '{1}.{2}' connect to itself",
                                                     name, nodes[iNode].type, nodes[iNode].name);
                        return false;
                    }

                    // Connection target must exist
                    int iTarget = nodes.FindIndex(x => (x.name == connectTo.Key));
                    if ( iTarget < 0 )
                    {
                        errorMessage = String.Format(
                            "rule '{0}' node '{1}.{2}' connect to non-exist node '{3}'",
                            name, nodes[iNode].type, nodes[iNode].name, connectTo.Key);
                        return false;
                    }

                    // 3. If "ConnectTo" using macro, the macro must be valid
                    if ( connectTo.Value.signalParameter != null )
                    {
                        Macro macro = new Macro(engine);
                        bool valid = true;
                        if ( connectTo.Value.signalParameter is String )
                        {
                            String paramStr = connectTo.Value.signalParameter as String;
                            if ( paramStr.StartsWith("#MACRO#") &&
                                 !macro.Parse(paramStr.Substring("#MACRO#".Length)) )
                                valid = false;
                        }
                        else if ( connectTo.Value.signalParameter is List<Object> )
                        {
                            foreach ( Object param in (connectTo.Value.signalParameter as List<Object>) )
                            {
                                if ( param is String )
                                {
                                    String paramStr = param as String;
                                    if ( paramStr.StartsWith("#MACRO#") &&
                                         !macro.Parse(paramStr.Substring("#MACRO#".Length)) )
                                    {
                                        valid = false;
                                        break;
                                    }
                                }
                            }
                        }
                        if ( !valid )
                        {
                            errorMessage = String.Format(
                                "rule '{0}' node '{1}.{2}.ConnectTo' contains invalid macro",
                                name, nodes[iNode].type, nodes[iNode].name);
                            return false;
                        }
                    }
                    // Prepare for #6
                    unTargetedPms.Remove(connectTo.Key);
                }

                // 4. Every primitive type used is defined in rule engine
                if ( nodes[iNode].type != "SourceEvent" )
                {
                    if ( !Primitive.IsPrimitiveDefined(nodes[iNode].type) )
                    {
                        errorMessage = String.Format(
                            "Primitive '{0}' used in rule '{1}' is not available in engine",
                            nodes[iNode].type, name);
                        return false;
                    }

                    // 5. Every primitive has correct parameters
                    String pmParamErrMsg;
                    if ( !Primitive.ValidateParameters(engine, 
                                                       nodes[iNode].type, 
                                                       nodes[iNode].primitiveParameters,
                                                       knownPrimitives,
                                                       out pmParamErrMsg) )
                    {
                        errorMessage = String.Format(
                            "Primitive '{0}' of rule '{1}' has invalid parameter. {2}",
                            nodes[iNode].name, name, pmParamErrMsg);
                        return false;
                    }
                }
            }

            // 6. Except "NonTargeted" primitive, every primitive must be targeted.
            if ( unTargetedPms.Count > 0 )
            {
                errorMessage = String.Format("Found untargeted primitives in rule '{0}': ", name);
                foreach ( var kv in unTargetedPms )
                    errorMessage += "'" + kv.Key + "' ";
                return false;
            }

            return true;
        }
    }
}
