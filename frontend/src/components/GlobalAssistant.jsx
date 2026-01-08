import { useState, useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAuth } from '../lib/auth';
import api from '../lib/api';
import { formatCredits } from '../lib/utils';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { ScrollArea } from './ui/scroll-area';
import { 
    MessageSquare, X, Send, Plus, Trash2, ChevronLeft, 
    Zap, Bot, User, History, Loader2, GripVertical
} from 'lucide-react';

export default function GlobalAssistant() {
    const { user, refreshUser } = useAuth();
    const [isOpen, setIsOpen] = useState(false);
    const [conversations, setConversations] = useState([]);
    const [currentConversationId, setCurrentConversationId] = useState(null);
    const [messages, setMessages] = useState([]);
    const [input, setInput] = useState('');
    const [sending, setSending] = useState(false);
    const [loadingConversations, setLoadingConversations] = useState(false);
    const [showHistory, setShowHistory] = useState(false);
    const messagesEndRef = useRef(null);
    
    // Draggable state
    const [position, setPosition] = useState({ x: null, y: null });
    const [isDragging, setIsDragging] = useState(false);
    const dragRef = useRef(null);
    const dragStartPos = useRef({ x: 0, y: 0 });
    const buttonStartPos = useRef({ x: 0, y: 0 });

    // Load conversations when assistant opens
    useEffect(() => {
        if (isOpen && user) {
            loadConversations();
        }
    }, [isOpen, user]);

    // Auto-scroll messages
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages]);

    // Handle drag start
    const handleDragStart = (e) => {
        e.preventDefault();
        setIsDragging(true);
        
        const clientX = e.touches ? e.touches[0].clientX : e.clientX;
        const clientY = e.touches ? e.touches[0].clientY : e.clientY;
        
        dragStartPos.current = { x: clientX, y: clientY };
        
        const rect = dragRef.current?.getBoundingClientRect();
        if (rect) {
            buttonStartPos.current = { 
                x: position.x ?? (window.innerWidth - rect.right + rect.width / 2),
                y: position.y ?? (window.innerHeight - rect.bottom + rect.height / 2)
            };
        }
    };

    // Handle drag move
    useEffect(() => {
        const handleDragMove = (e) => {
            if (!isDragging) return;
            
            const clientX = e.touches ? e.touches[0].clientX : e.clientX;
            const clientY = e.touches ? e.touches[0].clientY : e.clientY;
            
            const deltaX = dragStartPos.current.x - clientX;
            const deltaY = dragStartPos.current.y - clientY;
            
            const newX = Math.max(24, Math.min(window.innerWidth - 80, buttonStartPos.current.x + deltaX));
            const newY = Math.max(24, Math.min(window.innerHeight - 80, buttonStartPos.current.y + deltaY));
            
            setPosition({ x: newX, y: newY });
        };

        const handleDragEnd = () => {
            setIsDragging(false);
        };

        if (isDragging) {
            window.addEventListener('mousemove', handleDragMove);
            window.addEventListener('mouseup', handleDragEnd);
            window.addEventListener('touchmove', handleDragMove);
            window.addEventListener('touchend', handleDragEnd);
        }

        return () => {
            window.removeEventListener('mousemove', handleDragMove);
            window.removeEventListener('mouseup', handleDragEnd);
            window.removeEventListener('touchmove', handleDragMove);
            window.removeEventListener('touchend', handleDragEnd);
        };
    }, [isDragging]);

    const loadConversations = async () => {
        setLoadingConversations(true);
        try {
            const res = await api.get('/conversations');
            setConversations(res.data);
        } catch (error) {
            console.error('Failed to load conversations:', error);
        } finally {
            setLoadingConversations(false);
        }
    };

    const loadConversation = async (conversationId) => {
        try {
            const res = await api.get(`/conversations/${conversationId}/messages`);
            setMessages(res.data);
            setCurrentConversationId(conversationId);
            setShowHistory(false);
        } catch (error) {
            console.error('Failed to load conversation:', error);
        }
    };

    const startNewConversation = () => {
        setCurrentConversationId(null);
        setMessages([]);
        setShowHistory(false);
    };

    const deleteConversation = async (conversationId, e) => {
        e.stopPropagation();
        try {
            await api.delete(`/conversations/${conversationId}`);
            setConversations(prev => prev.filter(c => c.id !== conversationId));
            if (currentConversationId === conversationId) {
                startNewConversation();
            }
        } catch (error) {
            console.error('Failed to delete conversation:', error);
        }
    };

    const sendMessage = async () => {
        if (!input.trim() || sending) return;
        
        const message = input;
        setInput('');
        setSending(true);

        // Add user message immediately
        const userMsg = {
            id: `temp-${Date.now()}`,
            role: 'user',
            content: message,
            timestamp: new Date().toISOString()
        };
        setMessages(prev => [...prev, userMsg]);

        try {
            const res = await api.post('/assistant/chat', {
                message,
                conversation_id: currentConversationId
            });
            
            // Update with actual messages
            setMessages(prev => {
                const filtered = prev.filter(m => m.id !== userMsg.id);
                return [...filtered, res.data.user_message, res.data.ai_message];
            });
            
            // Set conversation ID if new
            if (!currentConversationId) {
                setCurrentConversationId(res.data.conversation_id);
            }
            
            // Refresh user credits
            await refreshUser();
            
            // Refresh conversations list
            loadConversations();
        } catch (error) {
            console.error('Failed to send message:', error);
            
            const errorMsg = error.response?.status === 402 
                ? 'Insufficient credits. Please purchase more credits.'
                : 'Failed to send message. Please try again.';
            
            setMessages(prev => [...prev, {
                id: `error-${Date.now()}`,
                role: 'system',
                content: errorMsg,
                timestamp: new Date().toISOString()
            }]);
        } finally {
            setSending(false);
        }
    };

    const handleKeyPress = (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    };

    const handleButtonClick = (e) => {
        e.preventDefault();
        e.stopPropagation();
        // Simple toggle without drag check for now
        console.log("GlobalAssistant: toggling isOpen from", isOpen, "to", !isOpen);
        setIsOpen(prev => !prev);
    };

    // Reset dragging state on mouse up (as a safety)
    useEffect(() => {
        const handleGlobalMouseUp = () => {
            if (isDragging) {
                setIsDragging(false);
            }
        };
        document.addEventListener('mouseup', handleGlobalMouseUp);
        return () => document.removeEventListener('mouseup', handleGlobalMouseUp);
    }, [isDragging]);

    // Debug: Log user state
    useEffect(() => {
        console.log("GlobalAssistant: user =", user, "isOpen =", isOpen);
    }, [user, isOpen]);

    if (!user) {
        console.log("GlobalAssistant: user is null/undefined, returning null");
        return null;
    }

    // Calculate button position styles
    const buttonStyle = position.x !== null ? {
        right: position.x,
        bottom: position.y
    } : {
        right: 24,
        bottom: 24
    };

    // Calculate panel position based on button - ensure it stays on screen
    const calculatePanelPosition = () => {
        const panelWidth = 384; // w-96 = 24rem = 384px
        const panelHeight = 500;
        const margin = 16;
        
        const btnRight = position.x ?? 24;
        const btnBottom = position.y ?? 24;
        
        // Calculate where panel would be
        let right = btnRight;
        let bottom = btnBottom + 72; // Above the button
        
        // Ensure panel doesn't go off right edge
        if (right < margin) {
            right = margin;
        }
        
        // Ensure panel doesn't go off left edge
        if (typeof window !== 'undefined' && window.innerWidth - right - panelWidth < margin) {
            right = window.innerWidth - panelWidth - margin;
        }
        
        // Ensure panel doesn't go off top
        if (typeof window !== 'undefined' && window.innerHeight - bottom - panelHeight < margin) {
            // Open below button instead
            bottom = Math.max(margin, window.innerHeight - panelHeight - margin);
        }
        
        return { right: Math.max(margin, right), bottom: Math.max(margin, bottom) };
    };

    const panelStyle = calculatePanelPosition();

    return (
        <>
            {/* Floating button - draggable */}
            <div
                ref={dragRef}
                className="fixed z-[9999] select-none"
                style={buttonStyle}
            >
                <div className="relative">
                    {/* Drag handle */}
                    <div
                        className="absolute -top-2 -left-2 w-6 h-6 rounded-full bg-gray-800 border border-white/20 flex items-center justify-center cursor-move opacity-0 hover:opacity-100 transition-opacity"
                        onMouseDown={handleDragStart}
                        onTouchStart={handleDragStart}
                        title="Drag to move"
                    >
                        <GripVertical size={12} className="text-gray-400" />
                    </div>
                    
                    <button
                        type="button"
                        onClick={(e) => {
                            console.log("Button clicked directly!");
                            handleButtonClick(e);
                        }}
                        className={`w-14 h-14 rounded-full bg-gradient-to-r from-fuchsia-500 to-cyan-500 shadow-lg shadow-fuchsia-500/30 flex items-center justify-center hover:scale-110 transition-all duration-200 ${isDragging ? 'cursor-grabbing' : 'cursor-pointer'}`}
                        data-testid="global-assistant-btn"
                    >
                        {isOpen ? (
                            <X className="w-6 h-6 text-white" />
                        ) : (
                            <MessageSquare className="w-6 h-6 text-white" />
                        )}
                    </button>
                </div>
            </div>

            {/* Chat panel */}
            {isOpen && (
                <motion.div
                    initial={{ opacity: 0, y: 20, scale: 0.95 }}
                    animate={{ opacity: 1, y: 0, scale: 1 }}
                    exit={{ opacity: 0, y: 20, scale: 0.95 }}
                    transition={{ duration: 0.2 }}
                    className="fixed w-96 h-[500px] bg-[#0B0F19] border border-white/10 rounded-2xl shadow-2xl shadow-black/50 flex flex-col overflow-hidden z-[9998]"
                    style={panelStyle}
                    data-testid="global-assistant-panel"
                >
                        {/* Header */}
                        <div className="flex items-center justify-between p-4 border-b border-white/10 bg-[#0B0F19]">
                            <div className="flex items-center gap-3">
                                {showHistory && (
                                    <button
                                        onClick={() => setShowHistory(false)}
                                        className="text-gray-400 hover:text-white transition-colors"
                                    >
                                        <ChevronLeft size={20} />
                                    </button>
                                )}
                                <div className="w-8 h-8 rounded-full bg-gradient-to-r from-fuchsia-500 to-cyan-500 flex items-center justify-center">
                                    <Bot size={18} className="text-white" />
                                </div>
                                <div>
                                    <h3 className="font-semibold text-sm">
                                        {showHistory ? 'Conversations' : 'LittleHelper AI'}
                                    </h3>
                                    <div className="flex items-center gap-1 text-xs text-gray-400">
                                        <Zap size={12} className="text-fuchsia-400" />
                                        <span>{formatCredits(user?.credits || 0)} credits</span>
                                    </div>
                                </div>
                            </div>
                            <div className="flex items-center gap-2">
                                {!showHistory && (
                                    <>
                                        <button
                                            onClick={() => setShowHistory(true)}
                                            className="p-2 text-gray-400 hover:text-white hover:bg-white/5 rounded-lg transition-colors"
                                            title="Conversation history"
                                        >
                                            <History size={18} />
                                        </button>
                                        <button
                                            onClick={startNewConversation}
                                            className="p-2 text-gray-400 hover:text-white hover:bg-white/5 rounded-lg transition-colors"
                                            title="New conversation"
                                        >
                                            <Plus size={18} />
                                        </button>
                                    </>
                                )}
                                {/* Explicit close button as safety fallback */}
                                <button
                                    onClick={() => setIsOpen(false)}
                                    className="p-2 text-gray-400 hover:text-red-400 hover:bg-red-500/10 rounded-lg transition-colors"
                                    title="Close assistant"
                                    data-testid="assistant-close-btn"
                                >
                                    <X size={18} />
                                </button>
                            </div>
                        </div>

                        {showHistory ? (
                            // Conversation history view
                            <ScrollArea className="flex-1 p-3">
                                {loadingConversations ? (
                                    <div className="flex items-center justify-center py-8">
                                        <Loader2 className="w-6 h-6 text-fuchsia-400 animate-spin" />
                                    </div>
                                ) : conversations.length === 0 ? (
                                    <div className="text-center py-8 text-gray-500">
                                        <History size={40} className="mx-auto mb-3 opacity-50" />
                                        <p>No conversations yet</p>
                                    </div>
                                ) : (
                                    <div className="space-y-2">
                                        {conversations.map(conv => (
                                            <motion.div
                                                key={conv.id}
                                                initial={{ opacity: 0, x: -10 }}
                                                animate={{ opacity: 1, x: 0 }}
                                                className={`p-3 rounded-lg cursor-pointer group transition-colors ${
                                                    currentConversationId === conv.id
                                                        ? 'bg-fuchsia-500/20 border border-fuchsia-500/30'
                                                        : 'bg-white/5 hover:bg-white/10'
                                                }`}
                                                onClick={() => loadConversation(conv.id)}
                                            >
                                                <div className="flex items-start justify-between gap-2">
                                                    <div className="flex-1 min-w-0">
                                                        <p className="text-sm font-medium truncate">
                                                            {conv.title || 'New Conversation'}
                                                        </p>
                                                        <p className="text-xs text-gray-500 truncate mt-1">
                                                            {conv.last_message}
                                                        </p>
                                                    </div>
                                                    <button
                                                        onClick={(e) => deleteConversation(conv.id, e)}
                                                        className="p-1 text-gray-500 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-all"
                                                    >
                                                        <Trash2 size={14} />
                                                    </button>
                                                </div>
                                                <p className="text-xs text-gray-600 mt-2">
                                                    {conv.message_count} messages
                                                </p>
                                            </motion.div>
                                        ))}
                                    </div>
                                )}
                            </ScrollArea>
                        ) : (
                            // Chat view
                            <>
                                <ScrollArea className="flex-1 p-4">
                                    {messages.length === 0 ? (
                                        <div className="flex flex-col items-center justify-center h-full text-center py-8">
                                            <div className="w-16 h-16 rounded-full bg-gradient-to-r from-fuchsia-500/20 to-cyan-500/20 flex items-center justify-center mb-4">
                                                <Bot size={32} className="text-fuchsia-400" />
                                            </div>
                                            <h4 className="font-semibold mb-2">Hi! I&apos;m LittleHelper</h4>
                                            <p className="text-sm text-gray-400 max-w-[250px]">
                                                Ask me anything about coding, projects, or get help with your development tasks.
                                            </p>
                                            <p className="text-xs text-fuchsia-400 mt-3">
                                                Chat uses credits ({user?.credits_enabled !== false ? 'enabled' : 'disabled'})
                                            </p>
                                        </div>
                                    ) : (
                                        <div className="space-y-4">
                                            {messages.map((msg, idx) => (
                                                <motion.div
                                                    key={msg.id || idx}
                                                    initial={{ opacity: 0, y: 10 }}
                                                    animate={{ opacity: 1, y: 0 }}
                                                    className={`flex gap-3 ${
                                                        msg.role === 'user' ? 'flex-row-reverse' : ''
                                                    }`}
                                                >
                                                    <div className={`w-7 h-7 rounded-full flex-shrink-0 flex items-center justify-center ${
                                                        msg.role === 'user' 
                                                            ? 'bg-fuchsia-500/20' 
                                                            : msg.role === 'system'
                                                                ? 'bg-red-500/20'
                                                                : 'bg-cyan-500/20'
                                                    }`}>
                                                        {msg.role === 'user' ? (
                                                            <User size={14} className="text-fuchsia-400" />
                                                        ) : msg.role === 'system' ? (
                                                            <X size={14} className="text-red-400" />
                                                        ) : (
                                                            <Bot size={14} className="text-cyan-400" />
                                                        )}
                                                    </div>
                                                    <div className={`flex-1 rounded-xl p-3 text-sm ${
                                                        msg.role === 'user'
                                                            ? 'bg-fuchsia-500/20 text-white'
                                                            : msg.role === 'system'
                                                                ? 'bg-red-500/20 text-red-300'
                                                                : 'bg-white/5 text-gray-200'
                                                    }`}>
                                                        <p className="whitespace-pre-wrap break-words">{msg.content}</p>
                                                        {msg.credits_deducted > 0 && (
                                                            <p className="text-xs text-gray-500 mt-2 flex items-center gap-1">
                                                                <Zap size={10} />
                                                                {msg.credits_deducted.toFixed(2)} credits used
                                                            </p>
                                                        )}
                                                    </div>
                                                </motion.div>
                                            ))}
                                            <div ref={messagesEndRef} />
                                        </div>
                                    )}
                                </ScrollArea>

                                {/* Input */}
                                <div className="p-4 border-t border-white/10">
                                    <div className="flex gap-2">
                                        <Input
                                            value={input}
                                            onChange={(e) => setInput(e.target.value)}
                                            onKeyPress={handleKeyPress}
                                            placeholder="Ask me anything..."
                                            disabled={sending}
                                            className="flex-1 bg-white/5 border-white/10 text-sm"
                                            data-testid="assistant-input"
                                        />
                                        <Button
                                            onClick={sendMessage}
                                            disabled={!input.trim() || sending}
                                            className="bg-fuchsia-500 hover:bg-fuchsia-600"
                                            data-testid="assistant-send"
                                        >
                                            {sending ? (
                                                <Loader2 size={18} className="animate-spin" />
                                            ) : (
                                                <Send size={18} />
                                            )}
                                        </Button>
                                    </div>
                                </div>
                            </>
                        )}
                    </motion.div>
                )}
        </>
    );
}
