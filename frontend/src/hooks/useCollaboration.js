import { useState, useEffect, useCallback, useRef } from 'react';

const API_BASE = process.env.REACT_APP_BACKEND_URL;

/**
 * useCollaboration - Real-time collaboration hook using WebSocket
 * 
 * Features:
 * - Live cursor positions
 * - Real-time file edits
 * - Collaborator presence
 * - Auto-reconnect
 */
export function useCollaboration(projectId, userId, userName) {
    const [collaborators, setCollaborators] = useState([]);
    const [isConnected, setIsConnected] = useState(false);
    const [cursors, setCursors] = useState({});
    const wsRef = useRef(null);
    const reconnectTimeoutRef = useRef(null);

    // Connect to WebSocket
    const connect = useCallback(() => {
        if (!projectId || !userId || !userName) return;

        const wsUrl = API_BASE
            .replace('https://', 'wss://')
            .replace('http://', 'ws://');
        
        const ws = new WebSocket(
            `${wsUrl}/api/collaboration/ws/${projectId}?userId=${userId}&userName=${encodeURIComponent(userName)}`
        );

        ws.onopen = () => {
            console.log('Collaboration WebSocket connected');
            setIsConnected(true);
        };

        ws.onmessage = (event) => {
            try {
                const message = JSON.parse(event.data);
                handleMessage(message);
            } catch (error) {
                console.error('Error parsing collaboration message:', error);
            }
        };

        ws.onclose = () => {
            console.log('Collaboration WebSocket disconnected');
            setIsConnected(false);
            
            // Auto-reconnect after 3 seconds
            reconnectTimeoutRef.current = setTimeout(() => {
                if (wsRef.current === ws) {
                    connect();
                }
            }, 3000);
        };

        ws.onerror = (error) => {
            console.error('Collaboration WebSocket error:', error);
        };

        wsRef.current = ws;
    }, [projectId, userId, userName]);

    // Handle incoming messages
    const handleMessage = useCallback((message) => {
        switch (message.type) {
            case 'sync':
            case 'join':
            case 'leave':
                setCollaborators(message.collaborators || []);
                break;
            
            case 'cursor':
                setCursors(prev => ({
                    ...prev,
                    [message.userId]: {
                        userId: message.userId,
                        userName: message.userName,
                        userColor: message.userColor,
                        fileId: message.fileId,
                        filePath: message.filePath,
                        cursor: message.cursor
                    }
                }));
                break;
            
            case 'edit':
                // Emit custom event for edit operations
                window.dispatchEvent(new CustomEvent('collaborationEdit', { detail: message }));
                break;
            
            case 'file_open':
                // Update cursor to show which file the user is viewing
                setCursors(prev => ({
                    ...prev,
                    [message.userId]: {
                        ...prev[message.userId],
                        userId: message.userId,
                        userName: message.userName,
                        userColor: message.userColor,
                        fileId: message.fileId,
                        filePath: message.filePath
                    }
                }));
                break;
        }
    }, []);

    // Send cursor position
    const sendCursor = useCallback((fileId, filePath, cursor) => {
        if (wsRef.current?.readyState === WebSocket.OPEN) {
            wsRef.current.send(JSON.stringify({
                type: 'cursor',
                fileId,
                filePath,
                cursor
            }));
        }
    }, []);

    // Send edit operation
    const sendEdit = useCallback((fileId, filePath, edit) => {
        if (wsRef.current?.readyState === WebSocket.OPEN) {
            wsRef.current.send(JSON.stringify({
                type: 'edit',
                fileId,
                filePath,
                edit: {
                    ...edit,
                    timestamp: Date.now()
                }
            }));
        }
    }, []);

    // Send file open notification
    const sendFileOpen = useCallback((fileId, filePath) => {
        if (wsRef.current?.readyState === WebSocket.OPEN) {
            wsRef.current.send(JSON.stringify({
                type: 'file_open',
                fileId,
                filePath
            }));
        }
    }, []);

    // Connect on mount
    useEffect(() => {
        connect();

        return () => {
            if (reconnectTimeoutRef.current) {
                clearTimeout(reconnectTimeoutRef.current);
            }
            if (wsRef.current) {
                wsRef.current.close();
            }
        };
    }, [connect]);

    return {
        collaborators,
        cursors,
        isConnected,
        sendCursor,
        sendEdit,
        sendFileOpen
    };
}

/**
 * CollaboratorCursor - Renders a cursor for a remote collaborator
 */
export function CollaboratorCursor({ cursor, editorRef }) {
    if (!cursor?.cursor || !editorRef?.current) return null;

    const { line, column } = cursor.cursor;
    
    // Calculate position based on line and column
    // This is a simplified version - real implementation would use editor API
    const top = (line - 1) * 20; // Approximate line height
    const left = column * 8; // Approximate character width

    return (
        <div
            className="absolute pointer-events-none z-50"
            style={{
                top: `${top}px`,
                left: `${left}px`,
                transform: 'translateY(-100%)'
            }}
        >
            {/* Cursor line */}
            <div
                className="w-0.5 h-5 animate-pulse"
                style={{ backgroundColor: cursor.userColor }}
            />
            {/* User label */}
            <div
                className="absolute left-0 top-0 text-xs text-white px-1 rounded whitespace-nowrap"
                style={{ 
                    backgroundColor: cursor.userColor,
                    transform: 'translateY(-100%)'
                }}
            >
                {cursor.userName}
            </div>
        </div>
    );
}

/**
 * CollaboratorAvatars - Shows active collaborators
 */
export function CollaboratorAvatars({ collaborators, currentUserId }) {
    const others = collaborators.filter(c => c.userId !== currentUserId);
    
    if (others.length === 0) return null;

    return (
        <div className="flex items-center gap-1">
            <span className="text-xs text-gray-400 mr-1">
                {others.length} collaborator{others.length !== 1 ? 's' : ''}
            </span>
            <div className="flex -space-x-2">
                {others.slice(0, 5).map((collab) => (
                    <div
                        key={collab.userId}
                        className="w-6 h-6 rounded-full flex items-center justify-center text-xs font-medium text-white border-2 border-gray-900"
                        style={{ backgroundColor: collab.userColor }}
                        title={collab.userName}
                    >
                        {collab.userName?.charAt(0)?.toUpperCase() || '?'}
                    </div>
                ))}
                {others.length > 5 && (
                    <div className="w-6 h-6 rounded-full flex items-center justify-center text-xs font-medium text-white bg-gray-600 border-2 border-gray-900">
                        +{others.length - 5}
                    </div>
                )}
            </div>
        </div>
    );
}

export default useCollaboration;
