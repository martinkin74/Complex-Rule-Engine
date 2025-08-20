using System;
using System.Collections.Generic;

namespace RuleEngine
{
    internal class SignalSource
    {
        public delegate void EventFunc();

        // Event triggered when all targets paused, Owner can optionally release resource
        public event EventFunc OnAllTargetsPaused;

        // Event Triggered when first target added, or any target resumed after all target paused
        // Owner can optinally allocate resource.
        public event EventFunc OnFirstTargetActivated;

        // Owner of this SignalSource, set by owner
        public Object Owner { get; private set; }

        public int TargetsCount { get { return _targets.Count; } }
        public List<SignalTarget> Targets
        {
            get
            {
                List<SignalTarget> result = new List<SignalTarget>();
                foreach ( TargetData target in _targets )
                    result.Add(target.target);

                return result;
            }
        }

        // Constructor which force owner to setup the owner property
        public SignalSource(Engine engine, Object owner)
        {
            _engine = engine;
            Owner = owner; 
        }

        /// <summary>
        /// Add one SignalTarget in the target list. This function also call the target to setup
        /// reverse SignalSource link inside that target
        /// </summary>
        public void ConnectTo(SignalTarget target, Object parameter)
        {
            TargetData data = new TargetData { 
                target=target, 
                paused=false,
                rawParameter = parameter
            };

            if ( parameter is List<Object> )
            {
                // Determine if parameters contains macro
                bool listHasMacro = false;
                List<Object> paramList = parameter as List<Object>;
                for ( int i = 0; i<paramList.Count; i++ )
                {
                    if ( paramList[i] is String && (paramList[i] as String).StartsWith("#MACRO#") )
                    {
                        listHasMacro = true;
                        break;
                    }
                }

                if ( listHasMacro )
                {
                    data.paramsWithMacro = new List<SigParam>();
                    for ( int i = 0; i<paramList.Count; i++ )
                    {
                        SigParam param = new SigParam { rawParam = paramList[i] };
                        if ( paramList[i] is String )
                        {
                            String strParam = paramList[i] as String;
                            if ( strParam.StartsWith("#MACRO#") )
                            {
                                param.macro = new Macro(_engine);
                                param.macro.Parse(strParam.Substring("#MACRO#".Length));
                            }
                        }
                        data.paramsWithMacro.Add(param);
                    }
                }
            }
            else if ( parameter is String )
            {
                String strParam = parameter as String;
                if ( strParam.StartsWith("#MACRO#") )
                {
                    data.macroParam = new Macro(_engine);
                    data.macroParam.Parse(strParam.Substring("#MACRO#".Length));
                }
            }

            _targets.Add(data);

            // Inform target about new connection
            target.ConnectFrom(this);

            // Fire event on the first target
            if ( TargetsCount == 1 && OnFirstTargetActivated != null )
                OnFirstTargetActivated();
        }

        /// <summary>
        /// Disconnect from one target
        /// </summary>
        public void DisconnectFrom(SignalTarget target)
        {
            int index = _targets.FindIndex(x => Object.ReferenceEquals(x.target, target));
            if ( index >= 0 )
            {
                if ( _targets[index].paused )
                    _nPausedTargets--;

                target.DisconnectFrom(this);

                _targets.RemoveAt(index);
            }
        }

        /// <summary>
        /// Trigger one signal
        /// </summary>
        public void Trigger(Object context)
        {
            foreach ( TargetData target in _targets )
            {
                if ( target.paramsWithMacro != null )
                {
                    List<Object> sigParam = new List<object>();
                    foreach ( SigParam param in target.paramsWithMacro )
                    {
                        if ( param.macro != null )
                            sigParam.Add(param.macro.Run(context));
                        else
                            sigParam.Add(param.rawParam);
                    }
                    target.target.Trigger(sigParam, context);
                }
                else if ( target.macroParam != null )
                    target.target.Trigger(target.macroParam.Run(context), context);
                else
                    target.target.Trigger(target.rawParameter, context);
            }
        }

        /// <summary>
        /// Get signal parameters defined in rule for one target
        /// </summary>
        public Object GetTargetSigParam(SignalTarget target)
        {
            TargetData data = FindTarget(target);
            if ( data != null )
                return data.rawParameter;
            else
                return null;
        }

        /// <summary>
        /// SignalTarget inform SignalSource that the target doesn't want new signals
        /// </summary>
        public void TargetPaused(SignalTarget target)
        {
            TargetData data = FindTarget(target);
            if ( data != null && !data.paused )
            {
                data.paused = true;
                _nPausedTargets++;
                if ( _nPausedTargets == TargetsCount && OnAllTargetsPaused != null )
                    OnAllTargetsPaused();
            }
        }

        /// <summary>
        /// SignalTarget inform SignalSource that the target want new signals again
        /// </summary>
        public void TargetResumed(SignalTarget target)
        {
            TargetData data = FindTarget(target);
            if ( data != null && data.paused )
            {
                data.paused = false;
                _nPausedTargets--;
                if ( _nPausedTargets == TargetsCount-1 && OnFirstTargetActivated != null )
                    OnFirstTargetActivated();
            }
        }

        /// <summary>
        /// Find one target by the object reference
        /// </summary>
        private TargetData FindTarget(SignalTarget target)
        {
            int targetIndex = _targets.FindIndex(
                                    x => Object.ReferenceEquals(x.target, target));
            if ( targetIndex >= 0 )
                return _targets[targetIndex];
            else
                return null;
        }

        //
        // Private data
        //

        /// <summary>
        /// Define a signal parameter
        /// use macro if not null, otherwise use rawParam
        /// </summary>
        struct SigParam
        {
            public Object rawParam;
            public Macro macro;
        }

        /// <summary>
        /// Define a connected target and parameters with it.
        /// </summary>
        class TargetData
        {
            public SignalTarget target;
            public bool paused;
            // If original parameters contain no macro, use it to trigger target directly
            // Otherwise if it is single macro parameter, use "macroParam"
            // else use "paramsWithMacro"
            public Object rawParameter;
            public Macro macroParam = null;
            public List<SigParam> paramsWithMacro = null;
        }

        private Engine _engine;
        private List<TargetData> _targets = new List<TargetData>();

        // Count of paused targets
        private int _nPausedTargets;
    }
}
