"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.MiniPandaDebugSession = void 0;
const debugadapter_1 = require("@vscode/debugadapter");
const Net = __importStar(require("net"));
/**
 * MiniPanda 调试会话
 *
 * 这是一个客户端调试适配器，连接到 Unity 中运行的 MiniPanda DAP 服务器
 */
class MiniPandaDebugSession extends debugadapter_1.LoggingDebugSession {
    constructor() {
        super('minipanda-debug.log');
        this.socket = null;
        this.responseHandlers = new Map();
        this.seq = 1;
        this.breakpoints = new Map();
        this.pendingBreakpoints = new Map(); // 待发送的断点
        this.configurationDoneSent = false;
        // 处理服务器数据（按字节解析 Content-Length）
        this.buffer = Buffer.alloc(0);
        this.setDebuggerLinesStartAt1(true);
        this.setDebuggerColumnsStartAt1(true);
    }
    log(msg) {
        this.sendEvent(new debugadapter_1.OutputEvent(`[MiniPanda] ${msg}\n`, 'console'));
    }
    initializeRequest(response, args) {
        // 立即发送日志
        this.sendEvent(new debugadapter_1.OutputEvent('[MiniPanda] Debug adapter initialized\n', 'console'));
        response.body = response.body || {};
        response.body.supportsConfigurationDoneRequest = true;
        response.body.supportsEvaluateForHovers = true;
        response.body.supportsConditionalBreakpoints = true;
        response.body.supportsSetVariable = false;
        response.body.supportsTerminateRequest = true;
        this.sendResponse(response);
        this.sendEvent(new debugadapter_1.InitializedEvent());
    }
    async launchRequest(response, args) {
        // 连接到 Unity 中的 DAP 服务器
        const port = args.debugServer || 4711;
        try {
            await this.connectToServer('localhost', port);
            this.log('Connected to server');
            // 发送 initialize 到服务器
            await this.sendToServer('initialize', {});
            this.log('Sent initialize');
            // 发送之前缓存的断点
            this.log(`Pending breakpoints: ${this.pendingBreakpoints.size}`);
            for (const [path, bps] of this.pendingBreakpoints) {
                this.log(`Sending breakpoints for ${path}: ${JSON.stringify(bps)}`);
                await this.sendToServer('setBreakpoints', {
                    source: { path },
                    breakpoints: bps
                });
            }
            this.pendingBreakpoints.clear();
            // 发送 configurationDone
            if (this.configurationDoneSent) {
                this.log('Sending configurationDone');
                await this.sendToServer('configurationDone', {});
            }
            // 发送 launch 请求到服务器
            await this.sendToServer('launch', {
                program: args.program,
                stopOnEntry: args.stopOnEntry || false,
                cwd: args.cwd
            });
            this.log('Sent launch');
            this.sendResponse(response);
            if (args.stopOnEntry) {
                this.sendEvent(new debugadapter_1.StoppedEvent('entry', MiniPandaDebugSession.THREAD_ID));
            }
        }
        catch (err) {
            this.sendErrorResponse(response, 1, `Failed to connect to debug server: ${err}`);
        }
    }
    async attachRequest(response, args) {
        try {
            await this.connectToServer(args.host || 'localhost', args.port);
            this.sendResponse(response);
        }
        catch (err) {
            this.sendErrorResponse(response, 1, `Failed to attach: ${err}`);
        }
    }
    configurationDoneRequest(response, args) {
        this.configurationDoneSent = true;
        // 如果已连接，发送 configurationDone 到服务器
        if (this.socket) {
            this.sendToServer('configurationDone', {});
        }
        this.sendResponse(response);
    }
    async setBreakPointsRequest(response, args) {
        const path = args.source.path || '';
        const clientLines = args.breakpoints || [];
        const bpData = clientLines.map(bp => ({
            line: bp.line,
            condition: bp.condition
        }));
        console.log(`[MiniPanda DA] setBreakPointsRequest: path=${path}, socket=${!!this.socket}, bps=${JSON.stringify(bpData)}`);
        this.log(`setBreakPointsRequest: path=${path}, socket=${!!this.socket}, bps=${JSON.stringify(bpData)}`);
        // 发送断点到服务器
        if (this.socket) {
            const serverResponse = await this.sendToServer('setBreakpoints', {
                source: { path },
                breakpoints: bpData
            });
            const breakpoints = (serverResponse.breakpoints || []).map((bp) => {
                return new debugadapter_1.Breakpoint(bp.verified, bp.line);
            });
            response.body = { breakpoints };
        }
        else {
            // 缓存断点，等连接后发送
            this.pendingBreakpoints.set(path, bpData);
            const breakpoints = clientLines.map(bp => new debugadapter_1.Breakpoint(true, bp.line));
            response.body = { breakpoints };
        }
        this.breakpoints.set(path, response.body.breakpoints);
        this.sendResponse(response);
    }
    threadsRequest(response) {
        response.body = {
            threads: [new debugadapter_1.Thread(MiniPandaDebugSession.THREAD_ID, 'Main Thread')]
        };
        this.sendResponse(response);
    }
    async stackTraceRequest(response, args) {
        if (this.socket) {
            const serverResponse = await this.sendToServer('stackTrace', {
                threadId: args.threadId
            });
            const frames = (serverResponse.stackFrames || []).map((f, i) => {
                return new debugadapter_1.StackFrame(f.id, f.name, new debugadapter_1.Source(f.source?.name || '', f.source?.path || ''), f.line, f.column);
            });
            response.body = {
                stackFrames: frames,
                totalFrames: frames.length
            };
        }
        else {
            response.body = { stackFrames: [], totalFrames: 0 };
        }
        this.sendResponse(response);
    }
    async scopesRequest(response, args) {
        if (this.socket) {
            const serverResponse = await this.sendToServer('scopes', {
                frameId: args.frameId
            });
            const scopes = (serverResponse.scopes || []).map((s) => {
                return new debugadapter_1.Scope(s.name, s.variablesReference, s.expensive);
            });
            response.body = { scopes };
        }
        else {
            response.body = { scopes: [] };
        }
        this.sendResponse(response);
    }
    async variablesRequest(response, args) {
        if (this.socket) {
            const serverResponse = await this.sendToServer('variables', {
                variablesReference: args.variablesReference
            });
            response.body = {
                variables: serverResponse.variables || []
            };
        }
        else {
            response.body = { variables: [] };
        }
        this.sendResponse(response);
    }
    async evaluateRequest(response, args) {
        if (this.socket) {
            try {
                const serverResponse = await this.sendToServer('evaluate', {
                    expression: args.expression,
                    frameId: args.frameId,
                    context: args.context
                });
                response.body = {
                    result: serverResponse.result || '',
                    type: serverResponse.type,
                    variablesReference: serverResponse.variablesReference || 0
                };
            }
            catch (err) {
                response.body = {
                    result: `Error: ${err}`,
                    variablesReference: 0
                };
            }
        }
        else {
            response.body = {
                result: 'Not connected',
                variablesReference: 0
            };
        }
        this.sendResponse(response);
    }
    continueRequest(response, args) {
        this.sendToServer('continue', { threadId: args.threadId });
        response.body = { allThreadsContinued: true };
        this.sendResponse(response);
    }
    nextRequest(response, args) {
        this.sendToServer('next', { threadId: args.threadId });
        this.sendResponse(response);
    }
    stepInRequest(response, args) {
        this.sendToServer('stepIn', { threadId: args.threadId });
        this.sendResponse(response);
    }
    stepOutRequest(response, args) {
        this.sendToServer('stepOut', { threadId: args.threadId });
        this.sendResponse(response);
    }
    pauseRequest(response, args) {
        this.sendToServer('pause', { threadId: args.threadId });
        this.sendResponse(response);
    }
    disconnectRequest(response, args) {
        if (this.socket) {
            this.sendToServer('disconnect', {});
            this.socket.destroy();
            this.socket = null;
        }
        this.sendResponse(response);
    }
    terminateRequest(response, args) {
        if (this.socket) {
            this.sendToServer('terminate', {});
        }
        this.sendResponse(response);
        this.sendEvent(new debugadapter_1.TerminatedEvent());
    }
    // 连接到 DAP 服务器
    connectToServer(host, port) {
        return new Promise((resolve, reject) => {
            this.socket = new Net.Socket();
            this.socket.on('connect', () => {
                resolve();
            });
            this.socket.on('error', (err) => {
                reject(err);
            });
            this.socket.on('data', (data) => {
                this.handleServerData(data);
            });
            this.socket.on('close', () => {
                this.sendEvent(new debugadapter_1.TerminatedEvent());
            });
            this.socket.connect(port, host);
        });
    }
    // 发送请求到服务器
    sendToServer(command, args) {
        return new Promise((resolve, reject) => {
            if (!this.socket) {
                reject(new Error('Not connected'));
                return;
            }
            const seq = this.seq++;
            const request = {
                seq,
                type: 'request',
                command,
                arguments: args
            };
            this.responseHandlers.set(seq, resolve);
            const json = JSON.stringify(request);
            const header = `Content-Length: ${Buffer.byteLength(json)}\r\n\r\n`;
            this.socket.write(header + json);
            // 超时处理
            setTimeout(() => {
                if (this.responseHandlers.has(seq)) {
                    this.responseHandlers.delete(seq);
                    reject(new Error('Request timeout'));
                }
            }, 10000);
        });
    }
    handleServerData(data) {
        this.buffer = Buffer.concat([this.buffer, data]);
        while (true) {
            const headerEnd = this.buffer.indexOf('\r\n\r\n');
            if (headerEnd < 0)
                break;
            const header = this.buffer.slice(0, headerEnd).toString('ascii');
            const match = header.match(/Content-Length:\s*(\d+)/i);
            if (!match) {
                this.buffer = this.buffer.slice(headerEnd + 4);
                continue;
            }
            const contentLength = parseInt(match[1], 10);
            const contentStart = headerEnd + 4;
            if (this.buffer.length < contentStart + contentLength)
                break;
            const contentBuf = this.buffer.slice(contentStart, contentStart + contentLength);
            this.buffer = this.buffer.slice(contentStart + contentLength);
            try {
                const message = JSON.parse(contentBuf.toString('utf8'));
                this.handleServerMessage(message);
            }
            catch (e) {
                // 忽略解析错误
            }
        }
    }
    // 处理服务器消息
    handleServerMessage(message) {
        if (message.type === 'response') {
            const handler = this.responseHandlers.get(message.request_seq);
            if (handler) {
                this.responseHandlers.delete(message.request_seq);
                handler(message.body || {});
            }
        }
        else if (message.type === 'event') {
            this.handleServerEvent(message);
        }
    }
    // 处理服务器事件
    handleServerEvent(event) {
        switch (event.event) {
            case 'stopped':
                this.sendEvent(new debugadapter_1.StoppedEvent(event.body?.reason || 'breakpoint', event.body?.threadId || MiniPandaDebugSession.THREAD_ID));
                break;
            case 'output':
                this.sendEvent(new debugadapter_1.OutputEvent(event.body?.output || '', event.body?.category || 'console'));
                break;
            case 'terminated':
                this.sendEvent(new debugadapter_1.TerminatedEvent());
                break;
        }
    }
}
exports.MiniPandaDebugSession = MiniPandaDebugSession;
MiniPandaDebugSession.THREAD_ID = 1;
//# sourceMappingURL=debugAdapter.js.map