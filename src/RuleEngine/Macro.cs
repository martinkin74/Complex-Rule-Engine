//-------------------------------------------------------------------------------------------------
//  Macro.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace RuleEngine
{
    internal sealed class Macro
    {
        private IEvent _metaEvent;
        private delegate Object RunFunc(Object input);
        private RunFunc _runFunc;
        private int _eventPropertyId; 
        private int[] _collectionIndexes;   // For macro "Contexts[m][n].event.property"

        public String ErrorMessage { get; private set; }

        public Macro(Engine engine)
        {
            _metaEvent = engine.MetaEvent;
        }

        public bool Parse(String macroStr)
        {
            if ( macroStr.StartsWith("Context.Event.") )
            {
                String propertyName = macroStr.Substring("Context.Event.".Length);
                _eventPropertyId = _metaEvent.GetPropertyId(propertyName);
                if ( _eventPropertyId < 0 )
                {
                    ErrorMessage = String.Format("Invalid event property name '{0}'", propertyName);
                    return false;
                }

                _runFunc = Run_EventProperty;
                return true;
            }
            else if ( macroStr.StartsWith("Contexts[") )
            {
                List<int> indexes = new List<int>();
                int pos = "Contexts".Length;
                while ( pos < macroStr.Length && macroStr[pos] == '[' )
                {
                    int endBracket = macroStr.IndexOf(']', pos+1);
                    if ( endBracket < 0 )
                    {
                        ErrorMessage = String.Format("Mismatch bracket in macro '{0}'", macroStr);
                        return false;
                    }
                    try
                    {
                        int index = Convert.ToInt32(macroStr.Substring(pos+1, endBracket-pos-1));
                        if ( index < 0 )
                        {
                            ErrorMessage = String.Format("Invalid array index number in macro '{0}'",
                                                         macroStr);
                            return false;
                        }
                        indexes.Add(index);
                    }
                    catch
                    {
                        ErrorMessage = String.Format("Invalid array index number in macro '{0}'",
                                                     macroStr);
                        return false;
                    }
                    pos = endBracket+1;
                }

                _collectionIndexes = indexes.ToArray();

                if ( pos == macroStr.Length )
                    _runFunc = Run_Collection;
                else
                {
                    String subStr = macroStr.Substring(pos);
                    if ( !subStr.StartsWith(".Event.") )
                    {
                        ErrorMessage = String.Format("Unknown macro '{0}'", macroStr);
                        return false;
                    }
                    String propertyName = subStr.Substring(".Event.".Length);
                    _eventPropertyId = _metaEvent.GetPropertyId(propertyName);
                    if ( _eventPropertyId < 0 )
                    {
                        ErrorMessage = String.Format("Invalid event property name '{0}'", propertyName);
                        return false;
                    }

                    _runFunc = Run_CollectionEventProperty;
                }
                return true;
            }
            ErrorMessage = String.Format("Unknown macro '{0}'", macroStr);
            return false;
        }

        public Object Run(Object input)
        {
            return _runFunc(input);
        }

        /// <summary>
        /// Macro runner which input is the IEvent, retrieve property from it.
        /// </summary>
        private Object Run_EventProperty(Object input)
        {
            return (input as IEvent).GetProperty(_eventPropertyId);
        }

        /// <summary>
        /// Macro runner which input is collection of contexts, return leaf context as result
        /// </summary>
        private Object Run_Collection(Object input)
        {
            for ( int i = 0; i<_collectionIndexes.Length; i++ )
            {
                List<Object> list = input as List<Object>;
                input = list[_collectionIndexes[i]];
            }

            return input;
        }

        /// <summary>
        /// Macro runner which input is collection of contexts, with each leaf context as IEvent.
        /// Index into it and retrieve property
        /// </summary>
        private Object Run_CollectionEventProperty(Object input)
        {
            for ( int i = 0; i<_collectionIndexes.Length; i++ )
            {
                List<Object> list = input as List<Object>;
                input = list[_collectionIndexes[i]];
            }

            return (input as IEvent).GetProperty(_eventPropertyId);
        }
    }
}
