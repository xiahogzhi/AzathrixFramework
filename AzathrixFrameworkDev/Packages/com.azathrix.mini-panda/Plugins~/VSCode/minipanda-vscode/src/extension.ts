import * as vscode from 'vscode';
import * as Net from 'net';
import { MiniPandaDebugSession } from './debugAdapter';
import {
    LanguageClient,
    LanguageClientOptions,
    StreamInfo,
    State,
    ErrorHandler,
    ErrorAction,
    CloseAction,
    Message,
    RevealOutputChannelOn
} from 'vscode-languageclient/node';

// 静默错误处理器
class SilentErrorHandler implements ErrorHandler {
    error(_error: Error, _message: Message | undefined, _count: number | undefined): ErrorHandlerResult | Promise<ErrorHandlerResult> {
        return { action: ErrorAction.Continue, handled: true };
    }
    closed(): CloseHandlerResult | Promise<CloseHandlerResult> {
        return { action: CloseAction.DoNotRestart, handled: true };
    }
}

type ErrorHandlerResult = { action: ErrorAction; message?: string; handled?: boolean };
type CloseHandlerResult = { action: CloseAction; message?: string; handled?: boolean };

let languageClient: LanguageClient | null = null;
let retryTimer: NodeJS.Timeout | null = null;
let shouldRetry = true;
let activeSocket: Net.Socket | null = null;
let isStarting = false;
let startToken = 0;
let outputChannel: vscode.OutputChannel | null = null;

export function activate(context: vscode.ExtensionContext) {
    console.log('[MiniPanda] Extension activating...');

    // 注册调试适配器工厂
    const factory = new MiniPandaDebugAdapterFactory();
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory('minipanda', factory)
    );
    console.log('[MiniPanda] Debug adapter factory registered');

    // 注册调试配置提供器
    context.subscriptions.push(
        vscode.debug.registerDebugConfigurationProvider('minipanda', new MiniPandaConfigurationProvider())
    );
    console.log('[MiniPanda] Debug configuration provider registered');

    // 注册表达式提供器（用于 hover 时获取完整的成员访问表达式）
    context.subscriptions.push(
        vscode.languages.registerEvaluatableExpressionProvider('minipanda', new MiniPandaEvaluatableExpressionProvider())
    );
    console.log('[MiniPanda] Evaluatable expression provider registered');

    // 启动语言客户端
    startLanguageClient(context);

    // 注册命令
    context.subscriptions.push(
        vscode.commands.registerCommand('minipanda.restartLanguageServer', () => {
            restartLanguageClient(context);
        })
    );
}

export function deactivate(): Thenable<void> | undefined {
    shouldRetry = false;
    if (retryTimer) {
        clearTimeout(retryTimer);
        retryTimer = null;
    }
    if (activeSocket) {
        activeSocket.destroy();
        activeSocket = null;
    }
    if (languageClient) {
        return languageClient.stop().catch(() => {});
    }
    return undefined;
}

function createConnection(host: string, port: number): Promise<StreamInfo> {
    return new Promise((resolve, reject) => {
        if (activeSocket) {
            activeSocket.destroy();
            activeSocket = null;
        }

        const socket = new Net.Socket();
        socket.setKeepAlive(true, 10000);
        socket.setNoDelay(true);

        const timeout = setTimeout(() => {
            socket.destroy();
            reject(new Error('Connection timeout'));
        }, 5000);

        socket.on('connect', () => {
            clearTimeout(timeout);
            activeSocket = socket;
            console.log(`[MiniPanda] Connected to LSP server at ${host}:${port}`);
            resolve({ reader: socket, writer: socket });
        });

        socket.on('error', (err) => {
            clearTimeout(timeout);
            socket.destroy();
            reject(err);
        });

        socket.on('close', () => {
            if (activeSocket === socket) {
                activeSocket = null;
            }
        });

        socket.connect(port, host);
    });
}

let currentContext: vscode.ExtensionContext | null = null;

