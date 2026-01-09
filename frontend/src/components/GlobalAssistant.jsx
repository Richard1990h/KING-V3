import { useState, useEffect, useRef, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAuth } from '../lib/auth';
import api, { friendsAPI } from '../lib/api';
import { formatCredits } from '../lib/utils';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { ScrollArea } from './ui/scroll-area';
import { 
    MessageSquare, X, Send, Plus, Trash2, ChevronLeft, 
    Zap, Bot, User, History, Loader2, GripVertical,
    Users, UserPlus, Check, XCircle, Bell, Circle
} from 'lucide-react';

// Tab types
const TABS = {
    AI: 'ai',
    FRIENDS: 'friends',
    DM: 'dm'
};

export default function GlobalAssistant() {
    const { user, refreshUser } = useAuth();
    const [isOpen, setIsOpen] = useState(false);
    
    // Active tab and DM state
    const [activeTab, setActiveTab] = useState(TABS.AI);
    const [activeDmFriend, setActiveDmFriend] = useState(null); // Friend we're chatting with
    const [dmTabs, setDmTabs] = useState([]); // Open DM conversations
    
    // AI Chat state
    const [conversations, setConversations] = useState([]);
    const [currentConversationId, setCurrentConversationId] = useState(null);
    const [messages, setMessages] = useState([]);
    const [input, setInput] = useState('');
    const [sending, setSending] = useState(false);
    const [loadingConversations, setLoadingConversations] = useState(false);
    const [showHistory, setShowHistory] = useState(false);
    
    // Friends state
    const [friends, setFriends] = useState([]);
    const [friendRequests, setFriendRequests] = useState({ incoming: [], outgoing: [] });
    const [loadingFriends, setLoadingFriends] = useState(false);
    const [addFriendEmail, setAddFriendEmail] = useState('');
    const [addingFriend, setAddingFriend] = useState(false);
    const [friendError, setFriendError] = useState('');
    const [friendSuccess, setFriendSuccess] = useState('');
    const [unreadCounts, setUnreadCounts] = useState({});
    
    // DM state
    const [dmMessages, setDmMessages] = useState([]);
    const [dmInput, setDmInput] = useState('');
    const [sendingDm, setSendingDm] = useState(false);
    const [loadingDm, setLoadingDm] = useState(false);
    
    const messagesEndRef = useRef(null);
    const dmMessagesEndRef = useRef(null);
    
    // Draggable state
    const [position, setPosition] = useState({ x: null, y: null });
    const [isDragging, setIsDragging] = useState(false);
    const dragRef = useRef(null);
    const dragStartPos = useRef({ x: 0, y: 0 });
    const buttonStartPos = useRef({ x: 0, y: 0 });

    // Load data when assistant opens
    useEffect(() => {
        if (isOpen && user) {
            if (activeTab === TABS.AI) {
                loadConversations();
            } else if (activeTab === TABS.FRIENDS) {
                loadFriends();
                loadFriendRequests();
            }
            loadUnreadCounts();
        }
    }, [isOpen, user, activeTab]);

    // Auto-scroll messages
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages]);
    
    useEffect(() => {
        dmMessagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [dmMessages]);

    // Load unread message counts
    const loadUnreadCounts = async () => {
        try {
            const res = await friendsAPI.getUnreadCount();
            const counts = {};
            (res.data?.by_user || []).forEach(u => {
                counts[u.sender_id] = u.count;
            });
            setUnreadCounts(counts);
        } catch (error) {
            console.error('Failed to load unread counts:', error);
        }
    };

    // Load friends list
    const loadFriends = async () => {
        setLoadingFriends(true);
        try {
            const res = await friendsAPI.getFriends();
            setFriends(res.data?.friends || []);
        } catch (error) {
            console.error('Failed to load friends:', error);
        } finally {
            setLoadingFriends(false);
        }
    };
    
    // Load friend requests
    const loadFriendRequests = async () => {
        try {
            const res = await friendsAPI.getRequests();
            setFriendRequests({
                incoming: res.data?.incoming || [],
                outgoing: res.data?.outgoing || []
            });
        } catch (error) {
            console.error('Failed to load friend requests:', error);
        }
    };
    
    // Send friend request
    const sendFriendRequest = async () => {
        if (!addFriendEmail.trim() || addingFriend) return;
        setAddingFriend(true);
        setFriendError('');
        setFriendSuccess('');
        
        try {
            await friendsAPI.sendRequest(addFriendEmail.trim());
            setFriendSuccess('Friend request sent!');
            setAddFriendEmail('');
            loadFriendRequests();
        } catch (error) {
            const msg = error.response?.data?.detail || 'Failed to send request';
            if (error.code === 'ERR_NETWORK') {
                setFriendError('Server unavailable. Please try again later.');
            } else {
                setFriendError(msg);
            }
        } finally {
            setAddingFriend(false);
        }
    };
    
    // Accept/Deny friend request
    const respondToRequest = async (requestId, action) => {
        try {
            await friendsAPI.respondToRequest(requestId, action);
            loadFriendRequests();
            if (action === 'accept') {
                loadFriends();
            }
        } catch (error) {
            console.error('Failed to respond to request:', error);
        }
    };
    
    // Open DM with a friend
    const openDmWith = (friend) => {
        // Add to DM tabs if not already there
        if (!dmTabs.find(t => t.id === friend.friend_user_id)) {
            setDmTabs(prev => [...prev, {
                id: friend.friend_user_id,
                name: friend.display_name || friend.email,
                email: friend.email
            }]);
        }
        setActiveDmFriend({
            id: friend.friend_user_id,
            name: friend.display_name || friend.email,
            email: friend.email
        });
        setActiveTab(TABS.DM);
        loadDmMessages(friend.friend_user_id);
        
        // Clear unread count for this friend
        setUnreadCounts(prev => ({ ...prev, [friend.friend_user_id]: 0 }));
    };
    
    // Close a DM tab
    const closeDmTab = (friendId, e) => {
        e?.stopPropagation();
        setDmTabs(prev => prev.filter(t => t.id !== friendId));
        if (activeDmFriend?.id === friendId) {
            const remaining = dmTabs.filter(t => t.id !== friendId);
            if (remaining.length > 0) {
                setActiveDmFriend(remaining[0]);
                loadDmMessages(remaining[0].id);
            } else {
                setActiveDmFriend(null);
                setActiveTab(TABS.FRIENDS);
            }
        }
    };
    
    // Load DM messages
    const loadDmMessages = async (friendId) => {
        setLoadingDm(true);
        try {
            const res = await friendsAPI.getMessages(friendId);
            setDmMessages(res.data || []);
        } catch (error) {
            console.error('Failed to load DM messages:', error);
            setDmMessages([]);
        } finally {
            setLoadingDm(false);
        }
    };
    
    // Send DM
    const sendDm = async () => {
        if (!dmInput.trim() || sendingDm || !activeDmFriend) return;
        
        const message = dmInput;
        setDmInput('');
        setSendingDm(true);
        
        // Optimistic update
        const tempMsg = {
            id: `temp-${Date.now()}`,
            sender_id: user?.id,
            message: message,
            created_at: new Date().toISOString()
        };
        setDmMessages(prev => [...prev, tempMsg]);
        
        try {
            const res = await friendsAPI.sendMessage(activeDmFriend.id, message);
            // Replace temp message with real one
            setDmMessages(prev => {
                const filtered = prev.filter(m => m.id !== tempMsg.id);
                return [...filtered, { 
                    ...tempMsg, 
                    id: res.data.id,
                    created_at: res.data.sent_at 
                }];
            });
        } catch (error) {
            console.error('Failed to send DM:', error);
            // Remove temp message on error
            setDmMessages(prev => prev.filter(m => m.id !== tempMsg.id));
        } finally {
            setSendingDm(false);
        }
    };

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
            if (activeTab === TABS.DM) {
                sendDm();
            } else {
                sendMessage();
            }
        }
    };

    const handleButtonClick = (e) => {
        e.preventDefault();
        e.stopPropagation();
        setIsOpen(prev => !prev);
    };

    // Reset dragging state on mouse up
    useEffect(() => {
        const handleGlobalMouseUp = () => {
            if (isDragging) {
                setIsDragging(false);
            }
        };
        document.addEventListener('mouseup', handleGlobalMouseUp);
        return () => document.removeEventListener('mouseup', handleGlobalMouseUp);
    }, [isDragging]);

    if (!user) return null;

    // Calculate total unread
    const totalUnread = Object.values(unreadCounts).reduce((a, b) => a + b, 0);
    const totalPendingRequests = friendRequests.incoming.length;

    // Calculate button position styles
    const buttonStyle = position.x !== null ? {
        right: position.x,
        bottom: position.y
    } : {
        right: 24,
        bottom: 24
    };

    // Calculate panel position based on button
    const calculatePanelPosition = () => {
        const panelWidth = 420;
        const panelHeight = 550;
        const margin = 16;
        
        const btnRight = position.x ?? 24;
        const btnBottom = position.y ?? 24;
        
        let right = btnRight;
        let bottom = btnBottom + 72;
        
        if (right < margin) right = margin;
        if (typeof window !== 'undefined' && window.innerWidth - right - panelWidth < margin) {
            right = window.innerWidth - panelWidth - margin;
        }
        if (typeof window !== 'undefined' && window.innerHeight - bottom - panelHeight < margin) {
            bottom = Math.max(margin, window.innerHeight - panelHeight - margin);
        }
        
        return { right: Math.max(margin, right), bottom: Math.max(margin, bottom) };
    };

    const panelStyle = calculatePanelPosition();

    // Render AI Chat content
    const renderAIChat = () => (
        <>
            {showHistory ? (
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
                            </div>
                        ) : (
                            <div className="space-y-4">
                                {messages.map((msg, idx) => (
                                    <motion.div
                                        key={msg.id || idx}
                                        initial={{ opacity: 0, y: 10 }}
                                        animate={{ opacity: 1, y: 0 }}
                                        className={`flex gap-3 ${msg.role === 'user' ? 'flex-row-reverse' : ''}`}
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
                                                <XCircle size={14} className="text-red-400" />
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
                                {sending ? <Loader2 size={18} className="animate-spin" /> : <Send size={18} />}
                            </Button>
                        </div>
                    </div>
                </>
            )}
        </>
    );

    // Render Friends content
    const renderFriends = () => (
        <ScrollArea className="flex-1 p-3">
            {/* Add Friend */}
            <div className="mb-4 p-3 rounded-lg bg-white/5">
                <h4 className="text-sm font-medium mb-2 flex items-center gap-2">
                    <UserPlus size={14} className="text-fuchsia-400" />
                    Add Friend
                </h4>
                <div className="flex gap-2">
                    <Input
                        value={addFriendEmail}
                        onChange={(e) => setAddFriendEmail(e.target.value)}
                        placeholder="Enter email address"
                        className="flex-1 bg-white/5 border-white/10 text-sm h-8"
                        onKeyPress={(e) => e.key === 'Enter' && sendFriendRequest()}
                    />
                    <Button
                        onClick={sendFriendRequest}
                        disabled={!addFriendEmail.trim() || addingFriend}
                        size="sm"
                        className="bg-fuchsia-500 hover:bg-fuchsia-600 h-8"
                    >
                        {addingFriend ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />}
                    </Button>
                </div>
                {friendError && <p className="text-xs text-red-400 mt-2">{friendError}</p>}
                {friendSuccess && <p className="text-xs text-green-400 mt-2">{friendSuccess}</p>}
            </div>
            
            {/* Incoming Requests */}
            {friendRequests.incoming.length > 0 && (
                <div className="mb-4">
                    <h4 className="text-sm font-medium mb-2 flex items-center gap-2">
                        <Bell size={14} className="text-yellow-400" />
                        Friend Requests ({friendRequests.incoming.length})
                    </h4>
                    <div className="space-y-2">
                        {friendRequests.incoming.map(req => (
                            <div key={req.id} className="flex items-center gap-3 p-2 rounded-lg bg-yellow-500/10">
                                <div className="w-8 h-8 rounded-full bg-gradient-to-r from-yellow-500 to-orange-500 flex items-center justify-center text-sm font-medium">
                                    {req.sender_name?.[0]?.toUpperCase() || '?'}
                                </div>
                                <div className="flex-1 min-w-0">
                                    <p className="text-sm font-medium truncate">{req.sender_name}</p>
                                    <p className="text-xs text-gray-400 truncate">{req.sender_email}</p>
                                </div>
                                <div className="flex gap-1">
                                    <button
                                        onClick={() => respondToRequest(req.id, 'accept')}
                                        className="p-1.5 rounded bg-green-500/20 text-green-400 hover:bg-green-500/30"
                                    >
                                        <Check size={14} />
                                    </button>
                                    <button
                                        onClick={() => respondToRequest(req.id, 'deny')}
                                        className="p-1.5 rounded bg-red-500/20 text-red-400 hover:bg-red-500/30"
                                    >
                                        <XCircle size={14} />
                                    </button>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}
            
            {/* Friends List */}
            <div>
                <h4 className="text-sm font-medium mb-2 flex items-center gap-2">
                    <Users size={14} className="text-cyan-400" />
                    Friends ({friends.length})
                </h4>
                {loadingFriends ? (
                    <div className="flex items-center justify-center py-6">
                        <Loader2 className="w-5 h-5 text-fuchsia-400 animate-spin" />
                    </div>
                ) : friends.length === 0 ? (
                    <div className="text-center py-6 text-gray-500">
                        <Users size={32} className="mx-auto mb-2 opacity-50" />
                        <p className="text-sm">No friends yet</p>
                        <p className="text-xs mt-1">Add friends by email above</p>
                    </div>
                ) : (
                    <div className="space-y-2">
                        {friends.map(friend => (
                            <div 
                                key={friend.id} 
                                className="flex items-center gap-3 p-2 rounded-lg bg-white/5 hover:bg-white/10 cursor-pointer transition-colors"
                                onClick={() => openDmWith(friend)}
                            >
                                <div className="relative">
                                    <div className="w-8 h-8 rounded-full bg-gradient-to-r from-fuchsia-500 to-cyan-500 flex items-center justify-center text-sm font-medium">
                                        {friend.display_name?.[0]?.toUpperCase() || '?'}
                                    </div>
                                    {unreadCounts[friend.friend_user_id] > 0 && (
                                        <div className="absolute -top-1 -right-1 w-4 h-4 rounded-full bg-red-500 text-white text-xs flex items-center justify-center">
                                            {unreadCounts[friend.friend_user_id]}
                                        </div>
                                    )}
                                </div>
                                <div className="flex-1 min-w-0">
                                    <p className="text-sm font-medium truncate">{friend.display_name}</p>
                                    <p className="text-xs text-gray-400 truncate">{friend.email}</p>
                                </div>
                                <MessageSquare size={16} className="text-gray-400" />
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </ScrollArea>
    );

    // Render DM Chat content
    const renderDM = () => (
        <>
            {/* DM Tabs */}
            {dmTabs.length > 0 && (
                <div className="flex gap-1 p-2 border-b border-white/10 overflow-x-auto">
                    {dmTabs.map(tab => (
                        <div
                            key={tab.id}
                            className={`flex items-center gap-1 px-2 py-1 rounded text-xs cursor-pointer transition-colors ${
                                activeDmFriend?.id === tab.id
                                    ? 'bg-fuchsia-500/20 text-fuchsia-400'
                                    : 'bg-white/5 text-gray-400 hover:bg-white/10'
                            }`}
                            onClick={() => {
                                setActiveDmFriend(tab);
                                loadDmMessages(tab.id);
                            }}
                        >
                            <span className="truncate max-w-[80px]">{tab.name}</span>
                            <button
                                onClick={(e) => closeDmTab(tab.id, e)}
                                className="ml-1 hover:text-red-400"
                            >
                                <X size={12} />
                            </button>
                        </div>
                    ))}
                </div>
            )}
            
            {activeDmFriend ? (
                <>
                    <ScrollArea className="flex-1 p-4">
                        {loadingDm ? (
                            <div className="flex items-center justify-center py-8">
                                <Loader2 className="w-5 h-5 text-fuchsia-400 animate-spin" />
                            </div>
                        ) : dmMessages.length === 0 ? (
                            <div className="flex flex-col items-center justify-center h-full text-center py-8">
                                <User size={32} className="text-gray-500 mb-2" />
                                <p className="text-sm text-gray-400">No messages yet</p>
                                <p className="text-xs text-gray-500">Start the conversation!</p>
                            </div>
                        ) : (
                            <div className="space-y-3">
                                {dmMessages.map((msg, idx) => {
                                    const isMe = msg.sender_id === user?.id;
                                    return (
                                        <motion.div
                                            key={msg.id || idx}
                                            initial={{ opacity: 0, y: 10 }}
                                            animate={{ opacity: 1, y: 0 }}
                                            className={`flex gap-2 ${isMe ? 'flex-row-reverse' : ''}`}
                                        >
                                            <div className={`w-6 h-6 rounded-full flex-shrink-0 flex items-center justify-center ${
                                                isMe ? 'bg-fuchsia-500/20' : 'bg-cyan-500/20'
                                            }`}>
                                                <User size={12} className={isMe ? 'text-fuchsia-400' : 'text-cyan-400'} />
                                            </div>
                                            <div className={`max-w-[75%] rounded-xl p-2 text-sm ${
                                                isMe ? 'bg-fuchsia-500/20' : 'bg-white/10'
                                            }`}>
                                                <p className="whitespace-pre-wrap break-words">{msg.message}</p>
                                                <p className="text-xs text-gray-500 mt-1">
                                                    {new Date(msg.created_at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                                                </p>
                                            </div>
                                        </motion.div>
                                    );
                                })}
                                <div ref={dmMessagesEndRef} />
                            </div>
                        )}
                    </ScrollArea>
                    
                    <div className="p-3 border-t border-white/10">
                        <div className="flex gap-2">
                            <Input
                                value={dmInput}
                                onChange={(e) => setDmInput(e.target.value)}
                                onKeyPress={handleKeyPress}
                                placeholder={`Message ${activeDmFriend.name}...`}
                                disabled={sendingDm}
                                className="flex-1 bg-white/5 border-white/10 text-sm"
                            />
                            <Button
                                onClick={sendDm}
                                disabled={!dmInput.trim() || sendingDm}
                                className="bg-fuchsia-500 hover:bg-fuchsia-600"
                            >
                                {sendingDm ? <Loader2 size={18} className="animate-spin" /> : <Send size={18} />}
                            </Button>
                        </div>
                    </div>
                </>
            ) : (
                <div className="flex-1 flex flex-col items-center justify-center text-gray-500">
                    <MessageSquare size={40} className="mb-3 opacity-50" />
                    <p>Select a friend to start chatting</p>
                    <Button
                        onClick={() => setActiveTab(TABS.FRIENDS)}
                        variant="link"
                        className="text-fuchsia-400 mt-2"
                    >
                        Go to Friends
                    </Button>
                </div>
            )}
        </>
    );

    return (
        <>
            {/* Floating button */}
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
                    
                    {/* Notification badge */}
                    {(totalUnread > 0 || totalPendingRequests > 0) && (
                        <div className="absolute -top-1 -right-1 w-5 h-5 rounded-full bg-red-500 text-white text-xs flex items-center justify-center font-medium">
                            {totalUnread + totalPendingRequests}
                        </div>
                    )}
                    
                    <button
                        type="button"
                        onClick={handleButtonClick}
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
            <AnimatePresence>
                {isOpen && (
                    <motion.div
                        initial={{ opacity: 0, y: 20, scale: 0.95 }}
                        animate={{ opacity: 1, y: 0, scale: 1 }}
                        exit={{ opacity: 0, y: 20, scale: 0.95 }}
                        transition={{ duration: 0.2 }}
                        className="fixed w-[420px] h-[550px] bg-[#0B0F19] border border-white/10 rounded-2xl shadow-2xl shadow-black/50 flex flex-col overflow-hidden z-[9998]"
                        style={panelStyle}
                        data-testid="global-assistant-panel"
                    >
                        {/* Header with tabs */}
                        <div className="border-b border-white/10 bg-[#0B0F19]">
                            <div className="flex items-center justify-between p-3">
                                <div className="flex items-center gap-2">
                                    {showHistory && activeTab === TABS.AI && (
                                        <button
                                            onClick={() => setShowHistory(false)}
                                            className="text-gray-400 hover:text-white transition-colors"
                                        >
                                            <ChevronLeft size={18} />
                                        </button>
                                    )}
                                    <div className="w-8 h-8 rounded-full bg-gradient-to-r from-fuchsia-500 to-cyan-500 flex items-center justify-center">
                                        {activeTab === TABS.AI ? (
                                            <Bot size={16} className="text-white" />
                                        ) : activeTab === TABS.FRIENDS ? (
                                            <Users size={16} className="text-white" />
                                        ) : (
                                            <MessageSquare size={16} className="text-white" />
                                        )}
                                    </div>
                                    <div>
                                        <h3 className="font-semibold text-sm">
                                            {activeTab === TABS.AI ? (showHistory ? 'Conversations' : 'LittleHelper AI') 
                                                : activeTab === TABS.FRIENDS ? 'Friends' 
                                                : activeDmFriend?.name || 'Messages'}
                                        </h3>
                                        {activeTab === TABS.AI && (
                                            <div className="flex items-center gap-1 text-xs text-gray-400">
                                                <Zap size={10} className="text-fuchsia-400" />
                                                <span>{formatCredits(user?.credits || 0)} credits</span>
                                            </div>
                                        )}
                                    </div>
                                </div>
                                <div className="flex items-center gap-1">
                                    {activeTab === TABS.AI && !showHistory && (
                                        <>
                                            <button
                                                onClick={() => setShowHistory(true)}
                                                className="p-1.5 text-gray-400 hover:text-white hover:bg-white/5 rounded-lg transition-colors"
                                                title="History"
                                            >
                                                <History size={16} />
                                            </button>
                                            <button
                                                onClick={startNewConversation}
                                                className="p-1.5 text-gray-400 hover:text-white hover:bg-white/5 rounded-lg transition-colors"
                                                title="New"
                                            >
                                                <Plus size={16} />
                                            </button>
                                        </>
                                    )}
                                    <button
                                        onClick={() => setIsOpen(false)}
                                        className="p-1.5 text-gray-400 hover:text-red-400 hover:bg-red-500/10 rounded-lg transition-colors"
                                        title="Close"
                                    >
                                        <X size={16} />
                                    </button>
                                </div>
                            </div>
                            
                            {/* Tab bar */}
                            <div className="flex px-3 pb-2 gap-1">
                                <button
                                    onClick={() => { setActiveTab(TABS.AI); setShowHistory(false); }}
                                    className={`flex-1 py-1.5 px-3 rounded-lg text-xs font-medium transition-colors flex items-center justify-center gap-1 ${
                                        activeTab === TABS.AI
                                            ? 'bg-fuchsia-500/20 text-fuchsia-400'
                                            : 'bg-white/5 text-gray-400 hover:bg-white/10'
                                    }`}
                                >
                                    <Bot size={14} />
                                    AI
                                </button>
                                <button
                                    onClick={() => { setActiveTab(TABS.FRIENDS); loadFriends(); loadFriendRequests(); }}
                                    className={`flex-1 py-1.5 px-3 rounded-lg text-xs font-medium transition-colors flex items-center justify-center gap-1 relative ${
                                        activeTab === TABS.FRIENDS
                                            ? 'bg-cyan-500/20 text-cyan-400'
                                            : 'bg-white/5 text-gray-400 hover:bg-white/10'
                                    }`}
                                >
                                    <Users size={14} />
                                    Friends
                                    {totalPendingRequests > 0 && (
                                        <span className="absolute -top-1 -right-1 w-4 h-4 rounded-full bg-yellow-500 text-white text-xs flex items-center justify-center">
                                            {totalPendingRequests}
                                        </span>
                                    )}
                                </button>
                                <button
                                    onClick={() => setActiveTab(TABS.DM)}
                                    className={`flex-1 py-1.5 px-3 rounded-lg text-xs font-medium transition-colors flex items-center justify-center gap-1 relative ${
                                        activeTab === TABS.DM
                                            ? 'bg-green-500/20 text-green-400'
                                            : 'bg-white/5 text-gray-400 hover:bg-white/10'
                                    }`}
                                >
                                    <MessageSquare size={14} />
                                    DMs
                                    {totalUnread > 0 && (
                                        <span className="absolute -top-1 -right-1 w-4 h-4 rounded-full bg-red-500 text-white text-xs flex items-center justify-center">
                                            {totalUnread}
                                        </span>
                                    )}
                                </button>
                            </div>
                        </div>

                        {/* Content */}
                        {activeTab === TABS.AI && renderAIChat()}
                        {activeTab === TABS.FRIENDS && renderFriends()}
                        {activeTab === TABS.DM && renderDM()}
                    </motion.div>
                )}
            </AnimatePresence>
        </>
    );
}
