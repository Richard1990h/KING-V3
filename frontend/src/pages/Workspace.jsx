import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { useTheme } from '../lib/theme';
import { projectsAPI, filesAPI, chatAPI, agentsAPI } from '../lib/api';
import api from '../lib/api';
import { formatCredits, getSyntaxLanguage, AGENT_COLORS, debounce } from '../lib/utils';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs';
import { ScrollArea } from '../components/ui/scroll-area';
import { Checkbox } from '../components/ui/checkbox';
import { Textarea } from '../components/ui/textarea';
import { 
    Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger 
} from '../components/ui/dialog';
import {
    DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger
} from '../components/ui/dropdown-menu';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism';
import { MessageContent } from '../components/CodeBlock';
import { CodeRunner } from '../components/CodeRunner';
import { 
    Zap, ArrowLeft, Play, Hammer, Save, Download, Send, Plus, Trash2,
    File, FileCode, Folder, ChevronRight, ChevronDown, MoreVertical,
    MessageSquare, Terminal, Cpu, LayoutGrid, Search, Code, TestTube,
    Bug, CheckCircle, Mic, RefreshCw, X, Maximize2, Minimize2, History,
    FolderOpen, ListTodo, Edit3, Check, AlertCircle, Loader2, Upload,
    PlayCircle, Cloud, Users
} from 'lucide-react';

const AGENT_ICONS = {
    planner: LayoutGrid,
    researcher: Search,
    developer: Code,
    test_designer: TestTube,
    executor: Play,
    debugger: Bug,
    verifier: CheckCircle
};

