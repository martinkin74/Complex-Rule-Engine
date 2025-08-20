using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Description: Do string matching on input string, publish positive signal if matched,
    ///     otherwise publish negative signal
    /// 
    /// Parameters:
    ///     Method: String. Match method. "MatchSingle", "MatchList", "DictionarySearch"
    ///     SubstringPos : Integer. Optional. Specify the start position if only match part of 
    ///                    input string.
    ///     Condition : String. For "MatchSingle" and "MatchList"
    ///                 "Equal" "Contains" "StartsWith" "EndsWith" "Regex"
    ///     MatchTo : List<String> For "MatchList" and "DictionarySearch", specify the target
    ///                  string list.
    ///               String for "MatchSingle"
    /// 
    /// Signal Parameters:
    ///     input : String. input value to be matched
    ///               
    /// ICheckable : No
    /// Dependencies : None
    /// </summary>
    internal sealed class StringFilter : IPrimitive
    {
        //
        // Private variables
        //
        enum Method : int
        {
            MatchSingle,
            DictionarySearch,
            MatchList
        }

        enum Condition : int
        {
            Equals,
            Contains,
            StartsWith,
            EndsWith,
            Regex
        }

        class Parameters {
            public Method method;

            // For all string matching methods
            public int substringPos;

            // For MatchSingle and MatchList
            public Condition strMatchCondition;

            // For MatchSingle
            public String matchToStr;
            public Regex matchToStrRegex;

            // For MatchList
            public List<String> stringList;
            public List<Regex> stringListRegex;

            // For DictionarySearch
            public Dictionary<String, int> stringDict;
        }

        private Parameters _params;
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
            if ( param.method != _params.method )
                return false;

            if ( param.substringPos != _params.substringPos )
                return false;

            if ( _params.method == Method.DictionarySearch )
            {
                if ( param.stringDict.Count != _params.stringDict.Count ||
                     !param.stringDict.Keys.SequenceEqual(_params.stringDict.Keys) )
                    return false;
            }
            else
            {
                if ( param.strMatchCondition != _params.strMatchCondition )
                    return false;

                if ( _params.method == Method.MatchSingle )
                {
                    if ( _params.strMatchCondition == Condition.Regex )
                    {
                        if ( !param.matchToStr.Equals(_params.matchToStr) )
                            return false;
                    }
                    else
                    {
                        if ( !param.matchToStr.Equals(_params.matchToStr,
                                                      StringComparison.OrdinalIgnoreCase) )
                            return false;
                    }
                }
                else
                {
                    if ( param.stringList.Count != _params.stringList.Count )
                        return false;
                    foreach ( String str in param.stringList )
                    {
                        int index;
                        if ( _params.strMatchCondition == Condition.Regex )
                            index = _params.stringList.FindIndex(
                                x => x.Equals(str, StringComparison.OrdinalIgnoreCase));
                        else
                            index = _params.stringList.FindIndex(x => x.Equals(str));
                        if ( index < 0 )
                            return false;
                    }
                }
            }

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

        /// <summary>
        /// Constructor
        /// </summary>
        public StringFilter(Engine engine)
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
            bool matched = false;
            String input = parameter as String;

            if ( _params.substringPos > 0 )
            {
                if ( input.Length <= _params.substringPos )
                    input = null;
                else
                    input = input.Substring(_params.substringPos);
            }

            if ( input != null )
            {
                switch ( _params.method )
                {
                    case Method.MatchSingle:
                        if ( _params.strMatchCondition == Condition.Regex )
                            matched = _params.matchToStrRegex.IsMatch(input);
                        else
                            matched = MatchString(input, _params.matchToStr);
                        break;

                    case Method.DictionarySearch:
                        matched = _params.stringDict.ContainsKey(input.ToLower());
                        break;

                    case Method.MatchList:
                        if ( _params.strMatchCondition == Condition.Regex )
                            matched = _params.stringListRegex.Exists(x => x.IsMatch(input));
                        else
                            matched = _params.stringList.Exists(x => MatchString(input, x));
                        break;
                }
            }

            Console.WriteLine("\tPrimitive[{0}] input '{1}' matched={2}. {3}", GetType().Name, 
                              parameter, matched, 
                              matched ? "Trigger 'Positive'" : "Trigger 'Negative'");
            if ( matched )
                SignalSender.Trigger(context);
            else
                SignalSenderOnNegative.Trigger(context);
        }

        /// <summary>
        /// Common function to do single string matching
        /// </summary>
        private bool MatchString(String input, String compareTo)
        {
            switch ( _params.strMatchCondition )
            {
                case Condition.Equals:
                    return input.Equals(compareTo, StringComparison.OrdinalIgnoreCase);

                case Condition.Contains:
                    return input.IndexOf(compareTo, StringComparison.OrdinalIgnoreCase) >= 0;

                case Condition.StartsWith:
                    return input.StartsWith(compareTo, StringComparison.OrdinalIgnoreCase);

                case Condition.EndsWith:
                    return input.EndsWith(compareTo, StringComparison.OrdinalIgnoreCase);
            }
            return false;
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

            if ( !Primitive.ValidateParam(parameters, "Method", typeof(String), 
                                          out param, out errorMessage) ||
                 !Primitive.ParseEnumParam(param, "Method", out parsed.method, out errorMessage) )
                return false;

            if ( Primitive.ValidateParam(parameters, "SubstringPos", typeof(int), 
                                         out param, out errorMessage) )
                parsed.substringPos = (int)param;

            if ( parsed.method == Method.DictionarySearch )
            {
                parsed.stringDict = new Dictionary<string,int>();
                if ( !Primitive.ValidateParam(parameters, "MatchTo", typeof(List<Object>),
                                              out param, out errorMessage) )
                    return false;

                foreach ( Object obj in (param as List<Object>) )
                {
                    if ( !(obj is String) )
                    {
                        errorMessage = "Parameter 'MatchTo' array contains non-string";
                        return false;
                    }
                    parsed.stringDict[(obj as String).ToLower()] = 1;
                }
            }
            else
            {
                if ( !Primitive.ValidateParam(parameters, "Condition", typeof(String),
                                                out param, out errorMessage) ||
                     !Primitive.ParseEnumParam(param, "Condition", out parsed.strMatchCondition,
                                                out errorMessage) )
                    return false;

                if ( parsed.method == Method.MatchSingle )
                {
                    if ( !Primitive.ValidateParam(parameters, "MatchTo", typeof(String),
                                                    out param, out errorMessage) )
                        return false;
                    parsed.matchToStr = param as String;

                    if ( parsed.strMatchCondition == Condition.Regex )
                        parsed.matchToStrRegex = new Regex(parsed.matchToStr);
                }
                else
                {
                    if ( !Primitive.ValidateParam(parameters, "MatchTo", typeof(List<Object>),
                                                    out param, out errorMessage) )
                        return false;

                    parsed.stringList = new List<String>();
                    if ( parsed.strMatchCondition == Condition.Regex )
                        parsed.stringListRegex = new List<Regex>();

                    foreach ( Object obj in (param as List<Object>) )
                    {
                        if ( !(obj is String) )
                        {
                            errorMessage = "Parameter 'MatchTo' array contains non-string";
                            return false;
                        }
                        parsed.stringList.Add(obj as String);
                        if ( parsed.strMatchCondition == Condition.Regex )
                            parsed.stringListRegex.Add(new Regex(obj as String));
                    }
                }
            }

            return true;
        }
    }
}
