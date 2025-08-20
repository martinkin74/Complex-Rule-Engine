//-------------------------------------------------------------------------------------------------
//  JsonRulesParser.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using RuleEngine.Primitives;

namespace RuleEngine
{
    // Sample JSON rules
    //{
    //    "Rules" :
    //    [
    //        {
    //            "GenerateEvent" : "E",
    //            "SourceEvents" :
    //            [
    //                {
    //                    "EventName" : "A",
    //                    "ConnectTo" : { "Collector_1" : { "SignalParameter":0 } }
    //                },
    //                {
    //                    "EventName" : "B",
    //                    "ConnectTo" : { "Counter_1" : {} }
    //                },
    //                {
    //                    "EventName" : "E",
    //                    "ConnectTo" : { "Counter_1" : { "SignalParameter" : "Reset" } }
    //                } 
    //            ],
    //            "Primitives":
    //            [
    //                {
    //                    "Type" : "Collector",
    //                    "Name" : "Collector_1",
    //                    "Parameters" : { "SlotsCount" : 2 },
    //                    "ConnectTo" : { "EventGenerator_1" : {} }
    //                },
    //                {
    //                    "Type" : "CountdownCounter",
    //                    "Name" : "Counter_1",
    //                    "ConnectTo" : {
    //                        "Collector_1" : {
    //                            "TriggerOnNegative" : true,
    //                            "SignalParameter" : 1
    //                        }
    //                    }
    //                },
    //                {
    //                    "Type" : "EventGenerator",
    //                    "Name" : "EventGenerator_1",
    //                    "Parameters" : { "NewEventName" : "E" }
    //                }
    //            ],
    //        }
    //    ]
    //}
    /// <summary>
    /// Parse JSON rules into C# structure, validate JSON format
    /// </summary>
    public class JsonRulesParser
    {
        // Present error message when parse failed
        public String errorMessage { get; private set; }

        private String _currentRuleName;

        /// <summary>
        /// Parse JSON rules, validate and generate C# rules
        /// </summary>
        public bool Parse(String jsonRules, out List<Rule> rules)
        {
            rules = null;

            JToken rootToken;

            // Load JSON string
            try
            {
                rootToken = JToken.Parse(jsonRules);
            }
            catch ( Exception e )
            {
                errorMessage = e.Message;
                return false;
            }

            if ( rootToken.Type != JTokenType.Object )
            {
                errorMessage = "Root token is not JSON object";
                return false;
            }

            JToken rulesToken = ((JObject)rootToken)["Rules"];
            if ( rulesToken == null || rulesToken.Type != JTokenType.Array)
            {
                errorMessage = "'Rules' array was not found under root token";
                return false;
            }

            // Parse each rule
            rules = new List<Rule>();

            int iRule = 0;
            foreach ( JToken ruleToken in rulesToken.Children() )
            {
                if ( ruleToken.Type != JTokenType.Object )
                {
                    errorMessage = String.Format("Rules[{0}] is not a JSON object", iRule);
                    return false;
                }

                Rule rule = new Rule();

                // Get rule name
                JToken token = ((JObject)ruleToken)["RuleName"];
                if ( token == null )
                {
                    errorMessage = 
                        String.Format("Rules[{0}] has no 'RuleName' property", iRule);
                    return false;
                }
                rule.name = token.ToString();
                _currentRuleName = rule.name;

                // Parse "SourceEvents" section
                token = ((JObject)ruleToken)["SourceEvents"];
                // Some rule might has no "SourceEvents" section, if all based on timer.
                if ( token != null && !ParseSourceEvents(token, rule) )
                    return false;

                // Parse "Primitives" section
                token = ((JObject)ruleToken)["Primitives"];
                if ( token == null )
                {
                    errorMessage = String.Format("Rules[{0}] has no 'Primitives' property", iRule);
                    return false;
                }

                if ( !ParsePrimitives(token, rule) )
                    return false;

                rules.Add(rule);
                iRule++;
            }

            return true;
        }

        /// <summary>
        /// Parse "SourceEvents" array
        /// </summary>
        private bool ParseSourceEvents(JToken srcEvtsToken, Rule rule)
        {
            if ( srcEvtsToken.Type != JTokenType.Array )
            {
                errorMessage = String.Format("'SourceEvents' of rule '{0}' is not array", rule.name);
                return false;
            }

            // Parse each SourceEvent
            int i = 0;
            foreach ( JToken evtToken in srcEvtsToken.Children() )
            {
                if ( evtToken.Type != JTokenType.Object )
                {
                    errorMessage = String.Format(
                        "SourceEvents[{0}] of rule '{1}' is not JSON object", i, rule.name);
                    return false;
                }

                JToken prop = ((JObject)evtToken)["EventName"];
                if ( prop == null )
                {
                    errorMessage = String.Format(
                        "SourceEvents[{0}] of rule '{1}' has no 'EventName' defined", i, rule.name);
                    return false;
                }

                Rule.Node node = new Rule.Node();
                node.type = "SourceEvent";
                node.name = prop.ToString();

                prop = ((JObject)evtToken)["ConnectTo"];
                if ( prop == null )
                {
                    errorMessage = String.Format(
                        "SourceEvents[{0}] of rule '{1}' has no 'ConnectTo' defined", i, rule.name);
                    return false;
                }

                if ( !ParseConnections(prop, node) )
                    return false;

                rule.nodes.Add(node);

                i++;
            }

            return true;
        }

