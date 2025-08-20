using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using RuleEngine;

namespace Tester
{
    /// <summary>
    /// Test event class, try to merge all event types into a single class
    /// </summary>
    class TestEvent : IEvent
    {
        // Define all possible property Id. One event instance may not include all properties.
        enum PropertyId : int
        {
            ProcessName,
            ProcessId,
            CreatorName,
            CreatorProcId,
            Path,
            RegValueName,
            RegValue,
            Score,
            intNumber,
            WindowsEventId,
        }

        internal string _path;
        internal string _processName;
        internal int _processId;
        internal string _creatorName;
        internal int _creatorProcId;
        internal string _regValueName;
        internal int _regValue;
        internal int _score;
        internal int _intNumber;
        internal int _winEvtId;

        // Indicate which property is available in this event, one bit for each property
        uint _propertiesMask = 0;

        //
        // Implement IEvent
        //
        public string EventName { get; protected set; }
        public IEvent CreateInstance(string eventName)
        {
            return new TestEvent(eventName);
        }

        public int GetPropertyId(string propertyName)
        {
            try
            {
                return (int)Enum.Parse(typeof(PropertyId), propertyName);
            }
            catch
            {
                return -1;
            }
        }

        public object GetProperty(int propertyId)
        {
            switch ( propertyId )
            {
                case (int)PropertyId.ProcessName:
                    return _processName;
                case (int)PropertyId.ProcessId:
                    return _processId;
                case (int)PropertyId.CreatorName:
                    return _creatorName;
                case (int)PropertyId.CreatorProcId:
                    return _creatorProcId;
                case (int)PropertyId.Path:
                    return _path;
                case (int)PropertyId.RegValueName:
                    return _regValueName;
                case (int)PropertyId.RegValue:
                    return _regValue;
                case (int)PropertyId.Score:
                    return _score;
                case (int)PropertyId.intNumber:
                    return _intNumber;
                case (int)PropertyId.WindowsEventId:
                    return _winEvtId;
            }
            return null;
        }

        public void SetProperty(int propertyId, object value)
        {
            switch ( propertyId )
            {
                case (int)PropertyId.ProcessName:
                    _processName = value as string;
                    break;
                case (int)PropertyId.ProcessId:
                    _processId = (int)value;
                    break;
                case (int)PropertyId.CreatorName:
                    _creatorName = value as string;
                    break;
                case (int)PropertyId.CreatorProcId:
                    _creatorProcId = (int)value;
                    break;
                case (int)PropertyId.Path:
                    _path = value as string;
                    break;
                case (int)PropertyId.RegValueName:
                    _regValueName = value as string;
                    break;
                case (int)PropertyId.RegValue:
                    _regValue = (int)value;
                    break;
                case (int)PropertyId.Score:
                    _score = (int)value;
                    break;
            }
        }

        //
        // Public methods
        //
        public TestEvent(string eventName)
        {
            EventName = eventName;
            switch ( eventName )
            {
                case "RegistryWrite":
                    _propertiesMask = (1 << (int)PropertyId.Path) | 
                                      (1 << (int)PropertyId.RegValueName) |
                                      (1 << (int)PropertyId.RegValue);
                    break;
                case "RegistryAlert":
                    _propertiesMask = (1 << (int)PropertyId.Score);
                    break;
                case "ProcessStart":
                    _propertiesMask = (1 << (int)PropertyId.ProcessName) |
                                      (1 << (int)PropertyId.ProcessId);
                    break;
                case "ProcessExit":
                    _propertiesMask = (1 << (int)PropertyId.ProcessId);
                    break;
                case "FileCreated":
                    _propertiesMask = (1 << (int)PropertyId.Path) | 
                                      (1 << (int)PropertyId.ProcessId);
                    break;
                case "ScriptExec":
                    _propertiesMask = (1 << (int)PropertyId.Path);
                    break;
                case "MaliciousScriptExec":
                    _propertiesMask = (1 << (int)PropertyId.Path) | 
                                      (1 << (int)PropertyId.CreatorName) |
                                      (1 << (int)PropertyId.CreatorProcId);
                    break;
                case "TestEvent":
                    _propertiesMask = (1 << (int)PropertyId.intNumber);
                    break;
                case "WindowsEvent":
                    _propertiesMask = (1 << (int)PropertyId.WindowsEventId);
                    break;
            }
        }

