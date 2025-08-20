//-------------------------------------------------------------------------------------------------
//  Accumulator.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: Accumulate on input, output signal when reach the threshold
    /// 
    /// Parameters:
    ///     ThresHold : The number on which we output and restart.
    ///     Timeout : Timeout for each input
    /// 
    /// Signal Parameters:
    ///     Command : String. Optional. "Reset" reset count to 0
    ///     Value : count to add onto the total number
    ///               
    /// ICheckable : No
    /// ExtraDependees : None
    /// </summary>
    internal sealed class Accumulator : IPrimitive
    {
        //
        // Private variables
        //
        class Parameters
        {
            public int threshold;
            public int timeout;
        }

        class InputItem {
            public object context;
            public int expireTime;
            public int value;
        }
        private Parameters _params;
        private List<InputItem> _inputs = new List<InputItem>();
        private object _lock = new object();
        private int _totalValue = 0;
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
            return ((param.threshold==_params.threshold) && (param.timeout==_params.timeout));
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
        public Accumulator(Engine engine)
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
            if ( parameter != null )
            {
                lock ( _lock )
                {
                    if ( (parameter is String) && (parameter as String) == "Reset" )
                    {
                        Console.WriteLine("\tPrimitive[{0}] Reset", GetType().Name);
                        _inputs.Clear();
                        _totalValue = 0;
                    }
                    else if ( parameter is int )
                    {
                        int value = (int)parameter;
                        int curTime = 0;

                        // Remove expired ones first
                        if ( _params.timeout > 0 )
                        {
                            curTime = Environment.TickCount;
                            for ( int i=0; i<_inputs.Count; i++ )
                            {
                                if ( curTime - _inputs[i].expireTime > 0 )
                                {
                                    _totalValue -= _inputs[i].value;
                                    _inputs.RemoveAt(i);
                                    i--;
                                }
                                else
                                    break;
                            }
                        }

                        // Add new one
                        InputItem item = new InputItem { context=context, value=value };
                        if ( _params.timeout > 0 )
                            item.expireTime = curTime + _params.timeout;
                        _totalValue += value;
                        _inputs.Add(item);
                        Console.WriteLine("\tPrimitive[{0}] input {1} total={2}", GetType().Name, 
                                          value, _totalValue);

                        // Trigger signal if reach threshold
                        if ( _totalValue >= _params.threshold )
                        {
                            Console.WriteLine("\tPrimitive[{0}] triggered, value {1} threshold {2}",
                                              GetType().Name, _totalValue, _params.threshold);
                            List<object> outputContext = new List<object>();
                            outputContext.Add(_totalValue);
                            foreach ( var inputItem in _inputs )
                                outputContext.Add(inputItem.context);
                            SignalSender.Trigger(outputContext);

                            _inputs.Clear();
                            _totalValue = 0;
                        }
                    }
                }
            }
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

            if ( !Primitive.ValidateParam(parameters, "Threshold", typeof(int), out param,
                                          out errorMessage) )
                return false;

            parsed.threshold = (int)param;

            if ( Primitive.ValidateParam(parameters, "Timeout", typeof(int), out param,
                                         out errorMessage) )
                parsed.timeout = (int)param;

            return true;
        }

    }
}
