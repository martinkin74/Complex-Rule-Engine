using System;
using System.Collections.Generic;
using System.Threading;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: Count on each input signal, continue forever
    /// 
    /// Parameters: None
    /// 
    /// Signal Parameters:
    ///     Command : "Increase" increase count
    ///               "Decrease" decrease count
    ///               "Reset" reset count to 0
    ///               
    /// ICheckable : Yes
    /// Dependencies : None
    /// </summary>
    internal sealed class BasicCounter : IPrimitive, ICheckable
    {
        //
        // Private variables
        //
        private int _count = 0;
        private String _errorMessage = null;

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
            return true;
        }

        // Check if current primitive has same parameters as input
        public bool HasSameParameters(Dictionary<String, Object> parameters,
                                      Dictionary<String, IPrimitive> primitivesDict)
        {
            return true;
        }

        //#########################################################################################
        //
        // Implement interface ICheckable
        //
        //#########################################################################################
        public Object Check(Object key)
        {
            return _count;
        }

        //#########################################################################################
        //
        //  Class implemention
        //
        //#########################################################################################
        /// <summary>
        /// Constructor
        /// </summary>
        public BasicCounter(Engine engine)
        {
            SignalReceiver = new SignalTarget(engine, this);
            SignalReceiver.OnTrigger += OnTrigger;
        }

        /// <summary>
        /// Callback from SignalReceiver, on signal triggered
        /// </summary>
        private void OnTrigger(Object parameter, Object context)
        {
            switch ( (int)parameter )
            {
                case 1:
                    Interlocked.Increment(ref _count);
                    break;
                case -1:
                    Interlocked.Decrement(ref _count);
                    break;
                case 0:
                    Interlocked.Exchange(ref _count, 0);
                    break;
            }
            Console.WriteLine("\tPrimitive[{0}] triggered, current count {1}", GetType().Name, 
                              _count);
        }
    }
}
