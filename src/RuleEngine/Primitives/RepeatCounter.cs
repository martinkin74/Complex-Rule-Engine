//-------------------------------------------------------------------------------------------------
//  RepeatCounter.cs
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
    /// Description: Count on each input signal until reached certain number, output signal and 
    ///     start from 0 again.
    /// 
    /// Parameters:
    ///     RestartAt : The number on which we output and restart.
    /// 
    /// Signal Parameters:
    ///     Command : Int. Optional. 0 reset count to 0
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class RepeatCounter : IPrimitive
    {
        //
        // Private variables
        //
        private int _count;
        private int _capNumber;
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
            Object param;

            if ( !Primitive.ValidateParam(parameters, "RestartAt", typeof(int), out param,
                                          out _errorMessage) )
                return false;

            _capNumber = (int)param;
            _count = _capNumber;

            return true;
        }

        // Check if current primitive has same parameters as input
        public bool HasSameParameters(Dictionary<String, Object> parameters,
                                      Dictionary<String, IPrimitive> primitivesDict)
        {
            Object param;

            if ( !Primitive.ValidateParam(parameters, "RestartAt", typeof(int), out param,
                                          out _errorMessage) )
                return false;

            return (int)param == _capNumber;
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
            Object param;

            return Primitive.ValidateParam(parameters, "RestartAt", typeof(int), out param,
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
        public RepeatCounter(Engine engine)
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
            if ( parameter != null && (parameter is int) && (int)parameter == 0 )
            {
                Console.WriteLine("\tPrimitive[{0}] Reset", GetType().Name);
                Interlocked.Exchange(ref _count, _capNumber);
            }
            else
            {
                bool downToZero = false;

                // Implement "Interlocked Compare and Decrement" with spin
                int oldValue = _count;
                while ( true )
                {
                    downToZero = (oldValue == 1);
                    int newValue = downToZero ? _capNumber : oldValue-1;
                    int value = Interlocked.CompareExchange(ref _count, newValue, oldValue);
                    if ( value == oldValue )
                        break;
                    oldValue = value;
                }

                Console.WriteLine("\tPrimitive[{0}] triggered, current count {1}", GetType().Name, _count);

                if ( downToZero )
                    SignalSender.Trigger(context);
            }
        }
    }
}
