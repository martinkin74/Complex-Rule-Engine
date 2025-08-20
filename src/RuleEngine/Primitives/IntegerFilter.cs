//-------------------------------------------------------------------------------------------------
//  IntegerFilter.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: Do logic condition check on input integer, publish positive signal if 
    ///     condition is true, otherwise publish negative signal
    /// 
    /// Parameters:
    ///     Condition : String. Logic check to do. "Equal" "LessThan" "GreaterThan"
    ///     CompareTo : Integer. The value to compare
    /// 
    /// Signal Parameters:
    ///     Value : Integer. Value to be compared
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class IntegerFilter : IPrimitive
    {
        //
        // Private variables
        //
        enum Condition : int
        {
            LessThan,
            Equals,
            GreaterThan,
            OneOf,
        }

        class Parameters {
            public Condition condition;
            public int compareTo;
            public List<int> compareTos;
        }

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

            switch ( _params.condition )
            {
                case Condition.Equals:
                    SignalReceiver.OnTrigger += PerformFilter_Equals;
                    break;
                case Condition.LessThan:
                    SignalReceiver.OnTrigger += PerformFilter_LessThan;
                    break;
                case Condition.GreaterThan:
                    SignalReceiver.OnTrigger += PerformFilter_GreaterThan;
                    break;
                case Condition.OneOf:
                    SignalReceiver.OnTrigger += PerformFilter_OneOf;
                    break;
            }
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
            if ( param.condition != _params.condition )
                return false;

            if ( param.condition==Condition.OneOf )
            {
                if ( param.compareTos.Count != _params.compareTos.Count )
                    return false;
                foreach ( int n in param.compareTos )
                {
                    if ( _params.compareTos.IndexOf(n) < 0 )
                        return false;
                }
            }
            else if ( param.compareTo != _params.compareTo )
                return false;

            return true;
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

        /// <summary>
        /// Constructor
        /// </summary>
        public IntegerFilter(Engine engine)
        {
            SignalReceiver = new SignalTarget(engine, this);

            SignalSender = new SignalSource(engine, this);
            SignalSenderOnNegative = new SignalSource(engine, this);
        }

        /// <summary>
        /// Callback from SignalReceiver, on signal triggered
        /// Do "Equals" comparing
        /// </summary>
        private void PerformFilter_Equals(Object parameter, Object context)
        {
            int inputInt = (int)parameter;
            if ( inputInt == _params.compareTo )
            {
                Console.WriteLine("\tPrimitive[{0}] input value {1}. Trigger 'Positive'",
                                  GetType().Name, inputInt);
                SignalSender.Trigger(context);
            }
            else
            {
                Console.WriteLine("\tPrimitive[{0}] input value {1}. Trigger 'Negative'", 
                                  GetType().Name, inputInt);
                SignalSenderOnNegative.Trigger(context);
            }
        }

        /// <summary>
        /// Callback from SignalReceiver, on signal triggered
        /// Do "LessThan" comparing
        /// </summary>
        private void PerformFilter_LessThan(Object parameter, Object context)
        {
            int inputInt = (int)parameter;
            if ( inputInt < _params.compareTo )
            {
                Console.WriteLine("\tPrimitive[{0}] input value {1}. Trigger 'Positive'",
                                  GetType().Name, inputInt);
                SignalSender.Trigger(context);
            }
            else
            {
                Console.WriteLine("\tPrimitive[{0}] input value {1}. Trigger 'Negative'",
                                  GetType().Name, inputInt);
                SignalSenderOnNegative.Trigger(context);
            }
        }

        /// <summary>
        /// Callback from SignalReceiver, on signal triggered
        /// Do "GreaterThan" comparing
        /// </summary>
        private void PerformFilter_GreaterThan(Object parameter, Object context)
        {
            int inputInt = (int)parameter;
            if ( inputInt > _params.compareTo )
            {
                Console.WriteLine("\tPrimitive[{0}] input value {1}. Trigger 'Positive'",
                                  GetType().Name, inputInt);
                SignalSender.Trigger(context);
            }
            else
            {
                Console.WriteLine("\tPrimitive[{0}] input value {1}. Trigger 'Negative'",
                                  GetType().Name, inputInt);
                SignalSenderOnNegative.Trigger(context);
            }
        }

        /// <summary>
        /// Callback from SignalReceiver, on signal triggered
        /// Check if input value is one of expected values
        /// </summary>
        private void PerformFilter_OneOf(Object parameter, Object context)
        {
            int inputInt = (int)parameter;
            if ( _params.compareTos.IndexOf(inputInt) >= 0 )
            {
                Console.WriteLine("\tPrimitive[{0}] input value {1}. Trigger 'Positive'",
                                  GetType().Name, inputInt);
                SignalSender.Trigger(context);
            }
            else
            {
                Console.WriteLine("\tPrimitive[{0}] input value {1}. Trigger 'Negative'",
                                  GetType().Name, inputInt);
                SignalSenderOnNegative.Trigger(context);
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

            if ( !Primitive.ValidateParam(parameters, "Condition", typeof(String),
                                          out param, out errorMessage) ||
                 !Primitive.ParseEnumParam(param, "Condition", out parsed.condition,
                                           out errorMessage) )
                return false;

            if ( parsed.condition == Condition.OneOf )
            {
                if ( !Primitive.ValidateParam(parameters, "CompareTo", typeof(List<Object>),
                                                out param, out errorMessage) )
                    return false;

                parsed.compareTos = new List<int>();
                foreach ( Object obj in (param as List<Object>) )
                {
                    if ( !(obj is int) )
                    {
                        errorMessage = "Parameter 'CompareTo' array contains non-int value";
                        return false;
                    }
                    parsed.compareTos.Add((int)obj);
                }
            }
            else
            {
                if ( !Primitive.ValidateParam(parameters, "CompareTo", typeof(int),
                                          out param, out errorMessage) )
                    return false;

                parsed.compareTo = (int)param;
            }

            return true;
        }
    }
}