export default function Workspace() {
    const { projectId } = useParams();
    const navigate = useNavigate();
    const { user, refreshUser } = useAuth();
    const { t } = useI18n();
    const { theme } = useTheme();
    
    // State
    const [project, setProject] = useState(null);
    const [files, setFiles] = useState([]);
    const [selectedFile, setSelectedFile] = useState(null);
    const [fileContent, setFileContent] = useState('');
    const [originalContent, setOriginalContent] = useState('');
    const [chatMessages, setChatMessages] = useState([]);
    const [chatInput, setChatInput] = useState('');
    const [agents, setAgents] = useState([]);
    const [enabledAgents, setEnabledAgents] = useState(['developer']);
    const [loading, setLoading] = useState(true);
    
    // To-Do List state for AI building
    const [todoItems, setTodoItems] = useState([]);
    const [showTodoPanel, setShowTodoPanel] = useState(false);
    const [editingTodo, setEditingTodo] = useState(null);
    const [aiWorkingPhase, setAiWorkingPhase] = useState(null); // 'planning', 'researching', 'developing', 'testing', null
    const [activeAgentId, setActiveAgentId] = useState(null); // Track which agent is currently working
    const [planApproved, setPlanApproved] = useState(false);
    const [saving, setSaving] = useState(false);
    const [sending, setSending] = useState(false);
    const [building, setBuilding] = useState(false);
    const [running, setRunning] = useState(false);
    const [output, setOutput] = useState([]);
    const [activeTab, setActiveTab] = useState('chat');
    const [showNewFile, setShowNewFile] = useState(false);
    const [newFileName, setNewFileName] = useState('');
    
    // New states for enhanced features
    const [chatFullscreen, setChatFullscreen] = useState(false);
    const [globalConversations, setGlobalConversations] = useState([]);
    const [showConversationLoader, setShowConversationLoader] = useState(false);
    const [loadingConversations, setLoadingConversations] = useState(false);
    const [multiAgentMode, setMultiAgentMode] = useState(true); // ON by default
    
    // File upload state
    const [showUploadDialog, setShowUploadDialog] = useState(false);
    const [uploading, setUploading] = useState(false);
    const fileUploadRef = useRef(null);
    
    // Code Runner state
    const [showCodeRunner, setShowCodeRunner] = useState(false);
    
    // Backend availability state
    const [backendAvailable, setBackendAvailable] = useState(true);
    
    const chatEndRef = useRef(null);
    const outputEndRef = useRef(null);
    
    // Get primary color from theme for glowing effects
    const primaryColor = theme?.primary_color || '#d946ef';

    // Helper function to clean up raw JSON from chat messages
    const cleanMessageContent = (content) => {
        if (!content || typeof content !== 'string') return content;
        
        // Check if content looks like raw JSON (tasks/plan data)
        const isRawJson = content.includes('"id":') && 
                         (content.includes('"task-') || content.includes('"agent_type"') || 
                          content.includes('"estimated_tokens"') || content.includes('"deliverables"'));
        
        if (isRawJson) {
            // Try to parse and format nicely
            try {
                // Extract task info from JSON-like content
                const taskMatches = content.match(/"title":\s*"([^"]+)"/g);
                if (taskMatches && taskMatches.length > 0) {
                    const tasks = taskMatches.map(m => m.replace(/"title":\s*"/, '').replace(/"$/, ''));
                    return `📋 Build plan created with ${tasks.length} task(s):\n\n${tasks.map((t, i) => `${i + 1}. ${t}`).join('\n')}\n\nReview the To-Do tab to see details and approve the plan.`;
                }
            } catch (e) {
                // If parsing fails, return a generic message
            }
            return '📋 Build plan has been created. Check the To-Do tab to review and approve the tasks.';
        }
        
        // Clean up code blocks and file markers if present
        let cleaned = content;
        
        // Remove FILE: markers and code blocks (keep just a summary)
        if (cleaned.includes('FILE:') && cleaned.includes('```')) {
            const fileMatches = cleaned.match(/FILE:\s*([^\n]+)/g);
            if (fileMatches && fileMatches.length > 0) {
                const fileNames = fileMatches.map(m => m.replace('FILE:', '').trim());
                const nonCodePart = cleaned.split('FILE:')[0].trim();
                cleaned = nonCodePart || `Created ${fileNames.length} file(s): ${fileNames.join(', ')}`;
            }
        }
        
        return cleaned;
    };

    // Load project data
    useEffect(() => {
        loadProject();
        loadAgents();
        loadGlobalConversations();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [projectId]);

    // Auto-scroll chat
    useEffect(() => {
        chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [chatMessages]);

    // Auto-scroll output
    useEffect(() => {
        outputEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [output]);

    const loadProject = async () => {
        try {
            const [projectRes, filesRes, chatRes] = await Promise.all([
                projectsAPI.get(projectId),
                filesAPI.getAll(projectId),
                chatAPI.getHistory(projectId)
            ]);
            setProject(projectRes.data || null);
            // Defensive: ensure arrays are always arrays
            const filesData = Array.isArray(filesRes.data) ? filesRes.data : [];
            const chatData = Array.isArray(chatRes.data) ? chatRes.data : [];
            setFiles(filesData);
            setChatMessages(chatData);
            
            // Select first file
            if (filesData.length > 0) {
                selectFile(filesData[0]);
            }
        } catch (error) {
            console.error('Failed to load project:', error);
            // Don't navigate away on error - allow user to stay and retry
            setFiles([]);
            setChatMessages([]);
        } finally {
            setLoading(false);
        }
    };

    const loadAgents = async () => {
        try {
            const res = await agentsAPI.getAll();
            // Defensive: ensure agents is always an array
            const agentsData = Array.isArray(res.data) ? res.data : [];
            setAgents(agentsData);
            // Enable all agents by default (they work together)
            setEnabledAgents(agentsData.map(a => a.id));
        } catch (error) {
            console.error('Failed to load agents:', error);
            // Set default agents on error for graceful degradation
            setAgents([]);
            setEnabledAgents([]);
        }
    };

    const loadGlobalConversations = async () => {
        setLoadingConversations(true);
        try {
            const res = await api.get('/conversations');
            // Defensive: ensure conversations is always an array
            setGlobalConversations(Array.isArray(res.data) ? res.data : []);
        } catch (error) {
            console.error('Failed to load global conversations:', error);
            setGlobalConversations([]);
        } finally {
            setLoadingConversations(false);
        }
    };

    const loadConversationIntoChat = async (conversationId) => {
        try {
            const res = await api.get(`/conversations/${conversationId}/messages`);
            // Defensive: ensure messages is always an array
            setChatMessages(Array.isArray(res.data) ? res.data : []);
            setShowConversationLoader(false);
        } catch (error) {
            console.error('Failed to load conversation:', error);
            setChatMessages([]);
        }
    };

    const selectFile = (file) => {
        // Save current file if modified
        if (selectedFile && fileContent !== originalContent) {
            saveFile();
        }
        setSelectedFile(file);
        setFileContent(file.content);
        setOriginalContent(file.content);
        // Collapse chat when selecting a file (double-click effect)
        if (chatFullscreen) {
            setChatFullscreen(false);
        }
    };

    const handleFileDoubleClick = (file) => {
        selectFile(file);
        setChatFullscreen(false);
    };

    const saveFile = async () => {
        if (!selectedFile || fileContent === originalContent) return;
        setSaving(true);
        try {
            await filesAPI.update(projectId, selectedFile.id, { content: fileContent });
            setOriginalContent(fileContent);
            // Update files list
            setFiles(files.map(f => f.id === selectedFile.id ? { ...f, content: fileContent } : f));
        } catch (error) {
            console.error('Failed to save file:', error);
        } finally {
            setSaving(false);
        }
    };

    // Debounced auto-save
    // eslint-disable-next-line react-hooks/exhaustive-deps
    const debouncedSave = useCallback(debounce(saveFile, 2000), [selectedFile]);

    useEffect(() => {
        if (fileContent !== originalContent) {
            debouncedSave();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [fileContent]);

    const createFile = async () => {
        if (!newFileName.trim()) return;
        try {
            const res = await filesAPI.create(projectId, { path: newFileName, content: '' });
            setFiles([...files, res.data]);
            selectFile(res.data);
            setShowNewFile(false);
            setNewFileName('');
        } catch (error) {
            console.error('Failed to create file:', error);
        }
    };

    const deleteFile = async (fileId) => {
        try {
            await filesAPI.delete(projectId, fileId);
            setFiles(files.filter(f => f.id !== fileId));
            if (selectedFile?.id === fileId) {
                setSelectedFile(null);
                setFileContent('');
            }
        } catch (error) {
            console.error('Failed to delete file:', error);
        }
    };

    const loadFiles = async () => {
        try {
            const res = await filesAPI.getAll(projectId);
            // Defensive: ensure files is always an array
            setFiles(Array.isArray(res.data) ? res.data : []);
        } catch (error) {
            console.error('Failed to load files:', error);
            setFiles([]);
        }
    };

    // Handle file/zip upload
    const handleFileUpload = async (e) => {
        const uploadedFiles = e.target.files;
        if (!uploadedFiles || uploadedFiles.length === 0) return;
        
        setUploading(true);
        
        try {
            for (const file of uploadedFiles) {
                if (file.name.endsWith('.zip')) {
                    // Handle ZIP file upload
                    const formData = new FormData();
                    formData.append('file', file);
                    await api.post(`/api/projects/${projectId}/upload-zip`, formData, {
                        headers: { 'Content-Type': 'multipart/form-data' }
                    });
                } else {
                    // Handle single file upload
                    const content = await file.text();
                    await filesAPI.create(projectId, { path: file.name, content });
                }
            }
            await loadFiles();
            setShowUploadDialog(false);
        } catch (error) {
            console.error('Failed to upload files:', error);
            alert('Failed to upload files. Please try again.');
        } finally {
            setUploading(false);
            if (fileUploadRef.current) {
                fileUploadRef.current.value = '';
            }
        }
    };

    // Export project as ZIP
    const exportProject = async () => {
        try {
            const res = await api.get(`/api/projects/${projectId}/export`, {
                responseType: 'blob'
            });
            const url = window.URL.createObjectURL(new Blob([res.data]));
            const link = document.createElement('a');
            link.href = url;
            link.setAttribute('download', `${project?.name || 'project'}.zip`);
            document.body.appendChild(link);
            link.click();
            link.parentNode.removeChild(link);
        } catch (error) {
            console.error('Failed to export project:', error);
            alert('Failed to export project. Please try again.');
        }
    };

    const sendMessage = async () => {
        if (!chatInput.trim() || sending) return;
        setSending(true);
        
        const message = chatInput;
        setChatInput('');
        
        // Check if this is a build request when multi-agent mode is ON
        const isBuildRequest = multiAgentMode && (
            message.toLowerCase().includes('build') ||
            message.toLowerCase().includes('create') ||
            message.toLowerCase().includes('make') ||
            message.toLowerCase().includes('develop') ||
            message.toLowerCase().includes('generate') ||
            message.toLowerCase().includes('write') ||
            message.toLowerCase().includes('add')
        );
        
        try {
            // Add user message to chat
            setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                role: 'user', 
                content: message, 
                timestamp: new Date().toISOString() 
            }]);

            if (isBuildRequest && multiAgentMode) {
                // Start the AI building flow
                await startAIBuildingFlow(message);
            } else {
                // Regular chat - just get AI response
                const res = await chatAPI.send(projectId, message, enabledAgents);
                
                // Defensive: safely access response data
                const aiMessage = res.data?.ai_message || res.data;
                let cleanContent = aiMessage?.content || 'No response received';
                
                // Check if response contains file info and clean it
                if (cleanContent.includes('FILE:') || cleanContent.includes('```')) {
                    // Extract just the summary/explanation part
                    const lines = cleanContent.split('\n');
                    const summaryLines = lines.filter(line => 
                        !line.startsWith('FILE:') && 
                        !line.startsWith('```') &&
                        !line.match(/^\s*[\[\{]/) // Filter out JSON
                    );
                    cleanContent = summaryLines.join('\n').trim() || 'Code has been generated and added to your files.';
                }
                
                setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                    ...aiMessage,
                    content: cleanContent,
                    role: aiMessage?.role || 'assistant',
                    timestamp: aiMessage?.timestamp || new Date().toISOString()
                }]);
                
                // Refresh files list to show any newly created files
                await loadFiles();
            }
            await refreshUser();
        } catch (error) {
            console.error('Failed to send message:', error);
            if (error.response?.status === 402) {
                setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                    role: 'system', 
                    content: t('error_insufficient_credits', 'Insufficient credits. Please purchase more credits to continue.'), 
                    timestamp: new Date().toISOString() 
                }]);
            } else {
                setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                    role: 'system', 
                    content: `Error: ${error.response?.data?.detail || error.message}`, 
                    timestamp: new Date().toISOString() 
                }]);
            }
        } finally {
            setSending(false);
        }
    };

    const startAIBuildingFlow = async (userRequest) => {
        setActiveTab('todo');
        setAiWorkingPhase('planning');
        setActiveAgentId('planner');
        setPlanApproved(false);
        setTodoItems([]);
        
        try {
            // First, scan existing files for context
            const existingFilesRes = await filesAPI.getAll(projectId);
            const existingFiles = Array.isArray(existingFilesRes.data) ? existingFilesRes.data : [];
            const fileContext = existingFiles.length > 0 
                ? `Existing files: ${existingFiles.map(f => f.path).join(', ')}`
                : 'No existing files';
            
            // Phase 1: Planner creates initial to-do list
            setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                role: 'assistant', 
                agent: 'planner',
                content: `Scanning project structure... ${existingFiles.length} file(s) found.\nAnalyzing your request and creating a build plan...`, 
                timestamp: new Date().toISOString() 
            }]);
            
            const planRes = await api.post(`/ai/plan`, {
                project_id: projectId,
                request: `${userRequest}\n\nContext: ${fileContext}`,
                agents: enabledAgents
            });
            
            // Defensive: ensure tasks is always an array
            const tasks = Array.isArray(planRes.data?.tasks) ? planRes.data.tasks : [];
            if (tasks.length > 0) {
                setTodoItems(tasks.map((task, i) => ({
                    id: Date.now() + i,
                    task: task.description || task.task || 'Task',
                    agent: task.agent || 'developer',
                    completed: false
                })));
                
                // Show task summary in chat
                const taskSummary = tasks.map((t, i) => 
                    `${i + 1}. [${t.agent || 'developer'}] ${t.description || t.task || 'Task'}`
                ).join('\n');
                
                setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                    role: 'assistant', 
                    agent: 'planner',
                    content: `📋 Build plan created:\n\n${taskSummary}\n\nReview the To-Do tab, make any edits, then click "Approve & Build" to start.`, 
                    timestamp: new Date().toISOString() 
                }]);
            } else {
                setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                    role: 'assistant', 
                    agent: 'planner',
                    content: `Could not generate a build plan. Please try rephrasing your request.`, 
                    timestamp: new Date().toISOString() 
                }]);
            }
            
            setAiWorkingPhase(null);
            setActiveAgentId(null);
            
        } catch (error) {
            console.error('AI Building flow error:', error);
            setAiWorkingPhase(null);
            setActiveAgentId(null);
            setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                role: 'system', 
                content: `Error creating build plan: ${error.response?.data?.detail || error.message}`, 
                timestamp: new Date().toISOString() 
            }]);
        }
    };

    const handleStartBuilding = async () => {
        const currentTodos = Array.isArray(todoItems) ? todoItems : [];
        if (currentTodos.length === 0) return;
        
        setAiWorkingPhase('developing');
        setActiveAgentId('developer');
        
        setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
            role: 'assistant', 
            agent: 'developer',
            content: '🔨 Starting to build based on the approved plan...', 
            timestamp: new Date().toISOString() 
        }]);
        
        let filesCreatedTotal = [];
        
        try {
            // Execute each task
            for (let i = 0; i < currentTodos.length; i++) {
                const task = currentTodos[i];
                if (task.completed) continue;
                
                // Set active agent for glowing effect
                setActiveAgentId(task.agent || 'developer');
                
                // Update UI to show current task
                setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                    role: 'assistant', 
                    agent: task.agent || 'developer',
                    content: `⚙️ Working on: ${task.task}`, 
                    timestamp: new Date().toISOString() 
                }]);
                
                // Call AI to execute the task
                const res = await api.post(`/ai/execute-task`, {
                    project_id: projectId,
                    task: task.task,
                    agent: task.agent || 'developer'
                });
                
                // Track created files and save them to the database
                const resFiles = Array.isArray(res.data?.files) ? res.data.files : [];
                if (resFiles.length > 0) {
                    for (const file of resFiles) {
                        try {
                            // Create the file in the database
                            await filesAPI.create(projectId, {
                                path: file.path,
                                content: file.content || ''
                            });
                            filesCreatedTotal.push(file);
                        } catch (fileErr) {
                            console.error('Failed to create file:', file.path, fileErr);
                        }
                    }
                    
                    // Show which files were created
                    const fileNames = resFiles.map(f => f.path).join(', ');
                    setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                        role: 'assistant', 
                        agent: task.agent || 'developer',
                        content: `✅ Created file(s): ${fileNames}`, 
                        timestamp: new Date().toISOString() 
                    }]);
                }
                
                // Mark task complete
                setTodoItems(prev => (Array.isArray(prev) ? prev : []).map(t => 
                    t.id === task.id ? { ...t, completed: true } : t
                ));
            }
            
            // Refresh files list to show all newly created files
            await loadFiles();
            
            setAiWorkingPhase(null);
            setActiveAgentId(null);
            
            // Final summary message
            const totalFiles = filesCreatedTotal.length;
            setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                role: 'assistant', 
                agent: 'verifier',
                content: `🎉 Build complete!\n\n📁 ${totalFiles} file(s) created:\n${filesCreatedTotal.map(f => `• ${f.path}`).join('\n')}\n\nCheck the Files panel to view and edit your code.`, 
                timestamp: new Date().toISOString() 
            }]);
            
        } catch (error) {
            console.error('Build execution error:', error);
            setAiWorkingPhase(null);
            setActiveAgentId(null);
            
            // Still refresh files in case some were created before error
            await loadFiles();
            
            setChatMessages(prev => [...(Array.isArray(prev) ? prev : []), { 
                role: 'system', 
                content: `Error during build: ${error.response?.data?.detail || error.message}`, 
                timestamp: new Date().toISOString() 
            }]);
        }
    };

    const buildProject = async () => {
        setBuilding(true);
        setActiveTab('output');
        setOutput([{ level: 'info', message: 'Starting build...', timestamp: new Date().toISOString() }]);
        
        try {
            // Save current file first
            if (fileContent !== originalContent) {
                await saveFile();
            }
            
            const res = await projectsAPI.build(projectId);
            // Defensive: ensure logs is always an array
            setOutput(Array.isArray(res.data?.logs) ? res.data.logs : [{ level: 'info', message: 'Build completed', timestamp: new Date().toISOString() }]);
        } catch (error) {
            setOutput(prev => [...(Array.isArray(prev) ? prev : []), { level: 'error', message: `Build failed: ${error.response?.data?.detail || error.message}`, timestamp: new Date().toISOString() }]);
        } finally {
            setBuilding(false);
        }
    };

    const runProject = async () => {
        setRunning(true);
        setActiveTab('output');
        setOutput([{ level: 'info', message: 'Running project...', timestamp: new Date().toISOString() }]);
        
        try {
            // Save current file first
            if (fileContent !== originalContent) {
                await saveFile();
            }
            
            const res = await projectsAPI.run(projectId);
            // Defensive: ensure logs is always an array
            setOutput(Array.isArray(res.data?.logs) ? res.data.logs : [{ level: 'info', message: 'Run completed', timestamp: new Date().toISOString() }]);
        } catch (error) {
            setOutput(prev => [...(Array.isArray(prev) ? prev : []), { level: 'error', message: `Run failed: ${error.response?.data?.detail || error.message}`, timestamp: new Date().toISOString() }]);
        } finally {
            setRunning(false);
        }
    };

    const toggleAgent = (agentId) => {
        setEnabledAgents(prev => 
            prev.includes(agentId) 
                ? prev.filter(id => id !== agentId)
                : [...prev, agentId]
        );
    };

    const toggleChatFullscreen = () => {
        setChatFullscreen(!chatFullscreen);
    };

    if (loading) {
        return (
            <div className="min-h-screen bg-[#030712] flex items-center justify-center">
                <div className="w-8 h-8 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
            </div>
        );
    }

    const hasChanges = fileContent !== originalContent;

    return (
        <div className="h-screen bg-[#030712] flex flex-col">
            {/* Header - Mobile Responsive */}
            <header className="h-14 md:h-14 border-b border-white/10 bg-[#0B0F19] flex items-center justify-between px-2 sm:px-4 flex-shrink-0">
                <div className="flex items-center gap-2 sm:gap-4 min-w-0">
                    <Link to="/dashboard" className="flex items-center gap-2 text-gray-400 hover:text-white transition-colors flex-shrink-0" data-testid="back-to-dashboard">
                        <ArrowLeft size={20} />
                    </Link>
                    
                    <div className="flex items-center gap-1 sm:gap-2 min-w-0">
                        <div className="w-6 h-6 rounded bg-fuchsia-500/20 flex items-center justify-center flex-shrink-0">
                            <Code size={14} className="text-fuchsia-400" />
                        </div>
                        <span className="font-semibold truncate max-w-[100px] sm:max-w-[200px]">{project?.name}</span>
                        <span className="text-sm text-gray-500 hidden sm:inline">•</span>
                        <span className="text-sm text-fuchsia-400 hidden sm:inline">{project?.language}</span>
                        <span className="text-sm text-gray-500 hidden md:inline">•</span>
                        <span className="text-sm text-gray-400 hidden md:inline">{Array.isArray(files) ? files.length : 0} FILES</span>
                    </div>
                </div>
                
                <div className="flex items-center gap-1 sm:gap-2">
                    {/* Mobile: icon only, Desktop: icon + text */}
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={buildProject}
                        disabled={building}
                        className="border-white/10 px-2 sm:px-3"
                        data-testid="build-btn"
                    >
                        {building ? <RefreshCw size={16} className="animate-spin" /> : <Hammer size={16} />}
                        <span className="ml-2 hidden sm:inline">{t('workspace_build', 'Build')}</span>
                    </Button>
                    
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={runProject}
                        disabled={running}
                        className="border-green-500/50 text-green-400 hover:bg-green-500/10 px-2 sm:px-3"
                        data-testid="run-btn"
                    >
                        {running ? <RefreshCw size={16} className="animate-spin" /> : <Play size={16} />}
                        <span className="ml-2 hidden sm:inline">{t('workspace_run', 'Run')}</span>
                    </Button>
                    
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={saveFile}
                        disabled={!hasChanges || saving}
                        className="border-white/10 px-2 sm:px-3"
                        data-testid="save-btn"
                    >
                        {saving ? <RefreshCw size={16} className="animate-spin" /> : <Save size={16} />}
                        <span className="ml-2 hidden sm:inline">{t('workspace_save', 'Save')}</span>
                    </Button>
                    
                    <Button variant="outline" size="sm" className="border-white/10 px-2 sm:px-3 hidden md:flex" data-testid="export-btn" onClick={exportProject}>
                        <Download size={16} />
                        <span className="ml-2 hidden lg:inline">{t('common_export', 'Export')}</span>
                    </Button>
                    
                    <Button 
                        variant="outline" 
                        size="sm" 
                        className="border-blue-500/50 text-blue-400 hover:bg-blue-500/10 px-2 sm:px-3 hidden lg:flex" 
                        data-testid="save-to-drive-btn"
                        onClick={async () => {
                            try {
                                await api.post(`/collaboration/${projectId}/export/drive`);
                                alert('Project saved to Google Drive!');
                            } catch (error) {
                                if (error.response?.status === 400) {
                                    alert('Please connect Google Drive in your Profile settings first.');
                                } else {
                                    alert('Failed to save to Google Drive: ' + (error.response?.data?.detail || error.message));
                                }
                            }
                        }}
                    >
                        <Cloud size={16} />
                        <span className="ml-2 hidden xl:inline">Save to Drive</span>
                    </Button>
                    
                    <div className="ml-2 sm:ml-4 flex items-center gap-1 sm:gap-2 px-2 sm:px-3 py-1.5 rounded-lg bg-fuchsia-500/10 border border-fuchsia-500/30">
                        <Zap size={16} className="text-fuchsia-400" />
                        <span className="text-fuchsia-300 font-medium text-sm">{formatCredits(user?.credits || 0)}</span>
                        <span className="text-gray-500 text-xs hidden sm:inline">{t('nav_credits', 'credits').toLowerCase()}</span>
                    </div>
                </div>
            </header>

            {/* Main workspace - Responsive Layout */}
            <div className="flex-1 flex overflow-hidden flex-col md:flex-row">
                {/* File tree - Collapsible on mobile */}
                <div className={`${selectedFile ? 'hidden md:flex' : 'flex'} w-full md:w-60 border-b md:border-b-0 md:border-r border-white/10 bg-[#0B0F19] flex-col max-h-[40vh] md:max-h-full`}>
                    <div className="h-10 flex items-center justify-between px-3 border-b border-white/10">
                        <span className="text-xs font-medium text-gray-400 uppercase tracking-wider">{t('workspace_files', 'FILES')}</span>
                        <div className="flex items-center gap-1">
                            {/* Upload button */}
                            <Dialog open={showUploadDialog} onOpenChange={setShowUploadDialog}>
                                <DialogTrigger asChild>
                                    <Button variant="ghost" size="icon" className="h-6 w-6" title={t('workspace_upload_files', 'Upload files')} data-testid="upload-btn">
                                        <Upload size={14} />
                                    </Button>
                                </DialogTrigger>
                                <DialogContent className="bg-[#0B0F19] border-white/10">
                                    <DialogHeader>
                                        <DialogTitle>{t('workspace_upload_files', 'Upload Files')}</DialogTitle>
                                    </DialogHeader>
                                    <div className="space-y-4 py-4">
                                        <p className="text-sm text-gray-400">
                                            {t('workspace_upload_desc', 'Upload individual files or a ZIP archive for the AI to work on.')}
                                        </p>
                                        <div 
                                            className="border-2 border-dashed border-white/20 rounded-lg p-8 text-center cursor-pointer hover:border-fuchsia-500/50 transition-colors"
                                            onClick={() => fileUploadRef.current?.click()}
                                        >
                                            <Upload className="w-8 h-8 mx-auto mb-2 text-gray-500" />
                                            <p className="text-sm text-gray-400">{t('workspace_select_files', 'Click to select files')}</p>
                                            <p className="text-xs text-gray-500 mt-1">Supports: .zip, .py, .js, .jsx, .ts, .tsx, .html, .css, etc.</p>
                                        </div>
                                        <input
                                            ref={fileUploadRef}
                                            type="file"
                                            multiple
                                            accept=".zip,.py,.js,.jsx,.ts,.tsx,.html,.css,.json,.md,.txt"
                                            onChange={handleFileUpload}
                                            className="hidden"
                                        />
                                        {uploading && (
                                            <div className="flex items-center justify-center gap-2 text-fuchsia-400">
                                                <Loader2 className="w-4 h-4 animate-spin" />
                                                <span className="text-sm">Uploading...</span>
                                            </div>
                                        )}
                                    </div>
                                </DialogContent>
                            </Dialog>
                            
                            {/* New file button */}
                            <Dialog open={showNewFile} onOpenChange={setShowNewFile}>
                                <DialogTrigger asChild>
                                    <Button variant="ghost" size="icon" className="h-6 w-6" data-testid="new-file-btn">
                                        <Plus size={14} />
                                    </Button>
                                </DialogTrigger>
                                <DialogContent className="bg-[#0B0F19] border-white/10">
                                    <DialogHeader>
                                        <DialogTitle>Create New File</DialogTitle>
                                    </DialogHeader>
                                    <div className="space-y-4 py-4">
                                        <Input
                                            value={newFileName}
                                            onChange={(e) => setNewFileName(e.target.value)}
                                            placeholder="filename.py"
                                            className="bg-white/5 border-white/10"
                                            data-testid="new-file-name-input"
                                        />
                                        <Button onClick={createFile} className="w-full bg-fuchsia-500 hover:bg-fuchsia-600" data-testid="create-file-submit">
                                            Create File
                                        </Button>
                                    </div>
                                </DialogContent>
                            </Dialog>
                        </div>
                    </div>
                    
                    <ScrollArea className="flex-1">
                        {files.length === 0 ? (
                            <div className="p-4 text-center text-gray-500">
                                <FileCode className="w-8 h-8 mx-auto mb-2 opacity-50" />
                                <p className="text-sm">{t('workspace_no_files')}</p>
                            </div>
                        ) : (
                            <div className="py-2">
                                {files.map((file) => (
                                    <div
                                        key={file.id}
                                        className={`file-tree-item flex items-center justify-between px-3 py-1.5 cursor-pointer group ${
                                            selectedFile?.id === file.id ? 'active' : ''
                                        }`}
                                        onClick={() => selectFile(file)}
                                        onDoubleClick={() => handleFileDoubleClick(file)}
                                        data-testid={`file-${file.path}`}
                                    >
                                        <div className="flex items-center gap-2 min-w-0">
                                            <FileCode size={14} className="text-fuchsia-400 flex-shrink-0" />
                                            <span className="text-sm truncate">{file.path}</span>
                                        </div>
                                        <DropdownMenu>
                                            <DropdownMenuTrigger asChild>
                                                <Button 
                                                    variant="ghost" 
                                                    size="icon" 
                                                    className="h-6 w-6 opacity-0 group-hover:opacity-100 transition-opacity"
                                                    onClick={(e) => e.stopPropagation()}
                                                >
                                                    <MoreVertical size={14} />
                                                </Button>
                                            </DropdownMenuTrigger>
                                            <DropdownMenuContent className="bg-[#0B0F19] border-white/10">
                                                <DropdownMenuItem 
                                                    onClick={() => deleteFile(file.id)}
                                                    className="text-red-400"
                                                >
                                                    <Trash2 size={14} className="mr-2" />
                                                    {t('common_delete')}
                                                </DropdownMenuItem>
                                            </DropdownMenuContent>
                                        </DropdownMenu>
                                    </div>
                                ))}
                            </div>
                        )}
                    </ScrollArea>
                </div>

                {/* Code editor - hide when chat is fullscreen */}
                {!chatFullscreen && (
                    <div className="flex-1 flex flex-col bg-[#030712]">
                        {selectedFile ? (
                            <>
                                {/* File tab with Run button */}
                                <div className="h-10 border-b border-white/10 flex items-center justify-between px-4">
                                    <div className="flex items-center gap-2 px-3 py-1 bg-white/5 rounded-t border-b-2 border-fuchsia-500">
                                        <FileCode size={14} className="text-fuchsia-400" />
                                        <span className="text-sm">{selectedFile.path}</span>
                                        {hasChanges && <span className="w-2 h-2 rounded-full bg-fuchsia-400" />}
                                    </div>
                                    <Button
                                        variant="ghost"
                                        size="sm"
                                        onClick={() => setShowCodeRunner(true)}
                                        className="h-7 text-cyan-400 hover:text-cyan-300 hover:bg-cyan-500/10"
                                        data-testid="run-code-btn"
                                    >
                                        <PlayCircle size={14} className="mr-1" />
                                        Run Code
                                    </Button>
                                </div>
                                
                                {/* Editor */}
                                <div className="flex-1 overflow-auto relative">
                                    <textarea
                                        value={fileContent}
                                        onChange={(e) => setFileContent(e.target.value)}
                                        className="code-editor w-full h-full p-4 bg-transparent resize-none outline-none"
                                        spellCheck={false}
                                        data-testid="code-editor"
                                    />
                                </div>
                            </>
                        ) : (
                            <div className="flex-1 flex items-center justify-center text-gray-500">
                                <div className="text-center">
                                    <Code size={48} className="mx-auto mb-4 opacity-20" />
                                    <p className="text-lg">{t('workspace_no_file_selected')}</p>
                                    <p className="text-sm">{t('workspace_create_file')} {t('workspace_or_use_ai')}</p>
                                </div>
                            </div>
                        )}
                    </div>
                )}

                {/* Right panel - Chat/Output/LLM - expands when fullscreen */}
                <div className={`${chatFullscreen ? 'flex-1' : 'w-96'} border-l border-white/10 bg-[#0B0F19] flex flex-col transition-all duration-300`}>
                    <Tabs value={activeTab} onValueChange={setActiveTab} className="flex flex-col h-full">
                        <TabsList className="h-10 w-full justify-start rounded-none border-b border-white/10 bg-transparent p-0">
                            <TabsTrigger 
                                value="chat" 
                                className="rounded-none border-b-2 border-transparent data-[state=active]:border-fuchsia-500 data-[state=active]:bg-transparent"
                                data-testid="chat-tab"
                            >
                                <MessageSquare size={16} className="mr-2" />
                                {t('workspace_chat', 'Chat')}
                            </TabsTrigger>
                            <TabsTrigger 
                                value="output" 
                                className="rounded-none border-b-2 border-transparent data-[state=active]:border-fuchsia-500 data-[state=active]:bg-transparent"
                                data-testid="output-tab"
                            >
                                <Terminal size={16} className="mr-2" />
                                {t('workspace_output', 'Output')}
                            </TabsTrigger>
                            <TabsTrigger 
                                value="todo" 
                                className="rounded-none border-b-2 border-transparent data-[state=active]:border-fuchsia-500 data-[state=active]:bg-transparent"
                                data-testid="todo-tab"
                            >
                                <ListTodo size={16} className="mr-2" />
                                {t('workspace_todo', 'To-Do')}
                                {todoItems.length > 0 && (
                                    <span className="ml-2 px-1.5 py-0.5 text-xs rounded-full bg-fuchsia-500/20 text-fuchsia-400">
                                        {todoItems.filter(t => !t.completed).length}
                                    </span>
                                )}
                            </TabsTrigger>
                            {/* Fullscreen toggle */}
                            <div className="ml-auto pr-2 flex items-center gap-2">
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-7 w-7"
                                    onClick={() => setShowConversationLoader(true)}
                                    title="Load conversation from global assistant"
                                >
                                    <FolderOpen size={14} />
                                </Button>
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-7 w-7"
                                    onClick={toggleChatFullscreen}
                                    title={chatFullscreen ? "Exit fullscreen" : "Expand chat"}
                                    data-testid="chat-fullscreen-toggle"
                                >
                                    {chatFullscreen ? <Minimize2 size={14} /> : <Maximize2 size={14} />}
                                </Button>
                            </div>
                        </TabsList>

                        <TabsContent value="chat" className="flex-1 flex flex-col m-0 overflow-hidden">
                            {/* Conversation Loader Dialog */}
                            <Dialog open={showConversationLoader} onOpenChange={setShowConversationLoader}>
                                <DialogContent className="bg-[#0B0F19] border-white/10 max-w-md">
                                    <DialogHeader>
                                        <DialogTitle className="flex items-center gap-2">
                                            <History size={18} />
                                            Load Global Conversation
                                        </DialogTitle>
                                    </DialogHeader>
                                    <div className="py-4">
                                        {loadingConversations ? (
                                            <div className="flex justify-center py-8">
                                                <div className="w-6 h-6 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
                                            </div>
                                        ) : globalConversations.length === 0 ? (
                                            <div className="text-center py-8 text-gray-500">
                                                <History size={32} className="mx-auto mb-2 opacity-50" />
                                                <p>No conversations yet</p>
                                                <p className="text-sm">Start a conversation in the global assistant</p>
                                            </div>
                                        ) : (
                                            <ScrollArea className="max-h-64">
                                                <div className="space-y-2">
                                                    {globalConversations.map(conv => (
                                                        <div
                                                            key={conv.id}
                                                            className="p-3 rounded-lg bg-white/5 hover:bg-white/10 cursor-pointer transition-colors"
                                                            onClick={() => loadConversationIntoChat(conv.id)}
                                                        >
                                                            <p className="text-sm font-medium truncate">{conv.title || 'New Conversation'}</p>
                                                            <p className="text-xs text-gray-500 truncate mt-1">{conv.last_message}</p>
                                                            <p className="text-xs text-gray-600 mt-1">{conv.message_count} messages</p>
                                                        </div>
                                                    ))}
                                                </div>
                                            </ScrollArea>
                                        )}
                                    </div>
                                </DialogContent>
                            </Dialog>

                            {/* Agent pipeline config - compact with hover names */}
                            <div className="p-3 border-b border-white/10">
                                <div className="flex items-center justify-between mb-2">
                                    <div className="flex items-center gap-2">
                                        <Zap size={14} className="text-fuchsia-400" />
                                        <span className="text-xs font-medium text-gray-400 uppercase">{t('workspace_multi_agent', 'Multi-Agent Mode')}</span>
                                    </div>
                                    <button
                                        onClick={() => setMultiAgentMode(!multiAgentMode)}
                                        className={`w-10 h-5 rounded-full flex items-center px-0.5 transition-colors ${
                                            multiAgentMode ? 'bg-fuchsia-500/40' : 'bg-gray-600'
                                        }`}
                                    >
                                        <div className={`w-4 h-4 bg-white rounded-full transition-transform ${
                                            multiAgentMode ? 'translate-x-5' : 'translate-x-0'
                                        }`} />
                                    </button>
                                </div>
                                
                                <p className="text-xs text-gray-500 mb-3">
                                    {multiAgentMode 
                                        ? t('workspace_multi_agent_on', 'AI agents work together to build full projects')
                                        : t('workspace_multi_agent_off', 'AI generates and analyzes code directly in chat')
                                    }
                                </p>
                                
                                {multiAgentMode && (
                                    <>
                                        <div className="text-xs text-gray-500 mb-2">{t('workspace_agents', 'AGENTS')} ({t('workspace_hover_name', 'hover for name')})</div>
                                        <div className="flex flex-wrap gap-1.5">
                                            {agents.map((agent) => {
                                                const Icon = AGENT_ICONS[agent.id] || Code;
                                                const isEnabled = enabledAgents.includes(agent.id);
                                                const isActive = activeAgentId === agent.id;
                                                return (
                                                    <button 
                                                        key={agent.id}
                                                        onClick={() => toggleAgent(agent.id)}
                                                        className={`group relative w-8 h-8 rounded-lg flex items-center justify-center transition-all ${
                                                            isActive 
                                                                ? 'animate-pulse ring-2 bg-white/20' 
                                                                : isEnabled 
                                                                    ? 'bg-white/10 ring-2 ring-fuchsia-500/50' 
                                                                    : 'bg-white/5 opacity-50'
                                                        }`}
                                                        style={{
                                                            boxShadow: isActive 
                                                                ? `0 0 20px ${primaryColor}, 0 0 40px ${primaryColor}40, inset 0 0 10px ${primaryColor}30` 
                                                                : 'none',
                                                            ringColor: isActive ? primaryColor : undefined
                                                        }}
                                                        title={agent.name}
                                                        data-testid={`agent-toggle-${agent.id}`}
                                                    >
                                                        <Icon 
                                                            size={16} 
                                                            style={{ color: isActive ? '#fff' : (isEnabled ? agent.color : '#666') }} 
                                                        />
                                                        {/* Active agent pulsing glow animation */}
                                                        {isActive && (
                                                            <>
                                                                <div 
                                                                    className="absolute inset-0 rounded-lg animate-ping opacity-30"
                                                                    style={{ backgroundColor: primaryColor }}
                                                                />
                                                                <div 
                                                                    className="absolute inset-[-4px] rounded-xl animate-pulse opacity-50"
                                                                    style={{ 
                                                                        backgroundColor: primaryColor,
                                                                        filter: 'blur(8px)'
                                                                    }}
                                                                />
                                                            </>
                                                        )}
                                                        {/* Hover tooltip */}
                                                        <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-xs rounded opacity-0 group-hover:opacity-100 transition-opacity whitespace-nowrap z-10 pointer-events-none">
                                                            {agent.name} {isActive && '(Working...)'}
                                                            <div className="absolute top-full left-1/2 -translate-x-1/2 border-4 border-transparent border-t-gray-900" />
                                                        </div>
                                                    </button>
                                                );
                                            })}
                                        </div>
                                        {activeAgentId && (
                                            <div className="mt-2 flex items-center gap-2 text-xs">
                                                <div 
                                                    className="w-2 h-2 rounded-full animate-pulse"
                                                    style={{ backgroundColor: primaryColor }}
                                                />
                                                <span className="text-gray-400">
                                                    {agents.find(a => a.id === activeAgentId)?.name || 'Agent'} is working...
                                                </span>
                                            </div>
                                        )}
                                    </>
                                )}
                            </div>

                            {/* Chat messages */}
                            <ScrollArea className="flex-1 p-3">
                                <div className="space-y-4">
                                    {chatMessages.length === 0 && (
                                        <div className="text-center py-8">
                                            <div className="w-12 h-12 rounded-xl bg-cyan-500/10 flex items-center justify-center mx-auto mb-3">
                                                <MessageSquare className="w-6 h-6 text-cyan-400" />
                                            </div>
                                            <p className="text-sm text-gray-400">{t('assistant_greeting', 'Hello! How can I assist you today?')}</p>
                                        </div>
                                    )}
                                    {chatMessages.map((msg, i) => (
                                        <div 
                                            key={i}
                                            className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
                                        >
                                            <div 
                                                className={`${chatFullscreen ? 'max-w-[70%]' : 'max-w-[85%]'} rounded-xl px-4 py-2 ${
                                                    msg.role === 'user' 
                                                        ? 'bg-fuchsia-500/20 border border-fuchsia-500/30' 
                                                        : msg.role === 'system'
                                                        ? 'bg-red-500/20 border border-red-500/30'
                                                        : 'bg-white/5 border border-white/10'
                                                }`}
                                            >
                                                {msg.role === 'assistant' && msg.agent_id && (
                                                    <div className="flex items-center gap-1.5 mb-1">
                                                        <span className="text-xs font-medium" style={{ color: AGENT_COLORS[msg.agent_id] }}>
                                                            {agents.find(a => a.id === msg.agent_id)?.name || 'Assistant'}
                                                        </span>
                                                    </div>
                                                )}
                                                {/* Use MessageContent for code block rendering in simple chat mode */}
                                                {!multiAgentMode && msg.role === 'assistant' && msg.content ? (
                                                    <MessageContent content={msg.content} />
                                                ) : (
                                                    <p className="text-sm whitespace-pre-wrap">{cleanMessageContent(msg.content || '')}</p>
                                                )}
                                            </div>
                                        </div>
                                    ))}
                                    {sending && (
                                        <div className="flex justify-start">
                                            <div className="bg-white/5 border border-white/10 rounded-xl px-4 py-2">
                                                <div className="flex items-center gap-2">
                                                    <div className="w-2 h-2 bg-fuchsia-500 rounded-full animate-bounce" />
                                                    <div className="w-2 h-2 bg-fuchsia-500 rounded-full animate-bounce" style={{ animationDelay: '0.1s' }} />
                                                    <div className="w-2 h-2 bg-fuchsia-500 rounded-full animate-bounce" style={{ animationDelay: '0.2s' }} />
                                                </div>
                                            </div>
                                        </div>
                                    )}
                                    <div ref={chatEndRef} />
                                </div>
                            </ScrollArea>

                            {/* Chat input */}
                            <div className="p-3 border-t border-white/10">
                                <div className="flex items-center gap-2">
                                    <Input
                                        value={chatInput}
                                        onChange={(e) => setChatInput(e.target.value)}
                                        onKeyDown={(e) => e.key === 'Enter' && !e.shiftKey && sendMessage()}
                                        placeholder="Describe what you want to build..."
                                        className="flex-1 bg-white/5 border-white/10"
                                        disabled={sending}
                                        data-testid="chat-input"
                                    />
                                    <Button
                                        onClick={sendMessage}
                                        disabled={sending || !chatInput.trim()}
                                        className="bg-fuchsia-500 hover:bg-fuchsia-600"
                                        data-testid="send-message-btn"
                                    >
                                        <Send size={18} />
                                    </Button>
                                </div>
                            </div>
                        </TabsContent>

                        <TabsContent value="output" className="flex-1 m-0 overflow-hidden">
                            <ScrollArea className="h-full p-3">
                                <div className="font-mono text-sm space-y-1">
                                    {output.map((log, i) => (
                                        <div 
                                            key={i}
                                            className={`${
                                                log.level === 'error' ? 'text-red-400' : 
                                                log.level === 'warning' ? 'text-yellow-400' : 
                                                'text-gray-300'
                                            }`}
                                        >
                                            <span className="text-gray-500">[{new Date(log.timestamp).toLocaleTimeString()}]</span>{' '}
                                            {log.message}
                                        </div>
                                    ))}
                                    {output.length === 0 && (
                                        <div className="text-center py-8 text-gray-500">
                                            <Terminal className="w-8 h-8 mx-auto mb-2 opacity-50" />
                                            <p>No output yet</p>
                                            <p className="text-xs">Build or run your project to see output</p>
                                        </div>
                                    )}
                                    <div ref={outputEndRef} />
                                </div>
                            </ScrollArea>
                        </TabsContent>

                        {/* To-Do Tab for AI Building */}
                        <TabsContent value="todo" className="flex-1 m-0 overflow-hidden">
                            <div className="h-full flex flex-col">
                                {/* AI Working Status */}
                                {aiWorkingPhase && (
                                    <div className="p-3 border-b border-white/10 bg-fuchsia-500/10">
                                        <div className="flex items-center gap-2">
                                            <Loader2 size={16} className="animate-spin text-fuchsia-400" />
                                            <span className="text-sm font-medium text-fuchsia-400">
                                                {aiWorkingPhase === 'planning' && 'Planner creating task list...'}
                                                {aiWorkingPhase === 'researching' && 'Researcher analyzing requirements...'}
                                                {aiWorkingPhase === 'developing' && 'Developer writing code...'}
                                                {aiWorkingPhase === 'testing' && 'Tester verifying code...'}
                                            </span>
                                        </div>
                                    </div>
                                )}
                                
                                {/* Header */}
                                <div className="p-3 border-b border-white/10 flex items-center justify-between">
                                    <h3 className="font-medium flex items-center gap-2">
                                        <ListTodo size={16} />
                                        Build Plan
                                    </h3>
                                    <div className="flex items-center gap-2">
                                        {todoItems.length > 0 && !planApproved && (
                                            <Button
                                                size="sm"
                                                onClick={() => {
                                                    setPlanApproved(true);
                                                    handleStartBuilding();
                                                }}
                                                className="bg-green-500 hover:bg-green-600 text-white"
                                            >
                                                <Check size={14} className="mr-1" />
                                                Approve & Build
                                            </Button>
                                        )}
                                        <Button
                                            size="sm"
                                            variant="ghost"
                                            onClick={() => setTodoItems(prev => [...prev, {
                                                id: Date.now(),
                                                task: 'New task',
                                                completed: false,
                                                agent: 'developer'
                                            }])}
                                        >
                                            <Plus size={14} className="mr-1" />
                                            Add Task
                                        </Button>
                                    </div>
                                </div>
                                
                                {/* To-Do List */}
                                <ScrollArea className="flex-1 p-3">
                                    {todoItems.length === 0 ? (
                                        <div className="text-center py-8 text-gray-500">
                                            <ListTodo className="w-8 h-8 mx-auto mb-2 opacity-50" />
                                            <p>No tasks yet</p>
                                            <p className="text-xs mt-1">Ask AI to build something and the plan will appear here</p>
                                        </div>
                                    ) : (
                                        <div className="space-y-2">
                                            {todoItems.map((item, index) => {
                                                const AgentIcon = AGENT_ICONS[item.agent] || Code;
                                                return (
                                                    <div 
                                                        key={item.id}
                                                        className={`p-3 rounded-lg border transition-all ${
                                                            item.completed 
                                                                ? 'bg-green-500/10 border-green-500/30' 
                                                                : 'bg-white/5 border-white/10 hover:border-white/20'
                                                        }`}
                                                    >
                                                        <div className="flex items-start gap-3">
                                                            <button
                                                                onClick={() => setTodoItems(prev => prev.map(t => 
                                                                    t.id === item.id ? { ...t, completed: !t.completed } : t
                                                                ))}
                                                                className={`mt-0.5 w-5 h-5 rounded border flex items-center justify-center transition-all ${
                                                                    item.completed 
                                                                        ? 'bg-green-500 border-green-500' 
                                                                        : 'border-white/30 hover:border-white/50'
                                                                }`}
                                                            >
                                                                {item.completed && <Check size={12} className="text-white" />}
                                                            </button>
                                                            
                                                            <div className="flex-1">
                                                                {editingTodo === item.id ? (
                                                                    <Textarea
                                                                        value={item.task}
                                                                        onChange={(e) => setTodoItems(prev => prev.map(t =>
                                                                            t.id === item.id ? { ...t, task: e.target.value } : t
                                                                        ))}
                                                                        onBlur={() => setEditingTodo(null)}
                                                                        autoFocus
                                                                        className="bg-white/10 border-white/20 text-sm min-h-[60px]"
                                                                    />
                                                                ) : (
                                                                    <p 
                                                                        className={`text-sm cursor-pointer ${item.completed ? 'line-through text-gray-500' : ''}`}
                                                                        onClick={() => setEditingTodo(item.id)}
                                                                    >
                                                                        {item.task}
                                                                    </p>
                                                                )}
                                                                
                                                                <div className="flex items-center gap-2 mt-2">
                                                                    <span className="flex items-center gap-1 px-2 py-0.5 rounded text-xs bg-white/10">
                                                                        <AgentIcon size={10} style={{ color: AGENT_COLORS[item.agent] }} />
                                                                        {item.agent}
                                                                    </span>
                                                                    <span className="text-xs text-gray-500">#{index + 1}</span>
                                                                </div>
                                                            </div>
                                                            
                                                            <div className="flex items-center gap-1">
                                                                <Button
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    className="h-6 w-6"
                                                                    onClick={() => setEditingTodo(item.id)}
                                                                >
                                                                    <Edit3 size={12} />
                                                                </Button>
                                                                <Button
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    className="h-6 w-6 text-red-400"
                                                                    onClick={() => setTodoItems(prev => prev.filter(t => t.id !== item.id))}
                                                                >
                                                                    <Trash2 size={12} />
                                                                </Button>
                                                            </div>
                                                        </div>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    )}
                                </ScrollArea>
                                
                                {/* Progress bar */}
                                {Array.isArray(todoItems) && todoItems.length > 0 && (
                                    <div className="p-3 border-t border-white/10">
                                        <div className="flex items-center justify-between text-xs text-gray-400 mb-1">
                                            <span>Progress</span>
                                            <span>{todoItems.filter(t => t.completed).length}/{todoItems.length} completed</span>
                                        </div>
                                        <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                                            <div 
                                                className="h-full bg-gradient-to-r from-fuchsia-500 to-cyan-500 transition-all"
                                                style={{ width: `${(todoItems.filter(t => t.completed).length / todoItems.length) * 100}%` }}
                                            />
                                        </div>
                                    </div>
                                )}
                            </div>
                        </TabsContent>

                    </Tabs>
                </div>
            </div>
            
            {/* Code Runner Modal */}
            <CodeRunner
                code={fileContent}
                language={getSyntaxLanguage(selectedFile?.path || '')}
                filename={selectedFile?.path}
                projectFiles={files}
                isOpen={showCodeRunner}
                onClose={() => setShowCodeRunner(false)}
            />
        </div>
    );
}
