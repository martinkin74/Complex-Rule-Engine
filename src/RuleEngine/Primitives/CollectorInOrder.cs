//-------------------------------------------------------------------------------------------------
//  CollectorInOrder.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: Receive signals from multiple sources, output signal after received from all
    ///     sources, in exact desired order. automatically reset after output.
    /// 
    /// Parameters:
    ///     SourceCount : Integer. sources count
    ///     Timeouts : List<int>. Timeouts of each signal collected, in milliseconds
    ///         
    /// Signal Parameters:
    ///     SourceIndex : Integer. Identify the signal source
    ///     Cancel : Bool. Optional, revoke the signal received from this source before.
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class CollectorInOrder : IPrimitive
    {
        //
        // Private variables
        //
        class SignalTracker
        {
            public long expireTime;
            public Object context;
        }
        private SignalTracker[] _tracker;
        private int _nextState = 0;
        private Object _lock = new Object();

        class Parameters
        {
            public int sourceCount;
            public List<int> stateTimeouts = null;
        }
        Parameters _params;

        private String _errorMessage;

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
            if ( !ParseParameters(parameters, primitivesDict, out _params, out _errorMessage) )
                return false;

            _tracker = new SignalTracker[_params.sourceCount];
            for ( int i=0; i<_params.sourceCount; i++ )
                _tracker[i] = new SignalTracker();

            return true;
        }

        // Check if current primitive has same parameters as input
        public bool HasSameParameters(Dictionary<String, Object> parameters,
                                      Dictionary<String, IPrimitive> primitivesDict)
        {
            Parameters param;
            if ( !ParseParameters(parameters, primitivesDict, out param, out _errorMessage) )
                return false;

            if ( param.sourceCount != _params.sourceCount )
                return false;

            if ( param.stateTimeouts == null )
                return (_params.stateTimeouts == null);
            else
            {
                if ( _params.stateTimeouts == null )
                    return false;
                return param.stateTimeouts.SequenceEqual(_params.stateTimeouts);
            }
        }

        //#########################################################################################
        //
        // Implement static functions optionally required by Primitive
        //
        //#########################################################################################
        public static bool ValidateParameters(Engine engine,
                                              Dictionary<String, Object> parameters,
                                              Dictionary<String, IPrimitive> knownPrimitives,
                                              out String errorMessage)
        {
            Parameters param;

            return ParseParameters(parameters, knownPrimitives, out param, out errorMessage);
        }

        //#########################################################################################
        //
        //  Class implemention
        //
        //#########################################################################################
        /// <summary>
        /// Constructor
        /// </summary>
        public CollectorInOrder(Engine engine)
        {
            SignalReceiver = new SignalTarget(engine, this);
            SignalReceiver.OnTrigger += OnTrigger;

            SignalSender = new SignalSource(engine, this);
        }

        /// <summary>
        /// Callback from SignalReceiver, on signal triggered
        /// </summary>
        private void OnTrigger(Object parameter, Object context)
        {
            // Get signal parameters
            int index = -1;
            bool cancel = false;
            if ( parameter is List<Object> )
            {
                List<Object> param = parameter as List<Object>;
                if ( param.Count > 0 && param[0] is int )
                    index = (int)param[0];
                if ( param.Count > 1 && param[1] is bool )
                    cancel = (bool)param[1];
            }
            else if ( parameter is int )
                index = (int)parameter;
            else
                return;

            if ( index < 0 || index >= _params.sourceCount )
                return;

            Console.WriteLine("\tPrimitive[{0}] triggered, source {1} {2}", GetType().Name, index,
                              cancel ? "Cancel" : "");

            lock ( _lock )
            {
                long currentTimeTicks = 0;

                // Clean up expired state
                if ( _params.stateTimeouts != null )
                {
                    currentTimeTicks = DateTime.Now.Ticks;
                    for ( int i=0; i<_nextState; i++ )
                    {
                        if ( _tracker[i].expireTime < currentTimeTicks )
                        {
                            Console.WriteLine("\tPrimitive[{0}] state back to {1} because expired",
                                              GetType().Name, i);
                            _nextState = i;
                            break;
                        }
                    }
                }

                // Process signal cancelation
                if ( cancel )
                {
                    if ( index < _nextState )
                    {
                        Console.WriteLine("\tPrimitive[{0}] state back to {1} because canceled",
                                          GetType().Name, index);
                        _nextState = index;
                    }
                }
                else
                {
                    // Only set new state if input is the expected next state
                    if ( index == _nextState )
                    {
                        if ( _params.stateTimeouts != null )
                            _tracker[index].expireTime = 
                                currentTimeTicks + _params.stateTimeouts[index] * 10000;

                        _tracker[index].context = context;

                        _nextState++;
                        if ( _nextState == _params.sourceCount )
                        {
                            // Combine all context into one new context
                            List<object> newContext = new List<object>();
                            for ( int i=0; i<_params.sourceCount; i++ )
                            {
                                newContext.Add(_tracker[i].context);
                                _tracker[i].context = null;
                            }

                            Console.WriteLine("\tPrimitive[{0}] collected all, ready to fire",
                                              GetType().Name);

                            // Output signal
                            SignalSender.Trigger(newContext);

                            // Reset collector
                            _nextState = 0;
                        }
                    }
                }
            }
        }

        private static bool ParseParameters(Dictionary<String, Object> parameters,
                                            Dictionary<String, IPrimitive> primitivesDict,
                                            out Parameters parsed, out String errorMessage)
        {
            errorMessage = null;
            parsed = new Parameters();
            Object param;

            if ( !Primitive.ValidateParam(parameters, "SourceCount", typeof(int), out param,
                                          out errorMessage) )
                return false;

            parsed.sourceCount = (int)param;

            if ( parameters.TryGetValue("Timeouts", out param) )
            {
                if ( !(param is List<Object>) )
                {
                    errorMessage = "Parameter 'Timeouts' is not array";
                    return false;
                }
                parsed.stateTimeouts = new List<int>();
                foreach ( Object obj in (param as List<Object>) )
                {
                    if ( !(obj is int) )
                    {
                        errorMessage = "Parameter 'Timeouts' array contains non-integer value";
                        return false;
                    }
                    parsed.stateTimeouts.Add((int)obj);
                }
            }

            return true;
        }
    }
}
