//-------------------------------------------------------------------------------------------------
//  SignalTarget.cs
//
//  Created by Ming Jin on 08/19/20.
//  Copyright (c) 2020-2025 Ming Jin. All rights reserved.
//
//-------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RuleEngine
{
    internal class SignalTarget
    {
        // Owner of this target, who will receive event from this target
        public Object Owner { get; private set; }
        // SignalSources connected to this target
        public List<SignalSource> ConnectedSources { get; private set; }

        /// <summary>
        /// Event on trigger
        /// </summary>
        public delegate void TriggerFunc(Object parameter, Object context);
        public event TriggerFunc OnTrigger;

        /// <summary>
        /// Constructor, have to initialize property
        /// </summary>
        public SignalTarget(Engine engine, Object owner)
        {
            ConnectedSources = new List<SignalSource>();
            Owner = owner;
        }

        /// <summary>
        /// Inform that one SignalSource is connected to this target. This function should save
        /// source pointers for later query
        /// </summary>
        public void ConnectFrom(SignalSource source)
        {
            ConnectedSources.Add(source);
        }

        /// <summary>
        /// Disconnect from one signal source
        /// </summary>
        /// <param name="source"></param>
        public void DisconnectFrom(SignalSource source)
        {
            int index = ConnectedSources.FindIndex(x => Object.ReferenceEquals(x, source));
            if ( index >= 0 )
                ConnectedSources.RemoveAt(index);
        }

        /// <summary>
        /// Trigger signal with parameters
        /// </summary>
        public void Trigger(Object parameter, Object context)
        {
            if ( OnTrigger != null )
                OnTrigger(parameter, context);
        }

        /// <summary>
        /// Owner call this to inform that it don't want to be triggered
        /// </summary>
        public void Pause()
        {
            foreach ( SignalSource sigSrc in ConnectedSources )
                sigSrc.TargetPaused(this);
        }

        /// <summary>
        /// Owner call this to inform that it want signals again
        /// </summary>
        public void Resume()
        {
            foreach ( SignalSource sigSrc in ConnectedSources )
                sigSrc.TargetResumed(this);
        }
    }
}