async function startLanguageClient(context: vscode.ExtensionContext) {
    if (isStarting) return;

    if (languageClient) {
        const oldClient = languageClient;
        languageClient = null;
        try { await oldClient.stop(); } catch (e) { }
    }

    isStarting = true;
    const myToken = ++startToken;
    currentContext = context;
    shouldRetry = true;

    const config = vscode.workspace.getConfiguration('minipanda');
    const port = config.get<number>('languageServer.port', 4712);
    const host = config.get<string>('languageServer.host', 'localhost');

    const serverOptions = async (): Promise<StreamInfo> => {
        while (shouldRetry && myToken === startToken) {
            try {
                return await createConnection(host, port);
            } catch (err) {
                await new Promise(r => setTimeout(r, 3000));
            }
        }
        throw new Error('Connection cancelled');
    };

    // 复用 output channel
    if (!outputChannel) {
        outputChannel = vscode.window.createOutputChannel('MiniPanda Language Server');
    }

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'minipanda' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.panda')
        },
        outputChannel: outputChannel,
        revealOutputChannelOn: RevealOutputChannelOn.Never,
        initializationFailedHandler: () => false,
        errorHandler: new SilentErrorHandler()
    };

    languageClient = new LanguageClient(
        'minipanda',
        'MiniPanda Language Server',
        serverOptions,
        clientOptions
    );

    languageClient.onDidChangeState(e => {
        if (e.newState === State.Running) {
            vscode.window.showInformationMessage('[MiniPanda] Language server connected');
        }
        if (e.newState === State.Stopped && shouldRetry && !isStarting) {
            setTimeout(() => {
                if (shouldRetry && currentContext && !isStarting) {
                    startLanguageClient(currentContext);
                }
            }, 5000);
        }
    });

    try {
        await languageClient.start();
    } catch (err) {
        // 静默
    } finally {
        isStarting = false;
    }
}

function restartLanguageClientInternal() {
    if (!currentContext || !shouldRetry) return;

    if (languageClient) {
        languageClient.stop().catch(() => {}).finally(() => {
            languageClient = null;
            if (shouldRetry && currentContext) {
                startLanguageClient(currentContext);
            }
        });
    } else if (currentContext) {
        startLanguageClient(currentContext);
    }
}

function restartLanguageClient(context: vscode.ExtensionContext) {
    if (retryTimer) {
        clearTimeout(retryTimer);
        retryTimer = null;
    }
    shouldRetry = true;
    restartLanguageClientInternal();
}

class MiniPandaDebugAdapterFactory implements vscode.DebugAdapterDescriptorFactory {
    createDebugAdapterDescriptor(
        session: vscode.DebugSession,
        executable: vscode.DebugAdapterExecutable | undefined
    ): vscode.ProviderResult<vscode.DebugAdapterDescriptor> {
        return new vscode.DebugAdapterInlineImplementation(new MiniPandaDebugSession());
    }
}

class MiniPandaConfigurationProvider implements vscode.DebugConfigurationProvider {
    resolveDebugConfiguration(
        folder: vscode.WorkspaceFolder | undefined,
        config: vscode.DebugConfiguration,
        token?: vscode.CancellationToken
    ): vscode.ProviderResult<vscode.DebugConfiguration> {
        // 如果没有配置，创建默认配置
        if (!config.type && !config.request && !config.name) {
            config.type = 'minipanda';
            config.name = 'Debug MiniPanda';
            config.request = 'launch';
            config.stopOnEntry = false;
        }

        // 如果有活动的 .panda 文件，使用它作为 program
        if (!config.program) {
            const editor = vscode.window.activeTextEditor;
            if (editor && editor.document.languageId === 'minipanda') {
                config.program = editor.document.uri.fsPath;
            } else {
                // 允许没有 program 的情况（连接到 Unity 调试服务器）
                config.program = 'remote';
            }
        }

        return config;
    }
}

// 表达式提供器：用于 hover 时获取完整的成员访问表达式（如 config.debug）
class MiniPandaEvaluatableExpressionProvider implements vscode.EvaluatableExpressionProvider {
    provideEvaluatableExpression(
        document: vscode.TextDocument,
        position: vscode.Position,
        token: vscode.CancellationToken
    ): vscode.ProviderResult<vscode.EvaluatableExpression> {
        const line = document.lineAt(position.line).text;

        // 从光标位置向左右扩展，找到完整的标识符链（支持 a.b.c 格式）
        let start = position.character;
        let end = position.character;

        // 向左扩展
        while (start > 0) {
            const ch = line[start - 1];
            if (/[\w.]/.test(ch)) {
                start--;
            } else {
                break;
            }
        }

        // 向右扩展
        while (end < line.length) {
            const ch = line[end];
            if (/[\w]/.test(ch)) {
                end++;
            } else {
                break;
            }
        }

        // 提取表达式
        let expression = line.substring(start, end);

        // 去掉开头的点（如果有）
        if (expression.startsWith('.')) {
            expression = expression.substring(1);
            start++;
        }

        // 去掉结尾的点（如果有）
        if (expression.endsWith('.')) {
            expression = expression.substring(0, expression.length - 1);
            end--;
        }

        if (!expression || expression.length === 0) {
            return undefined;
        }

        const range = new vscode.Range(position.line, start, position.line, end);
        return new vscode.EvaluatableExpression(range, expression);
    }
}







