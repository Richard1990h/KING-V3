import { useState, useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { ScrollArea } from './ui/scroll-area';
import { 
    Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger 
} from './ui/dialog';
import { friendsAPI } from '../lib/api';
import { useAuth } from '../lib/auth';
import {
    Users, UserPlus, MessageSquare, Send, Check, X, 
    Loader2, Bell, ChevronRight, User, Mail
} from 'lucide-react';

export function FriendsSidebar({ isOpen, onClose }) {
    const { user } = useAuth();
    const [activeTab, setActiveTab] = useState('friends'); // friends, requests, add
    const [friends, setFriends] = useState([]);
    const [requests, setRequests] = useState({ incoming: [], outgoing: [] });
    const [unreadCounts, setUnreadCounts] = useState({});
    const [loading, setLoading] = useState(false);
    const [selectedFriend, setSelectedFriend] = useState(null);
    const [addEmail, setAddEmail] = useState('');
    const [addError, setAddError] = useState('');
    const [addSuccess, setAddSuccess] = useState('');

    useEffect(() => {
        if (isOpen) {
            loadFriends();
            loadRequests();
            loadUnreadCounts();
        }
    }, [isOpen]);

    const loadFriends = async () => {
        try {
            const res = await friendsAPI.getFriends();
            // Defensive: ensure friends is always an array
            setFriends(Array.isArray(res.data) ? res.data : []);
        } catch (error) {
            console.error('Failed to load friends:', error);
            setFriends([]);
        }
    };

    const loadRequests = async () => {
        try {
            const res = await friendsAPI.getRequests();
            // Defensive: ensure requests structure has arrays
            setRequests({
                incoming: Array.isArray(res.data?.incoming) ? res.data.incoming : [],
                outgoing: Array.isArray(res.data?.outgoing) ? res.data.outgoing : []
            });
        } catch (error) {
            console.error('Failed to load requests:', error);
            setRequests({ incoming: [], outgoing: [] });
        }
    };

    const loadUnreadCounts = async () => {
        try {
            const res = await friendsAPI.getUnreadCount();
            const byUser = {};
            // Defensive: ensure by_user is an array before iterating
            const byUserData = Array.isArray(res.data?.by_user) ? res.data.by_user : [];
            byUserData.forEach(u => {
                if (u && u.sender_id !== undefined) {
                    byUser[u.sender_id] = u.count || 0;
                }
            });
            setUnreadCounts(byUser);
        } catch (error) {
            console.error('Failed to load unread counts:', error);
            setUnreadCounts({});
        }
    };

    const handleSendRequest = async () => {
        if (!addEmail.trim()) return;
        setAddError('');
        setAddSuccess('');
        setLoading(true);
        try {
            await friendsAPI.sendRequest(addEmail.trim());
            setAddSuccess('Friend request sent!');
            setAddEmail('');
            loadRequests();
        } catch (error) {
            const errorMessage = error.response?.data?.detail || error.message || 'Failed to send request';
            // Check if backend is unavailable
            if (error.code === 'ERR_NETWORK' || error.message?.includes('Network')) {
                setAddError('Server is currently unavailable. Please try again later.');
            } else if (error.response?.status === 404) {
                setAddError('User not found. Please check the email address.');
            } else if (error.response?.status === 409) {
                setAddError('Friend request already exists or you are already friends.');
            } else {
                setAddError(errorMessage);
            }
        } finally {
            setLoading(false);
        }
    };

    const handleRespondToRequest = async (requestId, action) => {
        try {
            await friendsAPI.respondToRequest(requestId, action);
            loadRequests();
            if (action === 'accept') {
                loadFriends();
            }
        } catch (error) {
            console.error('Failed to respond to request:', error);
        }
    };

    const handleRemoveFriend = async (friendId) => {
        if (!window.confirm('Are you sure you want to remove this friend?')) return;
        try {
            await friendsAPI.removeFriend(friendId);
            loadFriends();
            if (selectedFriend?.friend_user_id === friendId) {
                setSelectedFriend(null);
            }
        } catch (error) {
            console.error('Failed to remove friend:', error);
        }
    };

    const totalPendingRequests = requests.incoming?.length || 0;
    const totalUnread = Object.values(unreadCounts).reduce((a, b) => a + b, 0);

    if (!isOpen) return null;

    return (
        <motion.div
            initial={{ x: -300, opacity: 0 }}
            animate={{ x: 0, opacity: 1 }}
            exit={{ x: -300, opacity: 0 }}
            className="fixed left-0 top-0 h-full w-80 bg-[#0B0F19] border-r border-white/10 z-[100] flex flex-col"
        >
            {/* Header */}
            <div className="p-4 border-b border-white/10 flex items-center justify-between">
                <div className="flex items-center gap-2">
                    <Users className="text-fuchsia-400" size={20} />
                    <h2 className="font-semibold">Friends</h2>
                    {(totalPendingRequests > 0 || totalUnread > 0) && (
                        <span className="px-2 py-0.5 bg-fuchsia-500 text-white text-xs rounded-full">
                            {totalPendingRequests + totalUnread}
                        </span>
                    )}
                </div>
                <Button variant="ghost" size="icon" onClick={onClose}>
                    <X size={18} />
                </Button>
            </div>

            {/* Tabs */}
            <div className="flex border-b border-white/10">
                <button
                    onClick={() => setActiveTab('friends')}
                    className={`flex-1 py-2 text-sm font-medium transition-colors ${
                        activeTab === 'friends' 
                            ? 'text-fuchsia-400 border-b-2 border-fuchsia-400' 
                            : 'text-gray-400 hover:text-white'
                    }`}
                >
                    Friends ({friends.length})
                </button>
                <button
                    onClick={() => setActiveTab('requests')}
                    className={`flex-1 py-2 text-sm font-medium transition-colors relative ${
                        activeTab === 'requests' 
                            ? 'text-fuchsia-400 border-b-2 border-fuchsia-400' 
                            : 'text-gray-400 hover:text-white'
                    }`}
                >
                    Requests
                    {totalPendingRequests > 0 && (
                        <span className="absolute top-1 right-4 w-2 h-2 bg-red-500 rounded-full" />
                    )}
                </button>
                <button
                    onClick={() => setActiveTab('add')}
                    className={`flex-1 py-2 text-sm font-medium transition-colors ${
                        activeTab === 'add' 
                            ? 'text-fuchsia-400 border-b-2 border-fuchsia-400' 
                            : 'text-gray-400 hover:text-white'
                    }`}
                >
                    <UserPlus size={16} className="inline mr-1" />
                    Add
                </button>
            </div>

            {/* Content */}
            <ScrollArea className="flex-1">
                <AnimatePresence mode="wait">
                    {activeTab === 'friends' && (
                        <motion.div
                            key="friends"
                            initial={{ opacity: 0 }}
                            animate={{ opacity: 1 }}
                            exit={{ opacity: 0 }}
                            className="p-2"
                        >
                            {friends.length === 0 ? (
                                <div className="text-center py-8 text-gray-500">
                                    <Users className="w-12 h-12 mx-auto mb-2 opacity-50" />
                                    <p>No friends yet</p>
                                    <p className="text-xs mt-1">Add friends to collaborate!</p>
                                </div>
                            ) : (
                                <div className="space-y-1">
                                    {friends.map((friend) => (
                                        <button
                                            key={friend.friend_user_id}
                                            onClick={() => setSelectedFriend(friend)}
                                            className="w-full flex items-center gap-3 p-2 rounded-lg hover:bg-white/5 transition-colors group"
                                        >
                                            <div className="w-10 h-10 rounded-full bg-gradient-to-br from-fuchsia-500 to-cyan-500 flex items-center justify-center text-white font-medium">
                                                {friend.display_name?.charAt(0)?.toUpperCase() || friend.email?.charAt(0)?.toUpperCase() || '?'}
                                            </div>
                                            <div className="flex-1 text-left">
                                                <p className="font-medium text-sm">
                                                    {friend.display_name || friend.email?.split('@')[0]}
                                                </p>
                                                <p className="text-xs text-gray-500">{friend.email}</p>
                                            </div>
                                            {unreadCounts[friend.friend_user_id] > 0 && (
                                                <span className="px-2 py-0.5 bg-fuchsia-500 text-white text-xs rounded-full">
                                                    {unreadCounts[friend.friend_user_id]}
                                                </span>
                                            )}
                                            <ChevronRight size={16} className="text-gray-500 opacity-0 group-hover:opacity-100" />
                                        </button>
                                    ))}
                                </div>
                            )}
                        </motion.div>
                    )}

                    {activeTab === 'requests' && (
                        <motion.div
                            key="requests"
                            initial={{ opacity: 0 }}
                            animate={{ opacity: 1 }}
                            exit={{ opacity: 0 }}
                            className="p-2"
                        >
                            {/* Incoming */}
                            <div className="mb-4">
                                <h3 className="text-xs font-medium text-gray-500 uppercase px-2 mb-2">
                                    Incoming ({requests.incoming?.length || 0})
                                </h3>
                                {requests.incoming?.length === 0 ? (
                                    <p className="text-center text-gray-500 text-sm py-4">No pending requests</p>
                                ) : (
                                    <div className="space-y-2">
                                        {requests.incoming?.map((req) => (
                                            <div key={req.id} className="flex items-center gap-3 p-2 bg-white/5 rounded-lg">
                                                <div className="w-10 h-10 rounded-full bg-gradient-to-br from-purple-500 to-pink-500 flex items-center justify-center text-white font-medium">
                                                    {req.sender_name?.charAt(0)?.toUpperCase() || '?'}
                                                </div>
                                                <div className="flex-1">
                                                    <p className="font-medium text-sm">{req.sender_name}</p>
                                                    <p className="text-xs text-gray-500">{req.sender_email}</p>
                                                </div>
                                                <div className="flex gap-1">
                                                    <Button
                                                        size="icon"
                                                        variant="ghost"
                                                        className="h-8 w-8 text-green-400 hover:bg-green-500/20"
                                                        onClick={() => handleRespondToRequest(req.id, 'accept')}
                                                    >
                                                        <Check size={16} />
                                                    </Button>
                                                    <Button
                                                        size="icon"
                                                        variant="ghost"
                                                        className="h-8 w-8 text-red-400 hover:bg-red-500/20"
                                                        onClick={() => handleRespondToRequest(req.id, 'deny')}
                                                    >
                                                        <X size={16} />
                                                    </Button>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>

                            {/* Outgoing */}
                            <div>
                                <h3 className="text-xs font-medium text-gray-500 uppercase px-2 mb-2">
                                    Sent ({requests.outgoing?.length || 0})
                                </h3>
                                {requests.outgoing?.length === 0 ? (
                                    <p className="text-center text-gray-500 text-sm py-4">No pending requests</p>
                                ) : (
                                    <div className="space-y-2">
                                        {requests.outgoing?.map((req) => (
                                            <div key={req.id} className="flex items-center gap-3 p-2 bg-white/5 rounded-lg">
                                                <div className="w-10 h-10 rounded-full bg-gray-600 flex items-center justify-center text-white">
                                                    <User size={16} />
                                                </div>
                                                <div className="flex-1">
                                                    <p className="font-medium text-sm">{req.receiver_name || req.receiver_email}</p>
                                                    <p className="text-xs text-gray-500">Pending...</p>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </motion.div>
                    )}

                    {activeTab === 'add' && (
                        <motion.div
                            key="add"
                            initial={{ opacity: 0 }}
                            animate={{ opacity: 1 }}
                            exit={{ opacity: 0 }}
                            className="p-4"
                        >
                            <div className="space-y-4">
                                <div>
                                    <label className="text-sm text-gray-400 mb-2 block">
                                        Add Friend by Email
                                    </label>
                                    <div className="flex gap-2">
                                        <Input
                                            type="email"
                                            value={addEmail}
                                            onChange={(e) => setAddEmail(e.target.value)}
                                            placeholder="friend@example.com"
                                            className="bg-white/5 border-white/10"
                                            onKeyDown={(e) => e.key === 'Enter' && handleSendRequest()}
                                        />
                                        <Button 
                                            onClick={handleSendRequest}
                                            disabled={loading || !addEmail.trim()}
                                            className="bg-fuchsia-500 hover:bg-fuchsia-600"
                                        >
                                            {loading ? <Loader2 className="animate-spin" size={16} /> : <Send size={16} />}
                                        </Button>
                                    </div>
                                </div>
                                
                                {addError && (
                                    <p className="text-sm text-red-400">{addError}</p>
                                )}
                                {addSuccess && (
                                    <p className="text-sm text-green-400">{addSuccess}</p>
                                )}
                            </div>
                        </motion.div>
                    )}
                </AnimatePresence>
            </ScrollArea>

            {/* DM Panel */}
            <AnimatePresence>
                {selectedFriend && (
                    <DirectMessagePanel
                        friend={selectedFriend}
                        onClose={() => {
                            setSelectedFriend(null);
                            loadUnreadCounts();
                        }}
                        onRemoveFriend={handleRemoveFriend}
                    />
                )}
            </AnimatePresence>
        </motion.div>
    );
}

// Direct Message Panel Component
function DirectMessagePanel({ friend, onClose, onRemoveFriend }) {
    const [messages, setMessages] = useState([]);
    const [input, setInput] = useState('');
    const [loading, setLoading] = useState(false);
    const [sending, setSending] = useState(false);
    const scrollRef = useRef(null);

    useEffect(() => {
        loadMessages();
        const interval = setInterval(loadMessages, 5000); // Poll for new messages
        return () => clearInterval(interval);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [friend.friend_user_id]);

    useEffect(() => {
        if (scrollRef.current) {
            scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
        }
    }, [messages]);

    const loadMessages = async () => {
        try {
            const res = await friendsAPI.getMessages(friend.friend_user_id);
            // Defensive: ensure messages is always an array
            setMessages(Array.isArray(res.data) ? res.data : []);
        } catch (error) {
            console.error('Failed to load messages:', error);
            setMessages([]);
        }
    };

    const handleSend = async () => {
        if (!input.trim() || sending) return;
        setSending(true);
        try {
            await friendsAPI.sendMessage(friend.friend_user_id, input.trim());
            setInput('');
            loadMessages();
        } catch (error) {
            console.error('Failed to send message:', error);
        } finally {
            setSending(false);
        }
    };

    return (
        <motion.div
            initial={{ x: 300, opacity: 0 }}
            animate={{ x: 0, opacity: 1 }}
            exit={{ x: 300, opacity: 0 }}
            className="absolute inset-0 bg-[#0B0F19] flex flex-col"
        >
            {/* Header */}
            <div className="p-4 border-b border-white/10 flex items-center gap-3">
                <Button variant="ghost" size="icon" onClick={onClose}>
                    <ChevronRight size={18} className="rotate-180" />
                </Button>
                <div className="w-10 h-10 rounded-full bg-gradient-to-br from-fuchsia-500 to-cyan-500 flex items-center justify-center text-white font-medium">
                    {friend.display_name?.charAt(0)?.toUpperCase() || '?'}
                </div>
                <div className="flex-1">
                    <p className="font-medium">{friend.display_name || friend.email?.split('@')[0]}</p>
                    <p className="text-xs text-gray-500">{friend.email}</p>
                </div>
                <Button
                    variant="ghost"
                    size="sm"
                    className="text-red-400 hover:text-red-300 hover:bg-red-500/10"
                    onClick={() => onRemoveFriend(friend.friend_user_id)}
                >
                    Remove
                </Button>
            </div>

            {/* Messages */}
            <ScrollArea className="flex-1 p-4" ref={scrollRef}>
                <div className="space-y-3">
                    {messages.map((msg) => {
                        const isMe = msg.sender_id !== friend.friend_user_id;
                        return (
                            <div
                                key={msg.id}
                                className={`flex ${isMe ? 'justify-end' : 'justify-start'}`}
                            >
                                <div
                                    className={`max-w-[80%] px-3 py-2 rounded-lg ${
                                        msg.message_type === 'system'
                                            ? 'bg-gray-800 text-gray-400 text-center text-sm italic'
                                            : isMe
                                                ? 'bg-fuchsia-500 text-white'
                                                : 'bg-white/10 text-white'
                                    }`}
                                >
                                    <p className="text-sm whitespace-pre-wrap">{msg.message}</p>
                                    <p className="text-xs opacity-50 mt-1">
                                        {new Date(msg.created_at).toLocaleTimeString()}
                                    </p>
                                </div>
                            </div>
                        );
                    })}
                </div>
            </ScrollArea>

            {/* Input */}
            <div className="p-4 border-t border-white/10">
                <div className="flex gap-2">
                    <Input
                        value={input}
                        onChange={(e) => setInput(e.target.value)}
                        placeholder="Type a message..."
                        className="bg-white/5 border-white/10"
                        onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                    />
                    <Button 
                        onClick={handleSend}
                        disabled={sending || !input.trim()}
                        className="bg-fuchsia-500 hover:bg-fuchsia-600"
                    >
                        {sending ? <Loader2 className="animate-spin" size={16} /> : <Send size={16} />}
                    </Button>
                </div>
            </div>
        </motion.div>
    );
}

export default FriendsSidebar;
