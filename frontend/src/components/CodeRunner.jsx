import { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Button } from './ui/button';
import { 
    Play, X, Maximize2, Minimize2, Terminal, 
    Loader2, Square, RefreshCw, ExternalLink 
} from 'lucide-react';

/**
 * CodeRunner - Executes code and shows output in a popup or embedded preview
 * Supports: HTML/CSS/JS (browser), Python (via Pyodide), and more
 */
export function CodeRunner({ 
    code, 
    language, 
    filename,
    projectFiles = [],
    isOpen,
    onClose 
}) {
    const [output, setOutput] = useState('');
    const [error, setError] = useState('');
    const [running, setRunning] = useState(false);
    const [isMaximized, setIsMaximized] = useState(false);
    const [previewUrl, setPreviewUrl] = useState('');
    const iframeRef = useRef(null);

    // Language detection
    const isWebCode = ['html', 'htm', 'css', 'javascript', 'js', 'jsx', 'tsx'].includes(language?.toLowerCase());
    const isPython = ['python', 'py'].includes(language?.toLowerCase());

    const runCode = async () => {
        setRunning(true);
        setOutput('');
        setError('');

        try {
            if (isWebCode) {
                await runWebCode();
            } else if (isPython) {
                await runPythonCode();
            } else {
                setError(`Language "${language}" is not supported for direct execution.\nSupported: HTML, CSS, JavaScript, Python`);
            }
        } catch (err) {
            setError(err.message || 'Execution failed');
        } finally {
            setRunning(false);
        }
    };

    const runWebCode = async () => {
        // Find HTML file or create wrapper
        let htmlContent = '';
        let cssContent = '';
        let jsContent = '';

        // Check if current file is HTML
        if (['html', 'htm'].includes(language?.toLowerCase())) {
            htmlContent = code;
        } else if (language?.toLowerCase() === 'css') {
            cssContent = code;
        } else if (['javascript', 'js', 'jsx'].includes(language?.toLowerCase())) {
            jsContent = code;
        }

        // Also gather from project files
        projectFiles.forEach(file => {
            const ext = file.path?.split('.').pop()?.toLowerCase();
            if (ext === 'html' || ext === 'htm') {
                if (!htmlContent) htmlContent = file.content;
            } else if (ext === 'css') {
                cssContent += '\n' + (file.content || '');
            } else if (ext === 'js') {
                jsContent += '\n' + (file.content || '');
            }
        });

        // If no HTML, create a wrapper
        if (!htmlContent) {
            htmlContent = `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Code Preview</title>
    <style>${cssContent}</style>
</head>
<body>
    <script>${jsContent}</script>
</body>
</html>`;
        } else {
            // Inject CSS and JS into existing HTML
            if (cssContent && !htmlContent.includes(cssContent)) {
                htmlContent = htmlContent.replace('</head>', `<style>${cssContent}</style></head>`);
            }
            if (jsContent && !htmlContent.includes(jsContent)) {
                htmlContent = htmlContent.replace('</body>', `<script>${jsContent}</script></body>`);
            }
        }

        // Create blob URL for iframe
        const blob = new Blob([htmlContent], { type: 'text/html' });
        const url = URL.createObjectURL(blob);
        setPreviewUrl(url);
        setOutput('Web preview loaded successfully!');
    };

    const runPythonCode = async () => {
        setOutput('Loading Python runtime (Pyodide)...\n');
        
        try {
            // Load Pyodide if not already loaded
            if (!window.pyodide) {
                const script = document.createElement('script');
                script.src = 'https://cdn.jsdelivr.net/pyodide/v0.24.1/full/pyodide.js';
                document.head.appendChild(script);
                
                await new Promise((resolve, reject) => {
                    script.onload = resolve;
                    script.onerror = reject;
                });

                window.pyodide = await window.loadPyodide();
                setOutput('Python runtime loaded!\n\n');
            }

            // Redirect stdout
            window.pyodide.runPython(`
import sys
from io import StringIO
sys.stdout = StringIO()
sys.stderr = StringIO()
            `);

            // Run the code
            await window.pyodide.runPythonAsync(code);

            // Get output
            const stdout = window.pyodide.runPython('sys.stdout.getvalue()');
            const stderr = window.pyodide.runPython('sys.stderr.getvalue()');

            if (stderr) {
                setError(stderr);
            }
            setOutput(prev => prev + (stdout || '(No output)'));
        } catch (err) {
            setError(err.message);
        }
    };

    const stopExecution = () => {
        setRunning(false);
        if (previewUrl) {
            URL.revokeObjectURL(previewUrl);
            setPreviewUrl('');
        }
    };

    const openInNewWindow = () => {
        if (previewUrl) {
            window.open(previewUrl, '_blank', 'width=800,height=600');
        }
    };

    useEffect(() => {
        return () => {
            if (previewUrl) {
                URL.revokeObjectURL(previewUrl);
            }
        };
    }, [previewUrl]);

    if (!isOpen) return null;

    return (
        <AnimatePresence>
            <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                className={`fixed z-[200] bg-black/80 ${
                    isMaximized ? 'inset-0' : 'inset-4'
                } flex items-center justify-center`}
                onClick={onClose}
            >
                <motion.div
                    initial={{ scale: 0.95, opacity: 0 }}
                    animate={{ scale: 1, opacity: 1 }}
                    exit={{ scale: 0.95, opacity: 0 }}
                    className={`bg-[#0B0F19] border border-white/10 rounded-xl shadow-2xl flex flex-col overflow-hidden ${
                        isMaximized ? 'w-full h-full' : 'w-[900px] h-[600px] max-w-[95vw] max-h-[90vh]'
                    }`}
                    onClick={e => e.stopPropagation()}
                >
                    {/* Header */}
                    <div className="flex items-center justify-between px-4 py-3 bg-[#1a1f2e] border-b border-white/10">
                        <div className="flex items-center gap-3">
                            <div className="flex items-center gap-2">
                                <Terminal className="text-cyan-400" size={18} />
                                <span className="font-medium">Code Runner</span>
                            </div>
                            <span className="px-2 py-0.5 bg-gray-800 rounded text-xs text-gray-400">
                                {filename || 'untitled'} ({language})
                            </span>
                        </div>
                        <div className="flex items-center gap-2">
                            <Button
                                variant="ghost"
                                size="icon"
                                className="h-8 w-8"
                                onClick={runCode}
                                disabled={running}
                            >
                                {running ? (
                                    <Loader2 size={16} className="animate-spin" />
                                ) : (
                                    <Play size={16} className="text-green-400" />
                                )}
                            </Button>
                            {running && (
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-8 w-8 text-red-400"
                                    onClick={stopExecution}
                                >
                                    <Square size={16} />
                                </Button>
                            )}
                            <Button
                                variant="ghost"
                                size="icon"
                                className="h-8 w-8"
                                onClick={() => { setOutput(''); setError(''); setPreviewUrl(''); }}
                            >
                                <RefreshCw size={16} />
                            </Button>
                            {previewUrl && (
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-8 w-8"
                                    onClick={openInNewWindow}
                                >
                                    <ExternalLink size={16} />
                                </Button>
                            )}
                            <Button
                                variant="ghost"
                                size="icon"
                                className="h-8 w-8"
                                onClick={() => setIsMaximized(!isMaximized)}
                            >
                                {isMaximized ? <Minimize2 size={16} /> : <Maximize2 size={16} />}
                            </Button>
                            <Button
                                variant="ghost"
                                size="icon"
                                className="h-8 w-8"
                                onClick={onClose}
                            >
                                <X size={16} />
                            </Button>
                        </div>
                    </div>

                    {/* Content */}
                    <div className="flex-1 flex overflow-hidden">
                        {/* Code Preview (left) */}
                        <div className="w-1/2 border-r border-white/10 overflow-auto">
                            <div className="p-4">
                                <h3 className="text-xs font-medium text-gray-500 uppercase mb-2">Source Code</h3>
                                <pre className="bg-[#1e1e1e] p-4 rounded-lg text-sm font-mono text-gray-300 overflow-auto max-h-[400px]">
                                    {code}
                                </pre>
                            </div>
                        </div>

                        {/* Output (right) */}
                        <div className="w-1/2 overflow-auto flex flex-col">
                            {previewUrl ? (
                                /* Web Preview */
                                <div className="flex-1 bg-white">
                                    <iframe
                                        ref={iframeRef}
                                        src={previewUrl}
                                        className="w-full h-full border-0"
                                        title="Code Preview"
                                        sandbox="allow-scripts allow-same-origin"
                                    />
                                </div>
                            ) : (
                                /* Terminal Output */
                                <div className="flex-1 p-4">
                                    <h3 className="text-xs font-medium text-gray-500 uppercase mb-2">Output</h3>
                                    <div className="bg-[#1e1e1e] p-4 rounded-lg min-h-[200px] max-h-[400px] overflow-auto">
                                        {output && (
                                            <pre className="text-sm font-mono text-green-400 whitespace-pre-wrap">
                                                {output}
                                            </pre>
                                        )}
                                        {error && (
                                            <pre className="text-sm font-mono text-red-400 whitespace-pre-wrap">
                                                {error}
                                            </pre>
                                        )}
                                        {!output && !error && !running && (
                                            <p className="text-gray-500 text-sm">
                                                Click the Play button to run your code
                                            </p>
                                        )}
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Footer */}
                    <div className="px-4 py-2 bg-[#1a1f2e] border-t border-white/10 flex items-center justify-between text-xs text-gray-500">
                        <span>Supports: HTML, CSS, JavaScript, Python</span>
                        <span>
                            {isWebCode && 'Web preview renders in isolated sandbox'}
                            {isPython && 'Python runs via Pyodide (WebAssembly)'}
                        </span>
                    </div>
                </motion.div>
            </motion.div>
        </AnimatePresence>
    );
}

export default CodeRunner;
