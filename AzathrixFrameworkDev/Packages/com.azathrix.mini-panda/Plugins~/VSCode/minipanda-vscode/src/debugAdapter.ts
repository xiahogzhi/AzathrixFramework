import {
    LoggingDebugSession,
    InitializedEvent,
    TerminatedEvent,
    StoppedEvent,
    OutputEvent,
    Thread,
    StackFrame,
    Scope,
    Source,
    Breakpoint
} from '@vscode/debugadapter';
import { DebugProtocol } from '@vscode/debugprotocol';
import * as Net from 'net';

interface LaunchRequestArguments extends DebugProtocol.LaunchRequestArguments {
    program: string;
    stopOnEntry?: boolean;
    cwd?: string;
    debugServer?: number;
}

interface AttachRequestArguments extends DebugProtocol.AttachRequestArguments {
    port: number;
    host?: string;
}

/**
 * MiniPanda 调试会话
 *
 * 这是一个客户端调试适配器，连接到 Unity 中运行的 MiniPanda DAP 服务器
 */
export class MiniPandaDebugSession extends LoggingDebugSession {
    private static THREAD_ID = 1;
    private socket: Net.Socket | null = null;
    private responseHandlers: Map<number, (response: any) => void> = new Map();
    private seq = 1;
    private breakpoints: Map<string, DebugProtocol.Breakpoint[]> = new Map();
    private pendingBreakpoints: Map<string, any[]> = new Map(); // 待发送的断点
    private configurationDoneSent = false;

    public constructor() {
        super('minipanda-debug.log');
        this.setDebuggerLinesStartAt1(true);
        this.setDebuggerColumnsStartAt1(true);
    }

    private log(msg: string): void {
        this.sendEvent(new OutputEvent(`[MiniPanda] ${msg}\n`, 'console'));
    }

    protected initializeRequest(
        response: DebugProtocol.InitializeResponse,
        args: DebugProtocol.InitializeRequestArguments
    ): void {
        // 立即发送日志
        this.sendEvent(new OutputEvent('[MiniPanda] Debug adapter initialized\n', 'console'));

        response.body = response.body || {};
        response.body.supportsConfigurationDoneRequest = true;
        response.body.supportsEvaluateForHovers = true;
        response.body.supportsConditionalBreakpoints = true;
        response.body.supportsSetVariable = false;
        response.body.supportsTerminateRequest = true;

        this.sendResponse(response);
        this.sendEvent(new InitializedEvent());
    }

