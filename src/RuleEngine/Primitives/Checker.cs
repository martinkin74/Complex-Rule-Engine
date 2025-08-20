//-------------------------------------------------------------------------------------------------
//  Checker.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: On input signal, observe value from ICheckable primitive, do a logic condition
    ///     check, publish positive signal if condition is true, publish negative signal if not.
    /// 
    /// Parameters:
    ///     CheckTarget : String. The ICheckable primitive name to check
    ///     Condition : String. Logic check to do. "Equal" "LessThan" "GreaterThan"
    ///     CompareTo : Integer. The value to compare
    ///     AutoRollOver : Bool. Automatically increase barrier of "CompareTo" value after each
    ///         positive matching.
    ///         
    /// Signal Parameters: None
    ///               
    /// ICheckable : No
    /// Dependencies : CheckTarget
    /// </summary>
    internal sealed class Checker : IPrimitive
    {
        //
        // Private variables
        //
        enum CONDITION
        {
            LessThan,
            Equals,
            GreaterThan
        }

        class Parameters {
            public String checkTarget;
            public CONDITION condition;
            public int compareTo;
            public bool autoRollOver;
        }
        private Parameters _params;
        private ICheckable _checkTarget;
        private int _autoRollOverValue;
        private String _errorMessage;

        //
        // Implement interface IPrimitive
        //
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

            IPrimitive checkTarget = primitivesDict[_params.checkTarget];
            if ( !(checkTarget is ICheckable) )
            {
                _errorMessage = String.Format("CheckTarget '{0}' is not ICheckable",
                                              _params.checkTarget);
                return false;
            }

            _checkTarget = checkTarget as ICheckable;
            (_checkTarget as IPrimitive).DependerCount++;

            _autoRollOverValue = _params.compareTo;

            return true;
        }

        // Check if current primitive has same parameters as input
        public bool HasSameParameters(Dictionary<String, Object> parameters,
                                      Dictionary<String, IPrimitive> primitivesDict)
        {
            Parameters param;
            if ( !ParseParameters(parameters, primitivesDict, out param, out _errorMessage) )
                return false;

            IPrimitive checkTarget;
            if ( !primitivesDict.TryGetValue(param.checkTarget, out checkTarget) ||
                 !Object.ReferenceEquals(checkTarget, _checkTarget) )
                return false;

            if ( param.condition != _params.condition ||
                 param.compareTo != _params.compareTo ||
                 param.autoRollOver != _params.autoRollOver )
                return false;

            return true;
        }

        //
        // Implement static functions optionally required by Primitive
        //
        public static bool ValidateParameters(Engine engine,
                                              Dictionary<String, Object> parameters,
                                              Dictionary<String, IPrimitive> knownPrimitives,
                                              out String errorMessage)
        {
            Parameters param;
            return ParseParameters(parameters, knownPrimitives, out param, out errorMessage);
        }

        public static List<String> ListExtraDependees(Dictionary<String, Object> parameters)
        {
            Object param;
            if ( !parameters.TryGetValue("CheckTarget", out param) || !(param is String) )
                return null;

            return new List<String>{param as String};
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public Checker(Engine engine)
        {
            SignalReceiver = new SignalTarget(engine, this);
            SignalReceiver.OnTrigger += OnTrigger;
            SignalSender = new SignalSource(engine, this);
            SignalSenderOnNegative = new SignalSource(engine, this);
        }

        /// <summary>
        /// Callback from SignalReceiver, on signal triggered
        /// </summary>
        private void OnTrigger(Object parameter, Object context)
        {
            int checkResult = (int)_checkTarget.Check(null);

            bool positiveResult = false;
            if ( ((_params.condition==CONDITION.Equals) && (checkResult==_params.compareTo)) ||
                 ((_params.condition==CONDITION.LessThan) && (checkResult<_params.compareTo)) ||
                 ((_params.condition==CONDITION.GreaterThan) && (checkResult>_params.compareTo)) )
                positiveResult = true;

            Console.WriteLine("\tPrimitive[{0}] triggered, check result {1}, positive={2}",
                              GetType().Name, checkResult, positiveResult);

            if ( positiveResult )
                SignalSender.Trigger(context);
            else
                SignalSenderOnNegative.Trigger(context);
        }

        private static bool ParseParameters(Dictionary<String, Object> parameters,
                                            Dictionary<String, IPrimitive> primitivesDict,
                                            out Parameters parsed, out String errorMessage)
        {
            errorMessage = null;
            parsed = new Parameters();
            Object param;

            if ( !Primitive.ValidateParam(parameters, "CheckTarget", typeof(String), out param,
                                          out errorMessage) )
                return false;

            parsed.checkTarget = param as String;

            if ( !primitivesDict.ContainsKey(parsed.checkTarget) )
            {
                errorMessage = String.Format("CheckTarget '{0}' is not defined in rule", 
                                             parsed.checkTarget);
                return false;
            }

            if ( !Primitive.ValidateParam(parameters, "Condition", typeof(String), out param,
                                          out errorMessage) ||
                 !Primitive.ParseEnumParam(param, "Condition", out parsed.condition, 
                                           out errorMessage) )
                return false;

            if ( !Primitive.ValidateParam(parameters, "CompareTo", typeof(int), out param,
                                          out errorMessage) )
                return false;

            parsed.compareTo = (int)param;

            if ( parameters.TryGetValue("AutoRollOver", out param) )
            {
                if ( !(param is bool) )
                {
                    errorMessage = "Parameter 'AutoRollOver' is not boolean";
                    return false;
                }
                parsed.autoRollOver = (bool)param;
            }

            return true;
        }
    }
}
