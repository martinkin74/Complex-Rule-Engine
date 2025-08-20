using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: Receive signals from multiple sources, output signal after received from all
    ///     sources, automatically reset after output.
    /// 
    /// Parameters:
    ///     SourcesCount : Integer. sources count
    ///     Timeouts : List<int>. Timeouts of each signal collected, in milliseconds
    ///         
    /// Signal Parameters:
    ///     SourceIndex : Integer. Identify the signal source
    ///     Cancel : Bool. Optional, revoke the signal received from this source before.
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class Collector : IPrimitive
    {
        //
        // Private variables
        //
        class SignalTracker {
            public bool triggered = false;
            public long expireTime;
            public Object context;
        }
        private SignalTracker[] _tracker;
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
        public Collector(Engine engine)
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
            // Get parameters
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

            lock ( _trackerLock )
            {
                long currentTimeTicks = 0;
                if ( _params.trackerTimeouts != null )
                    currentTimeTicks = DateTime.Now.Ticks;

                // Set new state
                _tracker[index].triggered = !cancel;
                _tracker[index].context = context;
                if ( !cancel && _params.trackerTimeouts != null )
                    _tracker[index].expireTime = currentTimeTicks + 
                                                 _params.trackerTimeouts[index] * 10000;

                // Count total signals, reset expired states at same time
                int count = 0;
                for ( int i=0; i<_params.sourceCount; i++ )
                {
                    if ( _tracker[i].triggered )
                    {
                        if ( _params.trackerTimeouts != null && 
                             _tracker[i].expireTime <= currentTimeTicks )
                        {
                            Console.WriteLine("\tPrimitive[{0}] source {1} expired", GetType().Name,
                                              i);
                            _tracker[i].triggered = false;
                        }
                        else
                            count++;
                    }
                }

                // Send signal if all triggered
                if ( count == _params.sourceCount )
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
                    for ( int i=0; i<_params.sourceCount; i++ )
                        _tracker[i].triggered = false;
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
