//-------------------------------------------------------------------------------------------------
//  KeyedCollectorInOrder.cs
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
    /// Description: Receive signals from multiple sources, save states indexed by key, output
    ///     signal if collected all signals in exact desired order on any key, automatically reset
    ///     states for that key after output.
    /// 
    /// Parameters:
    ///     SourcesCount : Integer. sources count
    ///     Timeouts : List<int>. Timeouts of each signal collected, in milliseconds
    ///         
    /// Signal Parameters:
    ///     Key : Object. Identify the signal collector
    ///     SourceIndex : Integer. Identify the signal source
    ///     "RemoveKey" : String. Remove one key, and all its states
    ///     Cancel : Bool. Optional, revoke the signal received from this source before.
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class KeyedCollectorInOrder : IPrimitive
    {
        //
        // Private variables
        //
        class SignalTracker
        {
            public long expireTime;
            public Object context;
        }
        private Dictionary<Object, SignalTracker[]> _trackers;
        private Dictionary<Object,int> _nextStates;
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

            _trackers = new Dictionary<Object, SignalTracker[]>();
            _nextStates = new Dictionary<object, int>();
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
        public KeyedCollectorInOrder(Engine engine)
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
            int index = -1;
            bool cancel = false;
            Object key = null;
            bool removeKey = false;

            if ( parameter is List<Object> )
            {
                List<Object> param = parameter as List<Object>;

                if ( param.Count > 0 )
                    key = param[0];
                if ( param.Count > 1 )
                {
                    if ( param[1] is int )
                        index = (int)param[1];
                    else if ( (param[1] is String) && (param[1] as String) == "RemoveKey" )
                        removeKey = true;
                    else
                        return;
                }
                if ( param.Count > 2 && param[2] is bool )
                    cancel = (bool)param[2];

                if ( key == null || index < 0 || index >= _params.sourceCount )
                    return;
            }

            Console.WriteLine("\tPrimitive[{0}] triggered, key {1} source {2}{3}{4}",
                GetType().Name, key, index, removeKey ? " RemoveKey" : "", cancel ? " Cancel":"");

            lock ( _lock )
            {
                if ( removeKey )
                {
                    _nextStates.Remove(key);
                    Console.WriteLine("\tPrimitive[{0}] key {1} removed", GetType().Name, key);
                    return;
                }

                int nextState = 0;
                bool stateUpdated = false;

                SignalTracker[] tracker;
                if ( !_trackers.TryGetValue(key, out tracker) )
                {
                    tracker = new SignalTracker[_params.sourceCount];
                    for ( int i=0; i<_params.sourceCount; i++ )
                        tracker[i] = new SignalTracker();
                    _trackers[key] = tracker;
                    _nextStates[key] = 0;
                }
                nextState = _nextStates[key];

                long currentTimeTicks = 0;

                // Clean up expired state
                if ( _params.stateTimeouts != null )
                {
                    currentTimeTicks = DateTime.Now.Ticks;
                    for ( int i=0; i<nextState; i++ )
                    {
                        if ( tracker[i].expireTime < currentTimeTicks )
                        {
                            Console.WriteLine("\tPrimitive[{0}] key {1} state back to {2} because expired",
                                              GetType().Name, key, i);
                            nextState = i;
                            stateUpdated = true;
                            break;
                        }
                    }
                }

                // Process signal cancelation
                if ( cancel )
                {
                    if ( index < nextState )
                    {
                        Console.WriteLine("\tPrimitive[{0}] key {1} state back to {2} because canceled",
                                          GetType().Name, key, index);
                        nextState = index;
                        stateUpdated = true;
                    }
                }
                else
                {
                    // Only set new state if input is the expected next state
                    if ( index == nextState )
                    {
                        if ( _params.stateTimeouts != null )
                            tracker[index].expireTime = 
                                currentTimeTicks + _params.stateTimeouts[index] * 10000;
                        tracker[index].context = context;

                        nextState++;
                        stateUpdated = true;

                        if ( nextState == _params.sourceCount )
                        {
                            // Combine all context into one new context
                            List<object> newContext = new List<object>();
                            for ( int i=0; i<_params.sourceCount; i++ )
                                newContext.Add(_trackers[key][i].context);

                            Console.WriteLine("\tPrimitive[{0}] key {1} collected all, ready to fire",
                                              GetType().Name, key);

                            SignalSender.Trigger(newContext);

                            // Reset collector
                            _nextStates.Remove(key);
                            _trackers.Remove(key);
                            stateUpdated = false;
                        }
                    }
                }

                if ( stateUpdated )
                    _nextStates[key] = nextState;
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

            parsed.sourceCount = Convert.ToInt32(param);

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
