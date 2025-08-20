using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RuleEngine;

namespace RuleEngine.Primitives
{
    /// <summary>
    /// Define functions works by primitive type string
    /// </summary>
    internal class Primitive
    {
        /// <summary>
        /// Determine if one primitive is defined in rule engine
        /// </summary>
        public static bool IsPrimitiveDefined(String type)
        {
            Type classType = Type.GetType(typeof(Primitive).Namespace + "." + type);
            return (classType != null && typeof(IPrimitive).IsAssignableFrom(classType));
        }

        /// <summary>
        /// Create one primitive based on type
        /// </summary>
        public static IPrimitive Create(Engine engine, String type)
        {
            Type classType = Type.GetType(typeof(Primitive).Namespace + "." + type);
            return Activator.CreateInstance(classType, new Object[]{engine}) as IPrimitive;
        }

        //#########################################################################################
        //
        //  Static functions optionally required for certain primitives
        //
        //#########################################################################################

        /// <summary>
        /// Validate primitive parameters based on type.
        /// Primitives which is configurable by parameters should implement this static method
        /// </summary>
        public static bool ValidateParameters(Engine engine,
                                              String type, 
                                              Dictionary<String, Object> parameters,
                                              Dictionary<String, IPrimitive> knownPrimitives,
                                              out String errorMessage)
        {
            errorMessage = null;
            Type classType = Type.GetType(typeof(Primitive).Namespace + "." + type);
            MethodInfo method = classType.GetMethod("ValidateParameters");
            if ( method == null )
                return true;

            Object[] methodArguments = new Object[] { engine, parameters, knownPrimitives, null };
            bool result = (bool)method.Invoke(null, methodArguments);
            if ( methodArguments[3] != null )
                errorMessage = methodArguments[3].ToString();
            return result;
        }

        /// <summary>
        /// Check if one primitive type is "non targetable" which means it does not receive any
        /// signal. TimerSource is one example
        /// </summary>
        public static bool Targetable(String type)
        {
            Type classType = Type.GetType(typeof(Primitive).Namespace + "." + type);
            PropertyInfo propInfo = classType.GetProperty("Targetable");
            if ( propInfo == null )
                return true;
            else return (bool)propInfo.GetValue(null, null);
        }

        /// <summary>
        /// Check if one primitive depends on others. For example checker depend on check target.
        /// </summary>
        public static List<String> ListExtraDependees(String type, 
                                                      Dictionary<String, Object> parameters)
        {
            Type classType = Type.GetType(typeof(Primitive).Namespace + "." + type);
            MethodInfo method = classType.GetMethod("ListExtraDependees");
            if ( method == null )
                return null;
            else
                return method.Invoke(null, new object[] { parameters }) as List<String>;
        }

        //#########################################################################################
        //
        //  Shared utilities for all primitives
        //
        //#########################################################################################

        /// <summary>
        /// Verify a parameter is inside the list and has correct type
        /// </summary>
        public static bool ValidateParam(Dictionary<String, Object> parameters,
                                         String paramName, Type paramType, 
                                         out Object value, out String errorMessage)
        {
            errorMessage = null;
            value = null;
            if ( !parameters.TryGetValue(paramName, out value) )
            {
                errorMessage = String.Format("Parameters does not contains '{0}'", paramName);
                return false;
            }

            if ( value.GetType() != paramType )
            {
                errorMessage = String.Format("Parameter '{0}' is not {1}", paramName, 
                                             paramType.ToString());
                return false;
            }

            return true;
        }

        /// <summary>
        /// Try to parse one string parameter into an enum type
        /// </summary>
        public static bool ParseEnumParam<T>(Object param, String paramName, out T result,
                                            out String errorMessage)
        {
            result = default(T);
            errorMessage = null;
            try
            {
                result = (T)Enum.Parse(typeof(T), param as String);
            }
            catch
            {
                errorMessage = String.Format("Parameter '{0}' is not valid.", paramName);
                return false;
            }

            return true;
        }
    }
}