        /// <summary>
        /// Parse "Primitives" array
        /// </summary>
        private bool ParsePrimitives(JToken pmsToken, Rule rule)
        {
            if ( pmsToken.Type != JTokenType.Array )
            {
                errorMessage = String.Format("'Primitives' of rule '{0}' is not array", rule.name);
                return false;
            }

            int i = 0;
            foreach ( JToken pmToken in pmsToken.Children() )
            {
                if ( pmToken.Type != JTokenType.Object )
                {
                    errorMessage = String.Format(
                        "Primitives[{0}] of rule '{1}' is not JSON object", i, rule.name);
                    return false;
                }

                JToken propToken = ((JObject)pmToken)["Type"];
                if ( propToken == null )
                {
                    errorMessage = String.Format(
                        "Primitives[{0}] of rule '{1}' has no 'Type' defined", i, rule.name);
                    return false;
                }
                Rule.Node node = new Rule.Node();
                node.type = propToken.ToString();

                propToken = ((JObject)pmToken)["Name"];
                if ( propToken == null )
                {
                    errorMessage = String.Format(
                        "Primitives[{0}] of rule '{1}' has no 'Name' defined", i, rule.name);
                    return false;
                }
                node.name = propToken.ToString();

                propToken = ((JObject)pmToken)["ConnectTo"];
                if ( propToken != null && !ParseConnections(propToken, node) )
                    return false;

                node.primitiveParameters = new Dictionary<string, object>();
                propToken = ((JObject)pmToken)["Parameters"];
                if ( propToken != null )
                {
                    if ( propToken.Type != JTokenType.Object )
                    {
                        errorMessage = String.Format(
                            "Primitives[{0}].Parameters of rule '{1}' is not JSON object", 
                            i, rule.name);
                        return false;
                    }
                    foreach ( JProperty prop in ((JObject)propToken).Properties() )
                        node.primitiveParameters[prop.Name] = ParseComplexType(prop.Value);
                }

                rule.nodes.Add(node);
                i++;
            }

            return true;
        }

        /// <summary>
        /// Parse "ConnectTo" array for either "SourceEvent" or "Primitive"
        /// </summary>
        private bool ParseConnections(JToken connsToken, Rule.Node node)
        {
            if ( connsToken.Type != JTokenType.Object )
            {
                errorMessage = String.Format("In rule '{0}', '{1}.{2}.ConnectTo' is not object", 
                                             _currentRuleName, node.type, node.name);
                return false;
            }

            foreach ( JProperty conn in ((JObject)connsToken).Properties() )
            {
                if ( conn.Value.Type != JTokenType.Object )
                {
                    errorMessage = String.Format(
                        "In rule '{0}', '{1}.{2}.ConnectTo[{3}]' is not JSON object",
                        _currentRuleName, node.type, node.name, conn.Name);
                    return false;
                }

                Rule.Node.ConnectionInfo connInfo = new Rule.Node.ConnectionInfo();
                JObject connAttributes = (JObject)conn.Value;
                connInfo.signalOnNegative = false;

                JToken attribToken = connAttributes["TriggerOnNegative"];
                if ( attribToken != null )
                {
                    try {
                        connInfo.signalOnNegative = attribToken.Value<bool>();
                    } catch {
                        errorMessage = String.Format(
                            "In rule '{0}', '{1}.{2}.ConnectTo[{3}].TriggerOnNegative' has invalid value",
                            _currentRuleName, node.type, node.name, conn.Name);
                        return false;
                    }
                }

                attribToken = connAttributes["SignalParameter"];
                if ( attribToken != null )
                    connInfo.signalParameter = ParseComplexType(attribToken);

                node.connectTos[conn.Name] = connInfo;
            }

            return true;
        }

        private Object ParseComplexType(JToken token)
        {
            if ( token.Type == JTokenType.Array )
            {
                List<Object> result = new List<object>();
                foreach ( var child in ((JArray)token).Children() )
                    result.Add(ParseComplexType(child));
                return result;
            }
            else if ( token.Type == JTokenType.Object )
            {
                Dictionary<String, Object> result = new Dictionary<string, object>();
                foreach ( JProperty prop in ((JObject)token).Properties() )
                    result[prop.Name] = ParseComplexType(prop.Value);
                return result;
            }
            else if ( token.Type == JTokenType.Integer )
                return token.ToObject<int>();
            else
                return token.ToObject<Object>();
        }
    }
}