    protected async launchRequest(
        response: DebugProtocol.LaunchResponse,
        args: LaunchRequestArguments
    ): Promise<void> {
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
                this.sendEvent(new StoppedEvent('entry', MiniPandaDebugSession.THREAD_ID));
            }
        } catch (err) {
            this.sendErrorResponse(response, 1, `Failed to connect to debug server: ${err}`);
        }
    }

    protected async attachRequest(
        response: DebugProtocol.AttachResponse,
        args: AttachRequestArguments
    ): Promise<void> {
        try {
            await this.connectToServer(args.host || 'localhost', args.port);
            this.sendResponse(response);
        } catch (err) {
            this.sendErrorResponse(response, 1, `Failed to attach: ${err}`);
        }
    }

    protected configurationDoneRequest(
        response: DebugProtocol.ConfigurationDoneResponse,
        args: DebugProtocol.ConfigurationDoneArguments
    ): void {
        this.configurationDoneSent = true;
        // 如果已连接，发送 configurationDone 到服务器
        if (this.socket) {
            this.sendToServer('configurationDone', {});
        }
        this.sendResponse(response);
    }

    protected async setBreakPointsRequest(
        response: DebugProtocol.SetBreakpointsResponse,
        args: DebugProtocol.SetBreakpointsArguments
    ): Promise<void> {
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

            const breakpoints = (serverResponse.breakpoints || []).map((bp: any) => {
                return new Breakpoint(bp.verified, bp.line);
            });

            response.body = { breakpoints };
        } else {
            // 缓存断点，等连接后发送
            this.pendingBreakpoints.set(path, bpData);
            const breakpoints = clientLines.map(bp => new Breakpoint(true, bp.line));
            response.body = { breakpoints };
        }

        this.breakpoints.set(path, response.body.breakpoints);
        this.sendResponse(response);
    }

    protected threadsRequest(response: DebugProtocol.ThreadsResponse): void {
        response.body = {
            threads: [new Thread(MiniPandaDebugSession.THREAD_ID, 'Main Thread')]
        };
        this.sendResponse(response);
    }

    protected async stackTraceRequest(
        response: DebugProtocol.StackTraceResponse,
        args: DebugProtocol.StackTraceArguments
    ): Promise<void> {
        if (this.socket) {
            const serverResponse = await this.sendToServer('stackTrace', {
                threadId: args.threadId
            });

            const frames = (serverResponse.stackFrames || []).map((f: any, i: number) => {
                return new StackFrame(
                    f.id,
                    f.name,
                    new Source(f.source?.name || '', f.source?.path || ''),
                    f.line,
                    f.column
                );
            });

            response.body = {
                stackFrames: frames,
                totalFrames: frames.length
            };
        } else {
            response.body = { stackFrames: [], totalFrames: 0 };
        }

        this.sendResponse(response);
    }

    protected async scopesRequest(
        response: DebugProtocol.ScopesResponse,
        args: DebugProtocol.ScopesArguments
    ): Promise<void> {
        if (this.socket) {
            const serverResponse = await this.sendToServer('scopes', {
                frameId: args.frameId
            });

            const scopes = (serverResponse.scopes || []).map((s: any) => {
                return new Scope(s.name, s.variablesReference, s.expensive);
            });

            response.body = { scopes };
        } else {
            response.body = { scopes: [] };
        }

        this.sendResponse(response);
    }

    protected async variablesRequest(
        response: DebugProtocol.VariablesResponse,
        args: DebugProtocol.VariablesArguments
    ): Promise<void> {
        if (this.socket) {
            const serverResponse = await this.sendToServer('variables', {
                variablesReference: args.variablesReference
            });

            response.body = {
                variables: serverResponse.variables || []
            };
        } else {
            response.body = { variables: [] };
        }

        this.sendResponse(response);
    }

    protected async evaluateRequest(
        response: DebugProtocol.EvaluateResponse,
        args: DebugProtocol.EvaluateArguments
    ): Promise<void> {
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
            } catch (err) {
                response.body = {
                    result: `Error: ${err}`,
                    variablesReference: 0
                };
            }
        } else {
            response.body = {
                result: 'Not connected',
                variablesReference: 0
            };
        }

        this.sendResponse(response);
    }

    protected continueRequest(
        response: DebugProtocol.ContinueResponse,
        args: DebugProtocol.ContinueArguments
    ): void {
        this.sendToServer('continue', { threadId: args.threadId });
        response.body = { allThreadsContinued: true };
        this.sendResponse(response);
    }

    protected nextRequest(
        response: DebugProtocol.NextResponse,
        args: DebugProtocol.NextArguments
    ): void {
        this.sendToServer('next', { threadId: args.threadId });
        this.sendResponse(response);
    }

    protected stepInRequest(
        response: DebugProtocol.StepInResponse,
        args: DebugProtocol.StepInArguments
    ): void {
        this.sendToServer('stepIn', { threadId: args.threadId });
        this.sendResponse(response);
    }

    protected stepOutRequest(
        response: DebugProtocol.StepOutResponse,
        args: DebugProtocol.StepOutArguments
    ): void {
        this.sendToServer('stepOut', { threadId: args.threadId });
        this.sendResponse(response);
    }

    protected pauseRequest(
        response: DebugProtocol.PauseResponse,
        args: DebugProtocol.PauseArguments
    ): void {
        this.sendToServer('pause', { threadId: args.threadId });
        this.sendResponse(response);
    }

    protected disconnectRequest(
        response: DebugProtocol.DisconnectResponse,
        args: DebugProtocol.DisconnectArguments
    ): void {
        if (this.socket) {
            this.sendToServer('disconnect', {});
            this.socket.destroy();
            this.socket = null;
        }
        this.sendResponse(response);
    }

    protected terminateRequest(
        response: DebugProtocol.TerminateResponse,
        args: DebugProtocol.TerminateArguments
    ): void {
        if (this.socket) {
            this.sendToServer('terminate', {});
        }
        this.sendResponse(response);
        this.sendEvent(new TerminatedEvent());
    }

    // 连接到 DAP 服务器
    private connectToServer(host: string, port: number): Promise<void> {
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
                this.sendEvent(new TerminatedEvent());
            });

            this.socket.connect(port, host);
        });
    }

    // 发送请求到服务器
    private sendToServer(command: string, args: any): Promise<any> {
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

    // 处理服务器数据（按字节解析 Content-Length）
    private buffer = Buffer.alloc(0);
    private handleServerData(data: Buffer): void {
        this.buffer = Buffer.concat([this.buffer, data]);

        while (true) {
            const headerEnd = this.buffer.indexOf('\r\n\r\n');
            if (headerEnd < 0) break;

            const header = this.buffer.slice(0, headerEnd).toString('ascii');
            const match = header.match(/Content-Length:\s*(\d+)/i);
            if (!match) {
                this.buffer = this.buffer.slice(headerEnd + 4);
                continue;
            }

            const contentLength = parseInt(match[1], 10);
            const contentStart = headerEnd + 4;
            if (this.buffer.length < contentStart + contentLength) break;

            const contentBuf = this.buffer.slice(contentStart, contentStart + contentLength);
            this.buffer = this.buffer.slice(contentStart + contentLength);

            try {
                const message = JSON.parse(contentBuf.toString('utf8'));
                this.handleServerMessage(message);
            } catch (e) {
                // 忽略解析错误
            }
        }
    }

    // 处理服务器消息
    private handleServerMessage(message: any): void {
        if (message.type === 'response') {
            const handler = this.responseHandlers.get(message.request_seq);
            if (handler) {
                this.responseHandlers.delete(message.request_seq);
                handler(message.body || {});
            }
        } else if (message.type === 'event') {
            this.handleServerEvent(message);
        }
    }

    // 处理服务器事件
    private handleServerEvent(event: any): void {
        switch (event.event) {
            case 'stopped':
                this.sendEvent(new StoppedEvent(
                    event.body?.reason || 'breakpoint',
                    event.body?.threadId || MiniPandaDebugSession.THREAD_ID
                ));
                break;
            case 'output':
                this.sendEvent(new OutputEvent(
                    event.body?.output || '',
                    event.body?.category || 'console'
                ));
                break;
            case 'terminated':
                this.sendEvent(new TerminatedEvent());
                break;
        }
    }
}




