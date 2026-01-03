using System.Collections.Generic;

namespace Azathrix.MiniPanda.Debug.DAP
{
    /// <summary>
    /// DAP 协议基础消息
    /// </summary>
    public abstract class ProtocolMessage
    {
        public int seq { get; set; }
        public string type { get; set; }
    }

    /// <summary>
    /// DAP 请求消息
    /// </summary>
    public class Request : ProtocolMessage
    {
        public string command { get; set; }
        public Dictionary<string, object> arguments { get; set; }

        public Request() { type = "request"; }
    }

    /// <summary>
    /// DAP 响应消息
    /// </summary>
    public class Response : ProtocolMessage
    {
        public int request_seq { get; set; }
        public bool success { get; set; }
        public string command { get; set; }
        public string message { get; set; }
        public object body { get; set; }

        public Response() { type = "response"; }
    }

    /// <summary>
    /// DAP 事件消息
    /// </summary>
    public class Event : ProtocolMessage
    {
        public string @event { get; set; }
        public object body { get; set; }

        public Event() { type = "event"; }
    }

    #region 请求参数

    public class InitializeRequestArguments
    {
        public string clientID { get; set; }
        public string clientName { get; set; }
        public string adapterID { get; set; }
        public bool linesStartAt1 { get; set; } = true;
        public bool columnsStartAt1 { get; set; } = true;
        public string pathFormat { get; set; } = "path";
    }

    public class LaunchRequestArguments
    {
        public string program { get; set; }
        public string[] args { get; set; }
        public string cwd { get; set; }
        public bool stopOnEntry { get; set; }
        public bool noDebug { get; set; }
    }

    public class AttachRequestArguments
    {
        public int port { get; set; }
        public string host { get; set; }
    }

    public class SetBreakpointsArguments
    {
        public Source source { get; set; }
        public SourceBreakpoint[] breakpoints { get; set; }
    }

    public class StackTraceArguments
    {
        public int threadId { get; set; }
        public int? startFrame { get; set; }
        public int? levels { get; set; }
    }

    public class ScopesArguments
    {
        public int frameId { get; set; }
    }

    public class VariablesArguments
    {
        public int variablesReference { get; set; }
        public string filter { get; set; }
        public int? start { get; set; }
        public int? count { get; set; }
    }

    public class EvaluateArguments
    {
        public string expression { get; set; }
        public int? frameId { get; set; }
        public string context { get; set; }
    }

    public class ContinueArguments
    {
        public int threadId { get; set; }
    }

    public class NextArguments
    {
        public int threadId { get; set; }
    }

    public class StepInArguments
    {
        public int threadId { get; set; }
    }

    public class StepOutArguments
    {
        public int threadId { get; set; }
    }

    #endregion

    #region 响应体

    public class Capabilities
    {
        public bool supportsConfigurationDoneRequest { get; set; } = true;
        public bool supportsEvaluateForHovers { get; set; } = true;
        public bool supportsSetVariable { get; set; } = false;
        public bool supportsConditionalBreakpoints { get; set; } = true;
        public bool supportsHitConditionalBreakpoints { get; set; } = false;
        public bool supportsFunctionBreakpoints { get; set; } = false;
        public bool supportsStepBack { get; set; } = false;
        public bool supportsRestartFrame { get; set; } = false;
        public bool supportsGotoTargetsRequest { get; set; } = false;
        public bool supportsStepInTargetsRequest { get; set; } = false;
        public bool supportsCompletionsRequest { get; set; } = false;
        public bool supportsModulesRequest { get; set; } = false;
        public bool supportsExceptionOptions { get; set; } = false;
        public bool supportsTerminateRequest { get; set; } = true;
    }

    public class SetBreakpointsResponseBody
    {
        public DAP.Breakpoint[] breakpoints { get; set; }
    }

    public class StackTraceResponseBody
    {
        public StackFrame[] stackFrames { get; set; }
        public int totalFrames { get; set; }
    }

    public class ScopesResponseBody
    {
        public Scope[] scopes { get; set; }
    }

    public class VariablesResponseBody
    {
        public Variable[] variables { get; set; }
    }

    public class EvaluateResponseBody
    {
        public string result { get; set; }
        public string type { get; set; }
        public int variablesReference { get; set; }
    }

    public class ContinueResponseBody
    {
        public bool allThreadsContinued { get; set; } = true;
    }

    public class ThreadsResponseBody
    {
        public Thread[] threads { get; set; }
    }

    #endregion

    #region 事件体

    public class StoppedEventBody
    {
        public string reason { get; set; }
        public int threadId { get; set; } = 1;
        public string description { get; set; }
        public string text { get; set; }
        public bool allThreadsStopped { get; set; } = true;
    }

    public class OutputEventBody
    {
        public string category { get; set; } = "console";
        public string output { get; set; }
        public Source source { get; set; }
        public int? line { get; set; }
        public int? column { get; set; }
    }

    public class TerminatedEventBody
    {
        public bool restart { get; set; }
    }

    #endregion

    #region 数据类型

    public class Source
    {
        public string name { get; set; }
        public string path { get; set; }
        public int? sourceReference { get; set; }
    }

    public class SourceBreakpoint
    {
        public int line { get; set; }
        public int? column { get; set; }
        public string condition { get; set; }
        public string hitCondition { get; set; }
        public string logMessage { get; set; }
    }

    public class Breakpoint
    {
        public int id { get; set; }
        public bool verified { get; set; }
        public string message { get; set; }
        public Source source { get; set; }
        public int? line { get; set; }
        public int? column { get; set; }
    }

    public class StackFrame
    {
        public int id { get; set; }
        public string name { get; set; }
        public Source source { get; set; }
        public int line { get; set; }
        public int column { get; set; }
    }

    public class Scope
    {
        public string name { get; set; }
        public int variablesReference { get; set; }
        public bool expensive { get; set; }
    }

    public class Variable
    {
        public string name { get; set; }
        public string value { get; set; }
        public string type { get; set; }
        public int variablesReference { get; set; }
    }

    public class Thread
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    #endregion
}
