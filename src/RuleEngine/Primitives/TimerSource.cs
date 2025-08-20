//-------------------------------------------------------------------------------------------------
//  TimerSource.cs
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
    /// Description: Register to system timer, generate timer "ticks" and output signal.
    /// 
    /// Parameters:
    ///     Interval: String.
    ///         "OneTenthSecond" : Trigger signal every 100ms
    ///         "Second" : Trigger signal every second
    ///         "Minute" : Trigger signal every minute
    /// 
    /// Signal Parameters: Not targeted
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class TimerSource : IPrimitive, IDisposable
    {
        //
        // Private variables
        //
        enum Frequency {
            OneTenthSecond,
            Second,
            Minute
        }
        private Timer _timer = null;
        private int _timerPeriod;
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
            if ( !ParseParameters(parameters, primitivesDict, out _timerPeriod, out _errorMessage) )
                return false;

            _timer = new Timer(OnTimer);
            return true;
        }

        // Check if current primitive has same parameters as input
        public bool HasSameParameters(Dictionary<String, Object> parameters,
                                      Dictionary<String, IPrimitive> primitivesDict)
        {
            int timerPeriod = 0;
            if ( !ParseParameters(parameters, primitivesDict, out timerPeriod, out _errorMessage) )
                return false;

            return (timerPeriod == _timerPeriod);
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
            int timerPeriod = 0;
            return ParseParameters(parameters, knownPrimitives, out timerPeriod, 
                                    out errorMessage);
        }

        public static bool Targetable { get { return false; } }

        //#########################################################################################
        //
        //  Class implemention
        //
        //#########################################################################################
        /// <summary>
        /// Constructor
        /// </summary>
        public TimerSource(Engine engine)
        {
            SignalReceiver = new SignalTarget(engine, this);
            SignalSender = new SignalSource(engine, this);
            SignalSender.OnAllTargetsPaused += OnAllTargetsPaused;
            SignalSender.OnFirstTargetActivated += OnFirstTargetActivated;
        }

        /// <summary>
        /// Timer need to be disposed
        /// </summary>
        public void Dispose()
        {
            if ( _timer != null )
            {
                _timer.Dispose();
                _timer = null;
            }
        }

        /// <summary>
        /// Callback from Timer
        /// </summary>
        private void OnTimer(Object stateInfo)
        {
            Console.WriteLine("\tPrimitive[{0}] fire", GetType().Name);
            SignalSender.Trigger(null);
        }

        private static bool ParseParameters(Dictionary<String, Object> parameters,
                                            Dictionary<String, IPrimitive> primitivesDict,
                                            out int timerPeriod, out String errorMessage)
        {
            errorMessage = null;
            timerPeriod = 0;
            Object param;

            if ( !Primitive.ValidateParam(parameters, "Frequency", typeof(String), out param,
                                          out errorMessage) )
                return false;

            Frequency freq;
            if ( !Primitive.ParseEnumParam(param, "Frequency", out freq, out errorMessage) )
                return false;

            switch ( freq ) 
            {
                case Frequency.OneTenthSecond :
                    timerPeriod = 100;
                    break;
                case Frequency.Second :
                    timerPeriod = 1000;
                    break;
                case Frequency.Minute :
                    timerPeriod = 60000;
                    break;
            }

            return true;
        }

        /// <summary>
        /// Delegate called by SignalSender, stop timer when all targets paused
        /// </summary>
        private void OnAllTargetsPaused()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            Console.WriteLine("\tPrimitive[{0}] paused", GetType().Name);
        }

        /// <summary>
        /// Delegate called by SignalSender, activate timer on the first target attached.
        /// </summary>
        private void OnFirstTargetActivated()
        {
            _timer.Change(_timerPeriod, _timerPeriod);
            Console.WriteLine("\tPrimitive[{0}] resumed", GetType().Name);
        }
    }
}
