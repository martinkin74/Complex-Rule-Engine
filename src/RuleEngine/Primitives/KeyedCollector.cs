//-------------------------------------------------------------------------------------------------
//  KeyedCollector.cs
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
    ///     signal if collected all signals on any key, automatically reset states for that key 
    ///     after output.
    /// 
    /// Parameters:
    ///     SourceCount : Integer. sources count
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
    internal sealed class KeyedCollector : IPrimitive
    {
        //
        // Private variables
        //
        class SignalTracker {
            public bool triggered = false;
            public long expireTime;
            public Object context;
        }
        private Dictionary<Object,SignalTracker[]> _trackers;
        private Object _trackerLock = new Object();

        class Parameters
        {
            public int sourceCount;
            public List<int> trackerTimeouts = null;
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

            _trackers = new Dictionary<Object,SignalTracker[]>();
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

            if ( param.trackerTimeouts == null )
                return (_params.trackerTimeouts == null);
            else
            {
                if ( _params.trackerTimeouts == null )
                    return false;
                return param.trackerTimeouts.SequenceEqual(_params.trackerTimeouts);
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
        public KeyedCollector(Engine engine)
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
            bool removeKey = false;
            bool cancel = false;
            Object key = null;

            if ( !(parameter is List<Object>) )
                return;
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

            Console.WriteLine("\tPrimitive[{0}] triggered, key {1} source {2}{3}{4}", GetType().Name,
                key, index, removeKey ? " RemoveKey" : "", cancel ? " Cancel":"");

            lock ( _trackerLock )
            {
                if ( removeKey )
                {
                    _trackers.Remove(key);
                    Console.WriteLine("\tPrimitive[{0}] key {1} removed", GetType().Name, key);
                    return;
                }

                SignalTracker[] tracker;
                if ( !_trackers.TryGetValue(key, out tracker) )
                {
                    tracker = new SignalTracker[_params.sourceCount];
                    for ( int i=0; i<_params.sourceCount; i++ )
                        tracker[i] = new SignalTracker();
                    _trackers[key] = tracker;
                }

                long currentTimeTicks = 0;
                if ( _params.trackerTimeouts != null )
                    currentTimeTicks = DateTime.Now.Ticks;

                // Set new state
                tracker[index].triggered = !cancel;
                tracker[index].context = context;
                if ( !cancel && _params.trackerTimeouts != null )
                    tracker[index].expireTime = currentTimeTicks + 
                                                _params.trackerTimeouts[index] * 10000;

                // Count total signals, reset expired states at same time
                int count = 0;
                for ( int i=0; i<_params.sourceCount; i++ )
                {
                    if ( tracker[i].triggered )
                    {
                        if ( _params.trackerTimeouts != null && 
                             tracker[i].expireTime <= currentTimeTicks )
                        {
                            Console.WriteLine("\tPrimitive[{0}] key {1} source {2} expired", 
                                              GetType().Name, key, i);
                            tracker[i].triggered = false;
                        }
                        else
                            count++;
                    }
                }

                // Send signal if all triggered, output the key
                if ( count == _params.sourceCount )
                {
                    // Combine all context into one new context
                    List<object> newContext = new List<object>();
                    for ( int i=0; i<_params.sourceCount; i++ )
                        newContext.Add(_trackers[key][i].context);

                    Console.WriteLine("\tPrimitive[{0}] key {1} collected all, ready to fire",
                                      GetType().Name, key);

                    SignalSender.Trigger(newContext);

                    // Reset collector
                    _trackers.Remove(key);
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
                parsed.trackerTimeouts = new List<int>();
                foreach ( Object obj in (param as List<Object>) )
                {
                    if ( !(obj is int) )
                    {
                        errorMessage = "Parameter 'Timeouts' array contains non-integer value";
                        return false;
                    }
                    parsed.trackerTimeouts.Add((int)obj);
                }
            }

            return true;
        }
    }
}
