//-------------------------------------------------------------------------------------------------
//  InternalInterfaces.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace RuleEngine
{
    /// <summary>
    /// Interface should be implemented by all primitives
    /// </summary>
    internal interface IPrimitive
    {
        String ErrorMessage { get; }

        // This primitive use this sender to send signals
        SignalSource SignalSender { get; }

        // Optional signal sender on negative condition
        SignalSource SignalSenderOnNegative { get; }

        // This primitive receive signals from this receiver
        SignalTarget SignalReceiver { get; }

        // Beside normal dependencies (Signal targets depend on signal source), if this primitive
        // depend on other primitives (for example checker depend on ICheckable), put dependees
        // here
        List<IPrimitive> ExtraDependees { get; }

        // Count of dependers of this primitive, can be delete if 0
        // "Depender" here means other primitive need this one to finish job
        // So it will increase whenever connect to a signal target, and when a checker refer it if
        // it is ICheckable
        int DependerCount { get; set; }

        // Setup one primitive with parameters
        bool Setup(Dictionary<String, Object> parameters,
                   Dictionary<String, IPrimitive> primitivesDict);

        // Check if current primitive has same parameters as input
        bool HasSameParameters(Dictionary<String, Object> parameters,
                               Dictionary<String, IPrimitive> primitivesDict);
    }

    /// <summary>
    /// Interface optionally be implemented by certain primitives
    /// </summary>
    internal interface ICheckable
    {
        // Return current internal value, opitonal accept a key parameter of "keyed" primitives.
        Object Check(Object key);
    }
}
