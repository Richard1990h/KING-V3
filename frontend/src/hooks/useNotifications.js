import { useState, useEffect, useCallback, useRef } from 'react';
import { friendsAPI } from '../lib/api';

const API_BASE = process.env.REACT_APP_BACKEND_URL;

/**
 * useNotifications - Real-time notifications for DMs and friend requests
 * 
 * Features:
 * - WebSocket connection for real-time updates
 * - Polling fallback when WebSocket unavailable
 * - Unread counts for DMs
 * - Friend request notifications
 * - Auto-reconnect
 */
export function useNotifications(userId) {
    const [unreadDMs, setUnreadDMs] = useState(0);
    const [unreadByUser, setUnreadByUser] = useState({});
    const [pendingRequests, setPendingRequests] = useState(0);
    const [isConnected, setIsConnected] = useState(false);
    const [notifications, setNotifications] = useState([]);
    const wsRef = useRef(null);
    const pollIntervalRef = useRef(null);
    const reconnectTimeoutRef = useRef(null);

    // Load initial counts
    const loadCounts = useCallback(async () => {
        if (!userId) return;
        
        try {
            const [unreadRes, requestsRes] = await Promise.all([
                friendsAPI.getUnreadCount().catch(() => ({ data: { total: 0, by_user: [] } })),
                friendsAPI.getRequests().catch(() => ({ data: { incoming: [] } }))
            ]);
            
            const total = unreadRes.data?.total || 0;
            const byUser = {};
            const byUserData = Array.isArray(unreadRes.data?.by_user) ? unreadRes.data.by_user : [];
            byUserData.forEach(u => {
                if (u && u.sender_id !== undefined) {
                    byUser[u.sender_id] = u.count || 0;
                }
            });
            
            setUnreadDMs(total);
            setUnreadByUser(byUser);
            
            const incoming = Array.isArray(requestsRes.data?.incoming) ? requestsRes.data.incoming : [];
            setPendingRequests(incoming.length);
        } catch (error) {
            console.error('Failed to load notification counts:', error);
        }
    }, [userId]);

    // Connect to WebSocket for real-time notifications
    const connect = useCallback(() => {
        if (!userId) return;

        try {
            const wsUrl = API_BASE
                .replace('https://', 'wss://')
                .replace('http://', 'ws://');
            
            const ws = new WebSocket(`${wsUrl}/api/notifications/ws?userId=${userId}`);

            ws.onopen = () => {
                console.log('Notifications WebSocket connected');
                setIsConnected(true);
                // Stop polling when WebSocket connected
                if (pollIntervalRef.current) {
                    clearInterval(pollIntervalRef.current);
                    pollIntervalRef.current = null;
                }
            };

            ws.onmessage = (event) => {
                try {
                    const message = JSON.parse(event.data);
                    handleMessage(message);
                } catch (error) {
                    console.error('Error parsing notification message:', error);
                }
            };

            ws.onclose = () => {
                console.log('Notifications WebSocket disconnected');
                setIsConnected(false);
                
                // Start polling as fallback
                if (!pollIntervalRef.current) {
                    pollIntervalRef.current = setInterval(loadCounts, 30000);
                }
                
                // Auto-reconnect after 5 seconds
                reconnectTimeoutRef.current = setTimeout(() => {
                    if (wsRef.current === ws) {
                        connect();
                    }
                }, 5000);
            };

            ws.onerror = (error) => {
                console.error('Notifications WebSocket error:', error);
            };

            wsRef.current = ws;
        } catch (error) {
            console.error('Failed to connect WebSocket:', error);
            // Use polling fallback
            if (!pollIntervalRef.current) {
                pollIntervalRef.current = setInterval(loadCounts, 30000);
            }
        }
    }, [userId, loadCounts]);

    // Handle incoming WebSocket messages
    const handleMessage = useCallback((message) => {
        switch (message.type) {
            case 'new_dm':
                setUnreadDMs(prev => prev + 1);
                setUnreadByUser(prev => ({
                    ...prev,
                    [message.senderId]: (prev[message.senderId] || 0) + 1
                }));
                addNotification({
                    id: Date.now(),
                    type: 'dm',
                    title: 'New Message',
                    message: `${message.senderName}: ${message.preview}`,
                    senderId: message.senderId,
                    timestamp: new Date().toISOString()
                });
                break;
            
            case 'friend_request':
                setPendingRequests(prev => prev + 1);
                addNotification({
                    id: Date.now(),
                    type: 'friend_request',
                    title: 'Friend Request',
                    message: `${message.senderName} sent you a friend request`,
                    senderId: message.senderId,
                    timestamp: new Date().toISOString()
                });
                break;
            
            case 'friend_accepted':
                addNotification({
                    id: Date.now(),
                    type: 'friend_accepted',
                    title: 'Friend Request Accepted',
                    message: `${message.userName} accepted your friend request`,
                    userId: message.userId,
                    timestamp: new Date().toISOString()
                });
                break;
            
            case 'project_shared':
                addNotification({
                    id: Date.now(),
                    type: 'project_shared',
                    title: 'Project Shared',
                    message: `${message.ownerName} shared "${message.projectName}" with you`,
                    projectId: message.projectId,
                    timestamp: new Date().toISOString()
                });
                break;

            case 'counts':
                // Full count update from server
                setUnreadDMs(message.unreadDMs || 0);
                setUnreadByUser(message.unreadByUser || {});
                setPendingRequests(message.pendingRequests || 0);
                break;
        }
    }, []);

    // Add notification to list
    const addNotification = useCallback((notification) => {
        setNotifications(prev => [notification, ...prev].slice(0, 50)); // Keep last 50
        
        // Play notification sound (optional)
        // const audio = new Audio('/notification.mp3');
        // audio.volume = 0.3;
        // audio.play().catch(() => {});
        
        // Show browser notification if permitted
        if (Notification.permission === 'granted') {
            new Notification(notification.title, {
                body: notification.message,
                icon: '/favicon.ico'
            });
        }
    }, []);

    // Mark DMs as read for a user
    const markAsRead = useCallback((friendUserId) => {
        setUnreadByUser(prev => {
            const count = prev[friendUserId] || 0;
            setUnreadDMs(total => Math.max(0, total - count));
            const { [friendUserId]: _, ...rest } = prev;
            return rest;
        });
    }, []);

    // Clear notification
    const clearNotification = useCallback((notificationId) => {
        setNotifications(prev => prev.filter(n => n.id !== notificationId));
    }, []);

    // Clear all notifications
    const clearAllNotifications = useCallback(() => {
        setNotifications([]);
    }, []);

    // Request browser notification permission
    const requestPermission = useCallback(async () => {
        if ('Notification' in window && Notification.permission === 'default') {
            await Notification.requestPermission();
        }
    }, []);

    // Connect on mount
    useEffect(() => {
        if (!userId) return;
        
        loadCounts();
        connect();
        requestPermission();

        return () => {
            if (reconnectTimeoutRef.current) {
                clearTimeout(reconnectTimeoutRef.current);
            }
            if (pollIntervalRef.current) {
                clearInterval(pollIntervalRef.current);
            }
            if (wsRef.current) {
                wsRef.current.close();
            }
        };
    }, [userId, loadCounts, connect, requestPermission]);

    // Refresh counts periodically when WebSocket not connected
    useEffect(() => {
        if (!isConnected && !pollIntervalRef.current && userId) {
            pollIntervalRef.current = setInterval(loadCounts, 30000);
        }
        return () => {
            if (pollIntervalRef.current && isConnected) {
                clearInterval(pollIntervalRef.current);
                pollIntervalRef.current = null;
            }
        };
    }, [isConnected, userId, loadCounts]);

    return {
        unreadDMs,
        unreadByUser,
        pendingRequests,
        notifications,
        isConnected,
        markAsRead,
        clearNotification,
        clearAllNotifications,
        refresh: loadCounts
    };
}

export default useNotifications;
