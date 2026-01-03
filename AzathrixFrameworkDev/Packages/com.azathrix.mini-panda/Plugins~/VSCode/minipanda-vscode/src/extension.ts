import * as vscode from 'vscode';
import * as Net from 'net';
import { MiniPandaDebugSession } from './debugAdapter';
import {
    LanguageClient,
    LanguageClientOptions,
    StreamInfo,
    State
} from 'vscode-languageclient/node';

let languageClient: LanguageClient | null = null;
let retryTimer: NodeJS.Timeout | null = null;
let shouldRetry = true;
let activeSocket: Net.Socket | null = null;
let isStarting = false; // 防止重复启动
let startToken = 0; // 防止并发重连

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
    // 防止重复启动
    if (isStarting) {
        console.log('[MiniPanda] Already starting, skip');
        return;
    }

    // 先停止旧的客户端
    if (languageClient) {
        console.log('[MiniPanda] Stopping old client before restart');
        const oldClient = languageClient;
        languageClient = null;
        try {
            await oldClient.stop();
        } catch (e) {
            // 忽略停止错误
        }
    }

    isStarting = true;
    const myToken = ++startToken;
    currentContext = context;
    shouldRetry = true;

    const config = vscode.workspace.getConfiguration('minipanda');
    const port = config.get<number>('languageServer.port', 4712);
    const host = config.get<string>('languageServer.host', 'localhost');

    const serverOptions = async (): Promise<StreamInfo> => {
        // 重试连接
        while (shouldRetry && myToken === startToken) {
            try {
                return await createConnection(host, port);
            } catch (err) {
                console.log(`[MiniPanda] Connection failed, retrying in 3s...`);
                await new Promise(r => setTimeout(r, 3000));
            }
        }
        throw new Error('Connection cancelled');
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'minipanda' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.panda')
        },
        outputChannelName: 'MiniPanda Language Server',
        initializationFailedHandler: (error) => {
            console.log('[MiniPanda] Initialization failed:', error);
            return false;
        }
    };

    languageClient = new LanguageClient(
        'minipanda',
        'MiniPanda Language Server',
        serverOptions,
        clientOptions
    );

    // 监控客户端状态
    languageClient.onDidChangeState(e => {
        console.log(`[MiniPanda] Client state: ${State[e.oldState]} -> ${State[e.newState]}`);
        if (e.newState === State.Stopped && shouldRetry && !isStarting) {
            console.log('[MiniPanda] Language client stopped, restarting...');
            setTimeout(() => {
                if (shouldRetry && currentContext && !isStarting) {
                    startLanguageClient(currentContext);
                }
            }, 3000);
        }
    });

    console.log('[MiniPanda] Starting language client...');
    try {
        await languageClient.start();
        console.log('[MiniPanda] Language client started successfully, state:', State[languageClient.state]);
    } catch (err) {
        console.log('[MiniPanda] Failed to start language client:', err);
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







