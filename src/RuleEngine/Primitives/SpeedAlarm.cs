using System;
using System.Collections.Generic;
using System.Threading;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: Output signal when input signal exceed certain speed.
    /// 
    /// Parameters:
    ///     MaximumSpeed : The number on which we output within one period
    ///     Period : The time unit used to represent speed, in seconds
    /// 
    /// Signal Parameters:
    ///     Command : Int. 0 reset count to 0, others, the count to increase
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class SpeedAlarm : IPrimitive
    {
        class Parameters {
            public int maximumSpeed;
            public int period;
        }

        class TimedData {
            public int timeStamp;
            public int data;
        }
        //
        // Private variables
        //
        private Queue<TimedData> _timedCounts;
        private int _totalCount;
        private object _lock;
        private Parameters _params;
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

            _timedCounts = new Queue<TimedData>();
            _lock = new object();
            _totalCount = 0;
            return true;
        }

        // Check if current primitive has same parameters as input
        public bool HasSameParameters(Dictionary<String, Object> parameters,
                                      Dictionary<String, IPrimitive> primitivesDict)
        {
            // Load input parameters
            Parameters param;
            if ( !ParseParameters(parameters, primitivesDict, out param, out _errorMessage) )
                return false;

            // Compare parameters
            return ( param.maximumSpeed == _params.maximumSpeed && 
                     param.period == _params.period );
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
        public SpeedAlarm(Engine engine)
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
            if ( parameter == null || !(parameter is int) )
                return;

            int newCount = (int)parameter;
            bool trigger = false;
            lock ( _lock )
            {
                if ( newCount == 0 )
                {
                    _totalCount = 0;
                    _timedCounts.Clear();
                }
                else
                {
                    _totalCount += newCount;
                    int currentTime = Environment.TickCount;
                    _timedCounts.Enqueue(new TimedData { timeStamp=currentTime, data = newCount });
                    if ( _totalCount > _params.maximumSpeed )
                    {
                        int expireTime = currentTime - _params.period;
                        while ( _timedCounts.Count > 0 )
                        {
                            TimedData item = _timedCounts.Peek();
                            if ( item.timeStamp - expireTime > 0 )
                                break;
                            _totalCount -= item.data;
                            _timedCounts.Dequeue();
                        }
                        if ( _totalCount > _params.maximumSpeed )
                        {
                            _totalCount = 0;
                            _timedCounts.Clear();
                            trigger = true;
                        }
                    }
                }
            }
            if ( trigger )
                SignalSender.Trigger(context);
        }

        /// <summary>
        /// Parse and validate primitive parameters
        /// </summary>
        private static bool ParseParameters(Dictionary<String, Object> parameters,
                                            Dictionary<String, IPrimitive> primitivesDict,
                                            out Parameters parsed, out String errorMessage)
        {
            errorMessage = null;
            parsed = new Parameters();
            Object param;

            if ( !Primitive.ValidateParam(parameters, "MaximumSpeed", typeof(int),
                                          out param, out errorMessage) )
                return false;
            parsed.maximumSpeed = (int)param;

            if ( !Primitive.ValidateParam(parameters, "Period", typeof(int),
                                          out param, out errorMessage) )
                return false;
            parsed.period = (int)param * 1000;

            return true;
        }
    }
}
