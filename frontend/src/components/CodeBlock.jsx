import React, { useState } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism';
import { Button } from './ui/button';
import { Copy, Check, Maximize2, Minimize2, Download, FileCode } from 'lucide-react';

/**
 * CodeBlock - Notepad++ style code display component
 * Features:
 * - Syntax highlighting
 * - Line numbers
 * - Copy to clipboard
 * - Expand to fullscreen modal
 * - Download option
 */
export function CodeBlock({ 
    code, 
    language = 'text', 
    filename = null,
    showLineNumbers = true,
    maxHeight = '300px'
}) {
    const [copied, setCopied] = useState(false);
    const [expanded, setExpanded] = useState(false);

    const handleCopy = async () => {
        await navigator.clipboard.writeText(code);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    const handleDownload = () => {
        const blob = new Blob([code], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename || `code.${getExtension(language)}`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };

    const getExtension = (lang) => {
        const extensions = {
            javascript: 'js',
            typescript: 'ts',
            python: 'py',
            java: 'java',
            csharp: 'cs',
            html: 'html',
            css: 'css',
            json: 'json',
            sql: 'sql',
            bash: 'sh',
            text: 'txt'
        };
        return extensions[lang] || lang;
    };

    const codeContent = (
        <div className="relative group">
            {/* Header bar - Notepad++ style */}
            <div className="flex items-center justify-between px-3 py-2 bg-[#21252b] border-b border-[#181a1f] rounded-t-lg">
                <div className="flex items-center gap-2">
                    <FileCode size={14} className="text-cyan-400" />
                    <span className="text-xs font-mono text-gray-400">
                        {filename || `snippet.${getExtension(language)}`}
                    </span>
                    <span className="text-xs text-gray-500 px-2 py-0.5 bg-gray-800 rounded">
                        {language}
                    </span>
                </div>
                <div className="flex items-center gap-1">
                    <Button
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6 text-gray-400 hover:text-white"
                        onClick={handleCopy}
                        title="Copy code"
                    >
                        {copied ? <Check size={14} className="text-green-400" /> : <Copy size={14} />}
                    </Button>
                    <Button
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6 text-gray-400 hover:text-white"
                        onClick={handleDownload}
                        title="Download file"
                    >
                        <Download size={14} />
                    </Button>
                    <Button
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6 text-gray-400 hover:text-white"
                        onClick={() => setExpanded(!expanded)}
                        title={expanded ? "Minimize" : "Expand"}
                    >
                        {expanded ? <Minimize2 size={14} /> : <Maximize2 size={14} />}
                    </Button>
                </div>
            </div>
            
            {/* Code content */}
            <div 
                className="overflow-auto rounded-b-lg"
                style={{ maxHeight: expanded ? 'none' : maxHeight }}
            >
                <SyntaxHighlighter
                    language={language}
                    style={oneDark}
                    showLineNumbers={showLineNumbers}
                    customStyle={{
                        margin: 0,
                        padding: '12px',
                        background: '#1e1e1e',
                        fontSize: '13px',
                        borderRadius: 0,
                        fontFamily: '"Fira Code", "Consolas", "Monaco", monospace'
                    }}
                    lineNumberStyle={{
                        minWidth: '3em',
                        paddingRight: '1em',
                        color: '#5c6370',
                        borderRight: '1px solid #3e4451',
                        marginRight: '1em'
                    }}
                >
                    {code}
                </SyntaxHighlighter>
            </div>
        </div>
    );

    // Expanded modal view
    if (expanded) {
        return (
            <div className="fixed inset-0 z-[100] bg-black/80 flex items-center justify-center p-8">
                <div className="w-full max-w-4xl max-h-[90vh] flex flex-col bg-[#282c34] rounded-lg shadow-2xl border border-gray-700">
                    {/* Modal header */}
                    <div className="flex items-center justify-between px-4 py-3 bg-[#21252b] border-b border-[#181a1f] rounded-t-lg">
                        <div className="flex items-center gap-2">
                            <FileCode size={16} className="text-cyan-400" />
                            <span className="text-sm font-mono text-gray-300">
                                {filename || `snippet.${getExtension(language)}`}
                            </span>
                            <span className="text-xs text-gray-500 px-2 py-1 bg-gray-800 rounded ml-2">
                                {language}
                            </span>
                        </div>
                        <div className="flex items-center gap-2">
                            <Button
                                variant="ghost"
                                size="sm"
                                className="h-8 text-gray-400 hover:text-white"
                                onClick={handleCopy}
                            >
                                {copied ? <Check size={14} className="mr-1 text-green-400" /> : <Copy size={14} className="mr-1" />}
                                {copied ? 'Copied!' : 'Copy'}
                            </Button>
                            <Button
                                variant="ghost"
                                size="sm"
                                className="h-8 text-gray-400 hover:text-white"
                                onClick={handleDownload}
                            >
                                <Download size={14} className="mr-1" />
                                Download
                            </Button>
                            <Button
                                variant="ghost"
                                size="sm"
                                className="h-8 text-gray-400 hover:text-white"
                                onClick={() => setExpanded(false)}
                            >
                                <Minimize2 size={14} className="mr-1" />
                                Close
                            </Button>
                        </div>
                    </div>
                    
                    {/* Modal content */}
                    <div className="flex-1 overflow-auto">
                        <SyntaxHighlighter
                            language={language}
                            style={oneDark}
                            showLineNumbers={showLineNumbers}
                            customStyle={{
                                margin: 0,
                                padding: '16px',
                                background: '#1e1e1e',
                                fontSize: '14px',
                                borderRadius: 0,
                                minHeight: '100%',
                                fontFamily: '"Fira Code", "Consolas", "Monaco", monospace'
                            }}
                            lineNumberStyle={{
                                minWidth: '3.5em',
                                paddingRight: '1em',
                                color: '#5c6370',
                                borderRight: '1px solid #3e4451',
                                marginRight: '1em'
                            }}
                        >
                            {code}
                        </SyntaxHighlighter>
                    </div>
                </div>
            </div>
        );
    }

    return codeContent;
}

/**
 * Parse message content and extract code blocks
 * Returns array of { type: 'text' | 'code', content, language?, filename? }
 */
export function parseMessageWithCode(content) {
    const parts = [];
    const codeBlockRegex = /```(\w+)?(?:\s+(\S+))?\n([\s\S]*?)```/g;
    let lastIndex = 0;
    let match;

    while ((match = codeBlockRegex.exec(content)) !== null) {
        // Add text before code block
        if (match.index > lastIndex) {
            const textBefore = content.slice(lastIndex, match.index).trim();
            if (textBefore) {
                parts.push({ type: 'text', content: textBefore });
            }
        }

        // Add code block
        parts.push({
            type: 'code',
            language: match[1] || 'text',
            filename: match[2] || null,
            content: match[3].trim()
        });

        lastIndex = match.index + match[0].length;
    }

    // Add remaining text
    if (lastIndex < content.length) {
        const remainingText = content.slice(lastIndex).trim();
        if (remainingText) {
            parts.push({ type: 'text', content: remainingText });
        }
    }

    // If no code blocks found, return original content as text
    if (parts.length === 0) {
        parts.push({ type: 'text', content });
    }

    return parts;
}

/**
 * MessageContent - Renders message with code blocks styled
 */
export function MessageContent({ content }) {
    // Handle null/undefined content
    if (!content) {
        return null;
    }
    
    const parts = parseMessageWithCode(content);
    
    // Safety check - ensure parts is an array
    if (!parts || !Array.isArray(parts) || parts.length === 0) {
        return <p className="text-sm whitespace-pre-wrap">{content}</p>;
    }

    return (
        <div className="space-y-3">
            {parts.map((part, index) => (
                part.type === 'code' ? (
                    <CodeBlock
                        key={index}
                        code={part.content}
                        language={part.language}
                        filename={part.filename}
                    />
                ) : (
                    <p key={index} className="text-sm whitespace-pre-wrap">{part.content}</p>
                )
            ))}
        </div>
    );
}

export default CodeBlock;