        public void PrintContent()
        {
            Console.Write("{0} Event[{1}]", DateTime.Now.ToString("HH:mm:ss.fff"), EventName);
            for ( int iBit = 0; iBit<Marshal.SizeOf(_propertiesMask)*8; iBit++ )
            {
                if ( (_propertiesMask & (1 << iBit)) != 0 )
                    Console.Write(" {0}={1}",
                        (PropertyId)Enum.ToObject(typeof(PropertyId), iBit), GetProperty(iBit));
            }
            Console.WriteLine();
        }
    }

    class Program
    {
        static Engine engine;


        static void Test1()
        {
            Console.WriteLine("----Test1----");
            Console.WriteLine("Rule:");
            Console.WriteLine("If \"notepad.exe\" created one file and later the file is executed as script, generate event \"MaliciousScriptExec\"");
            Console.WriteLine();

            Console.WriteLine("Issue raw events");

            TestEvent evt;

            evt = new TestEvent("ProcessStart");
            evt._processName = "notepad.exe";
            evt._processId = 1111;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("FileCreated");
            evt._path = "script1.ps1";
            evt._processId = 1234;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ProcessStart");
            evt._processName = "notepad.exe";
            evt._processId = 2222;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("FileCreated");
            evt._path = "script2.ps1";
            evt._processId = 2222;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ProcessExit");
            evt._processId = 1111;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ProcessExit");
            evt._processId = 2222;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ScriptExec");
            evt._path = "script1.ps1";
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ScriptExec");
            evt._path = "script2.ps1";
            evt.PrintContent();
            engine.ProcessEvent(evt);
        }

        static void TestShare()
        {
            Console.WriteLine("----TestShare----");
            Console.WriteLine("Rule:");
            Console.WriteLine("Combine two rules. One is Test1 rule, The second rule is:");
            Console.WriteLine("If \"notepad.exe\" created one file and later 'TestEvent' happens, generate event \"NewTestEvent\"");
            Console.WriteLine("Two rules share same StringFilter and the first KeyedCollectorInOrder");
            Console.WriteLine();

            Console.WriteLine("Issue raw events");

            TestEvent evt;

            evt = new TestEvent("ProcessStart");
            evt._processName = "notepad.exe";
            evt._processId = 1111;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("FileCreated");
            evt._path = "script1.ps1";
            evt._processId = 1234;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ProcessStart");
            evt._processName = "notepad.exe";
            evt._processId = 2222;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("FileCreated");
            evt._path = "script2.ps1";
            evt._processId = 2222;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ProcessExit");
            evt._processId = 1111;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ProcessExit");
            evt._processId = 2222;
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ScriptExec");
            evt._path = "script1.ps1";
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("ScriptExec");
            evt._path = "script2.ps1";
            evt.PrintContent();
            engine.ProcessEvent(evt);

            evt = new TestEvent("TestEvent");
            evt._intNumber = 1;
            evt.PrintContent();
            engine.ProcessEvent(evt);
        }

        static void Test2()
        {
            Console.WriteLine("----Test2----");
            Console.WriteLine("Rule:");
            Console.WriteLine("If there is new \"FileBlocked\" event and we did not report for 10 seconds, report right away.");
            Console.WriteLine("If we've already reported recently, hold new \"FileBlocked\" events for 10 seconds to aggregate them.");
            Console.WriteLine("Do not report if there is no new \"FileBlocked\"");
            Console.WriteLine();

            TestEvent evt;

            Console.WriteLine("--Issue 15 raw 'FileBlocked' events, interval 1 second.");
            Console.WriteLine("--it should fire 'ReportFiles' on about the 10th.");
            for ( int i=0; i<15; i++ )
            {
                evt = new TestEvent("FileBlocked");
                evt.PrintContent();
                engine.ProcessEvent(evt);
                Thread.Sleep(1000);
            }
            Console.WriteLine("--Sleep 30 seconds. it should fire one 'ReportFiles' in 5 seconds");
            Console.WriteLine("--Timer should stop in 15 seconds");
            Thread.Sleep(30000);

            Console.WriteLine("--Issue one 'FileBlocked' event, this should trigger 'ReportFiles' right away.");
            evt = new TestEvent("FileBlocked");
            evt.PrintContent();
            engine.ProcessEvent(evt);
        }

