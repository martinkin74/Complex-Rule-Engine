//-------------------------------------------------------------------------------------------------
//  EventGenerator.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: On input signal, generate designed event and call rule engine on it.
    /// 
    /// Parameters:
    ///     NewEventName : The Id of event to be generated
    ///     Properties : Properties dictionary for this new event
    /// 
    /// Signal Parameters: None
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class EventGenerator : IPrimitive
    {
        //
        // Private variables
        //
        class Parameters
        {
            public String eventName;
            public Dictionary<int, Object> properties;
        }
        private Parameters _params;
        private String _errorMessage;
        private Engine _engine;

        //#########################################################################################
        //
        // Implement interface IPrimitive
        //
        //#########################################################################################
        public String ErrorMessage { get { return _errorMessage; } }
        public SignalSource SignalSender { get; private set; }
        public SignalSource SignalSenderOnNegative { get; private set; }
        public SignalTarget SignalReceiver { get; private set; }
        public List<IPrimitive> ExtraDependees { get; private set; }
        public int DependerCount { get; set; }

        public bool Setup(Dictionary<String, Object> parameters,
                          Dictionary<String, IPrimitive> primitivesDict)
        {
            if ( !ParseParameters(_engine, parameters, primitivesDict, out _params, 
                                  out _errorMessage) )
                return false;

            return true;
        }

        // Check if current primitive has same parameters as input
        public bool HasSameParameters(Dictionary<String, Object> parameters,
                                      Dictionary<String, IPrimitive> primitivesDict)
        {
            // Event generator are all unique and cannot share
            return false;
        }

        //#########################################################################################
        //
        // Implement static functions optionally required by Primitive
        //
        //#########################################################################################
        /// <summary>
        /// Validate parameters
        /// </summary>
        public static bool ValidateParameters(Engine engine,
                                              Dictionary<String, Object> parameters,
                                              Dictionary<String, IPrimitive> knownPrimitives,
                                              out String errorMessage)
        {
            Parameters param;
            return ParseParameters(engine, parameters, knownPrimitives, out param, 
                                   out errorMessage);
        }

        //#########################################################################################
        //
        //  Class implemention
        //
        //#########################################################################################
        /// <summary>
        /// Constructor
        /// </summary>
        public EventGenerator(Engine engine)
        {
            _engine = engine;
            SignalReceiver = new SignalTarget(engine, this);
            SignalReceiver.OnTrigger += OnTrigger;
        }

        public string NewEventName { get { return _params.eventName;} }

        /// <summary>
        /// Callback from SignalReceiver, on signal triggered. 
        /// Generate new event, fill properties and feed to engine
        /// </summary>
        private void OnTrigger(Object parameter, Object context)
        {
            Console.WriteLine("\tPrimitive[{0}] Triggered", GetType().Name);

            IEvent genEvt = _engine.MetaEvent.CreateInstance(_params.eventName);
            if ( _params.properties != null )
            {
                foreach ( var prop in _params.properties )
                {
                    if ( prop.Value is Macro )
                        genEvt.SetProperty(prop.Key, (prop.Value as Macro).Run(context));
                    else
                        genEvt.SetProperty(prop.Key, prop.Value);
                }
            }
            _engine.ProcessEvent(genEvt);
        }

        private static bool ParseParameters(Engine engine,
                                            Dictionary<String, Object> parameters,
                                            Dictionary<String, IPrimitive> primitivesDict,
                                            out Parameters parsed, out String errorMessage)
        {
            parsed = new Parameters();
            errorMessage = null;
            Object param;

            if ( !Primitive.ValidateParam(parameters, "NewEventName", typeof(String), out param,
                                          out errorMessage) )
                return false;

            parsed.eventName = param as String;

            if ( parameters.TryGetValue("Properties", out param) )
            {
                if ( !(param is Dictionary<String,Object>) )
                {
                    errorMessage = "Parameter 'Properties' is not Dictionary<String,Object>";
                    return false;
                }
                parsed.properties = new Dictionary<int, object>();

                // Get property IDs and Validate macros inside properties
                foreach ( String propName in (param as Dictionary<String, Object>).Keys.ToList() )
                {
                    int propId = engine.MetaEvent.GetPropertyId(propName);
                    if ( propId < 0 )
                    {
                        errorMessage = String.Format(
                            "Parameter 'Properties' contains unknown property '{0}'", propName);
                        return false;
                    }

                    Object propValue = (param as Dictionary<String, Object>)[propName];
                    if ( propValue is String )
                    {
                        String strValue = propValue as String;
                        if ( strValue.StartsWith("#MACRO#") )
                        {
                            Macro macro = new Macro(engine);
                            if ( !macro.Parse(strValue.Substring("#MACRO#".Length)) )
                            {
                                errorMessage = String.Format(
                                    "Parameter 'Properties' contains invalid macro '{0}' {1}",
                                    strValue, macro.ErrorMessage);
                                return false;
                            }
                            parsed.properties[propId] = macro;
                            continue;
                        }
                    }

                    parsed.properties[propId] = propValue;
                }
            }

            return true;
        }
    }
}