        static void TestAccumulator()
        {
            Console.WriteLine("----Test Identity Style Rule----");
            Console.WriteLine("Rule:");
            Console.WriteLine("On \"RegistryWrite\" event, if key path equals \"path_1\" and value name equals \"name_1\", give it score 20;");
            Console.WriteLine("if key path equals \"path_2\" and value equals 0 or 1, give it score 30.");
            Console.WriteLine("Generate event \"RegistryAlert\" when accumulated score exceed 60");
            Console.WriteLine();

            TestEvent evt;
            evt = new TestEvent("RegistryWrite");
            evt._path = "path_1";
            evt._regValueName = "name_1";
            evt._regValue = 0;
            Console.WriteLine("--Issue one \"RegistryWrite\" event in first case, accumulate score should be 20");
            engine.ProcessEvent(evt);
            Console.WriteLine("--Issue another \"RegistryWrite\" event in first case, accumulate score should be 40");
            engine.ProcessEvent(evt);

            Console.WriteLine("--Issue one \"RegistryWrite\" event in second case, should fire new event, with score 70");
            evt = new TestEvent("RegistryWrite");
            evt._path = "path_2";
            evt._regValueName = "name_2";
            evt._regValue = 1;
            engine.ProcessEvent(evt);
        }

        static void TestSpeedAlarm()
        {
            Console.WriteLine("----Test Speed Alarm----");
            Console.WriteLine("Rule:");
            Console.WriteLine("On \"WindowsEvent\" event, if EventId equals 4625 and occured more than 3 times within any 5 seconds, trigger alarm;");
            Console.WriteLine();

            TestEvent evt;
            evt = new TestEvent("WindowsEvent");
            evt._winEvtId = 4625;
            Console.WriteLine("--Issue 5 \"WindowsEvent\" events, every 2 second, should not trigger.");
            for ( int i=0; i<5; i++ )
            {
                engine.ProcessEvent(evt);
                Thread.Sleep(2000);
            }
            Console.WriteLine("--Issue 5 \"WindowsEvent\" events, every 1 second, should trigger on the 3rd one");
            for ( int i=0; i<5; i++ )
            {
                engine.ProcessEvent(evt);
                Thread.Sleep(1000);
            }
        }

        static void TestActor(IEvent evt)
        {
            Console.WriteLine("!!!Actor received event '{0}'", evt.EventName);
            (evt as TestEvent).PrintContent();
        }

        static bool LoadRuleFile(String filePath)
        {
            Console.WriteLine("Loading rule file '{0}'", filePath);
            using ( StreamReader file = File.OpenText(filePath) )
            {
                if ( !engine.AddRules(file.ReadToEnd()) )
                {
                    Console.WriteLine(engine.errorMessage);
                    return false;
                }
                return true;
            }
        }

        static void Main(string[] args)
        {
            engine = new Engine(new TestEvent(""));

            if ( !LoadRuleFile("Test1Rule.json") )
                return;

            if ( !LoadRuleFile("Test2Rule.json") )
                return;

            if ( !LoadRuleFile("TestShare.json") )
                return;

            if ( !LoadRuleFile("TestAccumulator.json") )
                return;

            if ( !LoadRuleFile("TestSpeedAlarm.json") )
                return;

            engine.RegisterActor("MaliciousScriptExec", TestActor);
            engine.RegisterActor("ReportFiles", TestActor);
            engine.RegisterActor("NewTestEvent", TestActor);
            engine.RegisterActor("RegistryAlert", TestActor);
            engine.RegisterActor("Event4625Alert", TestActor);
/*
            Test1();
            Console.WriteLine();
            Console.WriteLine();

            Test2();
            Console.WriteLine();
            Console.WriteLine();

            TestAccumulator();
            Console.WriteLine();
            Console.WriteLine();

            TestShare();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("--Now delete rule of Test1, the TestShare should still works for the NewTestEvent rule");
            engine.DeleteRule("MaliciousScriptExec");
            TestShare();
*/
            TestSpeedAlarm();

            engine.DeleteRule("TestSpeedAlarm");
            engine.DeleteRule("ReportFiles");
            engine.DeleteRule("NewTestEvent");
            engine.DeleteRule("RegistryAlert");
        }
    }
}
