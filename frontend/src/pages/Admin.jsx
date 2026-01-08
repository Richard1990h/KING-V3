import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../lib/auth';
import { adminAPI, siteSettingsAPI, profileAPI } from '../lib/api';
import { formatCredits, formatDate, formatRelativeTime } from '../lib/utils';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs';
import { Switch } from '../components/ui/switch';
import { ScrollArea } from '../components/ui/scroll-area';
import {
    Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '../components/ui/table';
import {
    Dialog, DialogContent, DialogHeader, DialogTitle
} from '../components/ui/dialog';
import {
    Select, SelectContent, SelectItem, SelectTrigger, SelectValue
} from '../components/ui/select';
import { Label } from '../components/ui/label';
import { Textarea } from '../components/ui/textarea';
import { 
    Zap, ArrowLeft, Users, FolderKanban, CreditCard, TrendingUp,
    Search, Edit2, Trash2, Shield, User, Activity, Database, 
    Server, Brain, Settings, Play, RefreshCw, AlertCircle, CheckCircle,
    XCircle, Clock, BookOpen, Package, Plus, DollarSign, Layers,
    Globe, Palette, Bot, Key, Eye, EyeOff, MapPin, MessageSquare,
    HardDrive, Wifi, Cpu, MemoryStick
} from 'lucide-react';

// Health Badge component
const HealthBadge = ({ status }) => {
    const colors = {
        healthy: 'bg-green-500/20 text-green-400',
        degraded: 'bg-yellow-500/20 text-yellow-400',
        unhealthy: 'bg-red-500/20 text-red-400',
        unknown: 'bg-gray-500/20 text-gray-400'
    };
    const icons = {
        healthy: CheckCircle,
        degraded: AlertCircle,
        unhealthy: XCircle,
        unknown: Clock
    };
    const Icon = icons[status] || icons.unknown;
    return (
        <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${colors[status] || colors.unknown}`}>
            <Icon size={12} />
            {status}
        </span>
    );
};

// Admins Online Panel Component
const AdminsOnlinePanel = ({ users, currentUserId }) => {
    const [adminVisibility, setAdminVisibility] = useState({});
    const [savingVisibility, setSavingVisibility] = useState(false);
    
    // Filter admins from the users list
    const admins = (users || []).filter(u => u.role === 'admin');
    
    // Calculate online status based on last_login_at (within last 15 minutes)
    const isOnline = (lastLogin) => {
        if (!lastLogin) return false;
        const lastActive = new Date(lastLogin);
        const now = new Date();
        const diffMinutes = (now - lastActive) / (1000 * 60);
        return diffMinutes < 15;
    };
    
    // Toggle visibility for current admin
    const toggleMyVisibility = async () => {
        setSavingVisibility(true);
        try {
            const newVisibility = !adminVisibility[currentUserId];
            // In a real implementation, this would call the backend
            // await adminAPI.updateAdminVisibility(currentUserId, !newVisibility);
            setAdminVisibility(prev => ({ ...prev, [currentUserId]: newVisibility }));
            // For now, just update local state
            alert(newVisibility ? 'You are now appearing as Online' : 'You are now appearing as Offline');
        } catch (error) {
            console.error('Failed to update visibility:', error);
        } finally {
            setSavingVisibility(false);
        }
    };
    
    // Get display status (respects visibility setting)
    const getDisplayStatus = (admin) => {
        const actuallyOnline = isOnline(admin.last_login_at || admin.analytics?.last_login_at);
        // If admin has set visibility to hidden, show offline even if online
        if (adminVisibility[admin.id] === false) {
            return { status: 'offline', label: 'Appear Offline', color: 'bg-gray-500' };
        }
        if (actuallyOnline) {
            return { status: 'online', label: 'Online', color: 'bg-green-500' };
        }
        return { status: 'offline', label: 'Offline', color: 'bg-gray-500' };
    };

    return (
        <div className="glass-card rounded-xl p-6">
            <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                <Shield className="w-5 h-5 text-yellow-400" />
                Admin Team Status
            </h3>
            <p className="text-sm text-gray-400 mb-4">
                View all admins and their online status. Admins can set themselves to appear offline for privacy.
            </p>
            
            {/* Current Admin's Visibility Toggle */}
            <div className="mb-4 p-4 rounded-lg bg-fuchsia-500/10 border border-fuchsia-500/20">
                <div className="flex items-center justify-between">
                    <div>
                        <Label className="text-white">Your Visibility</Label>
                        <p className="text-sm text-gray-400 mt-1">
                            {adminVisibility[currentUserId] === false 
                                ? "You're appearing as offline to other users"
                                : "You're visible as online when active"
                            }
                        </p>
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="text-sm text-gray-400">
                            {adminVisibility[currentUserId] === false ? 'Hidden' : 'Visible'}
                        </span>
                        <Switch
                            checked={adminVisibility[currentUserId] !== false}
                            onCheckedChange={toggleMyVisibility}
                            disabled={savingVisibility}
                        />
                    </div>
                </div>
            </div>
            
            {/* Admins List */}
            <div className="space-y-3">
                {admins.length === 0 ? (
                    <div className="text-center py-6 text-gray-500">
                        <Shield className="w-10 h-10 mx-auto mb-2 opacity-50" />
                        <p>No admins found</p>
                    </div>
                ) : (
                    admins.map((admin) => {
                        const displayStatus = getDisplayStatus(admin);
                        const isCurrentUser = admin.id === currentUserId;
                        
                        return (
                            <div 
                                key={admin.id} 
                                className={`flex items-center gap-4 p-3 rounded-lg ${
                                    isCurrentUser ? 'bg-fuchsia-500/5 border border-fuchsia-500/20' : 'bg-white/5'
                                }`}
                            >
                                {/* Avatar with status indicator */}
                                <div className="relative">
                                    <div className="w-12 h-12 rounded-full bg-gradient-to-br from-yellow-500 to-orange-500 flex items-center justify-center overflow-hidden">
                                        {admin.avatar_url ? (
                                            <img src={admin.avatar_url} alt="" className="w-full h-full object-cover" />
                                        ) : (
                                            <span className="text-white text-lg font-medium">
                                                {admin.name?.[0]?.toUpperCase() || 'A'}
                                            </span>
                                        )}
                                    </div>
                                    {/* Online indicator */}
                                    <div className={`absolute bottom-0 right-0 w-4 h-4 rounded-full border-2 border-[#0B0F19] ${displayStatus.color}`} />
                                </div>
                                
                                {/* Admin info */}
                                <div className="flex-1">
                                    <div className="flex items-center gap-2">
                                        <p className="font-medium text-white">
                                            {admin.name || admin.display_name || 'Admin'}
                                        </p>
                                        {isCurrentUser && (
                                            <span className="px-2 py-0.5 rounded text-xs bg-fuchsia-500/20 text-fuchsia-400">
                                                You
                                            </span>
                                        )}
                                    </div>
                                    <p className="text-sm text-gray-400">{admin.email}</p>
                                </div>
                                
                                {/* Status */}
                                <div className="text-right">
                                    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${
                                        displayStatus.status === 'online' 
                                            ? 'bg-green-500/20 text-green-400'
                                            : 'bg-gray-500/20 text-gray-400'
                                    }`}>
                                        <span className={`w-2 h-2 rounded-full ${displayStatus.color}`} />
                                        {displayStatus.label}
                                    </span>
                                    {admin.analytics?.last_login_at && (
                                        <p className="text-xs text-gray-500 mt-1">
                                            Last seen: {new Date(admin.analytics.last_login_at).toLocaleString()}
                                        </p>
                                    )}
                                </div>
                            </div>
                        );
                    })
                )}
            </div>
        </div>
    );
};

export default function Admin() {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [activeTab, setActiveTab] = useState('overview');
    const [stats, setStats] = useState(null);
    const [users, setUsers] = useState([]);
    const [runningJobs, setRunningJobs] = useState([]);
    const [agentActivity, setAgentActivity] = useState([]);
    const [knowledgeBase, setKnowledgeBase] = useState([]);
    const [systemHealth, setSystemHealth] = useState(null);
    const [settings, setSettings] = useState({});
    const [loading, setLoading] = useState(true);
    const [searchQuery, setSearchQuery] = useState('');
    const [editingUser, setEditingUser] = useState(null);
    const [editForm, setEditForm] = useState({ role: '', credits: '', credits_enabled: true, plan: '' });
    const [saving, setSaving] = useState(false);
    const [creditConfig, setCreditConfig] = useState({ chat: 0.5, project: 1.0 });
    
    // Plans state
    const [plans, setPlans] = useState([]);
    const [editingPlan, setEditingPlan] = useState(null);
    const [planForm, setPlanForm] = useState({
        id: '',
        name: '',
        description: '',
        priceMonthly: 0,
        priceYearly: 0,
        dailyCredits: 0,
        maxConcurrentWorkspaces: 1,
        allowsOwnApiKeys: false,
        features: '',
        sortOrder: 0
    });
    const [creatingPlan, setCreatingPlan] = useState(false);
    
    // Credit Packages state
    const [creditPackages, setCreditPackages] = useState([]);
    const [editingPackage, setEditingPackage] = useState(null);
    const [packageForm, setPackageForm] = useState({
        name: '',
        credits: 0,
        price: 0,
        is_active: true
    });
    const [creatingPackage, setCreatingPackage] = useState(false);
    
    // Free AI Providers state
    const [freeProviders, setFreeProviders] = useState([]);
    const [providerApiKey, setProviderApiKey] = useState({});
    const [showProviderKey, setShowProviderKey] = useState({});
    const [emergentEnabled, setEmergentEnabled] = useState(true);
    
    // User details state
    const [selectedUser, setSelectedUser] = useState(null);
    const [userDetails, setUserDetails] = useState(null);
    const [loadingDetails, setLoadingDetails] = useState(false);
    
    // Default settings state
    const [defaults, setDefaults] = useState({
        theme: {},
        language: 'en',
        free_credits: 100
    });
    
    // Site Settings state (announcements, etc.)
    const [siteSettings, setSiteSettings] = useState({
        announcement_enabled: false,
        announcement_message: '',
        announcement_type: 'info', // info, warning, success
        maintenance_mode: false,
        admins_auto_friend: true
    });
    const [savingSiteSettings, setSavingSiteSettings] = useState(false);

    useEffect(() => {
        if (user?.role !== 'admin') {
            navigate('/dashboard');
            return;
        }
        loadAllData();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [user, navigate]);

    const loadAllData = async () => {
        setLoading(true);
        try {
            const [statsRes, usersRes, jobsRes, activityRes, knowledgeRes, healthRes, settingsRes, plansRes, providersRes, aiSettingsRes, defaultsRes, packagesRes, siteSettingsRes] = await Promise.all([
                adminAPI.getStats(),
                adminAPI.getUsers(),
                adminAPI.getRunningJobs().catch(() => ({ data: [] })),
                adminAPI.getAgentActivity().catch(() => ({ data: [] })),
                adminAPI.getKnowledgeBase().catch(() => ({ data: [] })),
                adminAPI.getSystemHealth().catch(() => ({ data: {} })),
                adminAPI.getSettings().catch(() => ({ data: {} })),
                adminAPI.getAllPlans().catch(() => ({ data: [] })),
                adminAPI.getFreeAIProviders().catch(() => ({ data: [] })),
                adminAPI.getAISettings().catch(() => ({ data: { emergent_llm_enabled: true } })),
                adminAPI.getDefaults().catch(() => ({ data: {} })),
                adminAPI.getCreditPackages().catch(() => ({ data: [] })),
                siteSettingsAPI.get().catch(() => ({ data: {} }))
            ]);
            setStats(statsRes.data);
            setUsers(usersRes.data);
            setRunningJobs(jobsRes.data);
            setAgentActivity(activityRes.data);
            setKnowledgeBase(knowledgeRes.data);
            setSystemHealth(healthRes.data);
            setSettings(settingsRes.data);
            setPlans(plansRes.data || []);
            setFreeProviders(providersRes.data || []);
            setEmergentEnabled(aiSettingsRes.data?.emergent_llm_enabled ?? true);
            setDefaults(defaultsRes.data || { theme: {}, language: 'en', free_credits: 100 });
            setCreditPackages(packagesRes.data || []);
            setCreditConfig({
                chat: settingsRes.data?.credits_per_1k_tokens_chat || 0.5,
                project: settingsRes.data?.credits_per_1k_tokens_project || 1.0
            });
            // Load site settings
            if (siteSettingsRes.data) {
                setSiteSettings(prev => ({
                    ...prev,
                    ...siteSettingsRes.data
                }));
            }
        } catch (error) {
            console.error('Failed to load admin data:', error);
        } finally {
            setLoading(false);
        }
    };

    const filteredUsers = users.filter(u =>
        u.name?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        u.email?.toLowerCase().includes(searchQuery.toLowerCase())
    );

    const openEditDialog = (userData) => {
        setEditingUser(userData);
        setEditForm({
            role: userData.role,
            credits: userData.credits.toString(),
            credits_enabled: userData.credits_enabled !== false,
            plan: userData.plan || 'free'
        });
    };

    const saveUserChanges = async () => {
        if (!editingUser) return;
        setSaving(true);
        try {
            const updateData = {};
            if (editForm.role !== editingUser.role) updateData.role = editForm.role;
            if (parseFloat(editForm.credits) !== editingUser.credits) updateData.credits = parseFloat(editForm.credits);
            if (editForm.credits_enabled !== (editingUser.credits_enabled !== false)) updateData.credits_enabled = editForm.credits_enabled;
            if (editForm.plan !== (editingUser.plan || 'free')) updateData.plan = editForm.plan;
            
            if (Object.keys(updateData).length > 0) {
                await adminAPI.updateUser(editingUser.id, updateData);
                setUsers(users.map(u => u.id === editingUser.id ? { ...u, ...updateData } : u));
            }
            setEditingUser(null);
        } catch (error) {
            console.error('Failed to update user:', error);
        } finally {
            setSaving(false);
        }
    };

    const deleteUser = async (userId) => {
        if (!window.confirm('Are you sure you want to delete this user? This action cannot be undone.')) return;
        try {
            await adminAPI.deleteUser(userId);
            setUsers(users.filter(u => u.id !== userId));
        } catch (error) {
            console.error('Failed to delete user:', error);
        }
    };

    const updateCreditConfig = async () => {
        try {
            await adminAPI.updateCreditConfig(creditConfig.chat, creditConfig.project);
            alert('Credit configuration updated!');
        } catch (error) {
            console.error('Failed to update credit config:', error);
        }
    };

    const deleteKnowledgeEntry = async (entryId) => {
        try {
            await adminAPI.deleteKnowledgeEntry(entryId);
            setKnowledgeBase(knowledgeBase.filter(k => k.id !== entryId));
        } catch (error) {
            console.error('Failed to delete knowledge entry:', error);
        }
    };

    const invalidateExpiredKnowledge = async () => {
        try {
            const res = await adminAPI.invalidateExpiredKnowledge();
            alert(res.data.message);
            loadAllData();
        } catch (error) {
            console.error('Failed to invalidate knowledge:', error);
        }
    };

    // Plan management functions
    const openPlanDialog = (plan = null) => {
        if (plan) {
            setEditingPlan(plan);
            setPlanForm({
                plan_id: plan.plan_id,
                name: plan.name,
                price_monthly: plan.price_monthly,
                daily_credits: plan.daily_credits,
                max_projects: plan.max_projects,
                max_concurrent_workspaces: plan.max_concurrent_workspaces || 1,
                allows_own_api_keys: plan.allows_own_api_keys || false,
                features: (plan.features || []).join('\n')
            });
            setCreatingPlan(false);
        } else {
            setEditingPlan(null);
            setPlanForm({
                plan_id: '',
                name: '',
                price_monthly: 0,
                daily_credits: 0,
                max_projects: -1,
                max_concurrent_workspaces: 1,
                allows_own_api_keys: false,
                features: ''
            });
            setCreatingPlan(true);
        }
    };

    const savePlan = async () => {
        setSaving(true);
        try {
            const planData = {
                id: planForm.id,
                name: planForm.name,
                description: planForm.description || '',
                priceMonthly: parseFloat(planForm.priceMonthly) || 0,
                priceYearly: parseFloat(planForm.priceYearly) || 0,
                dailyCredits: parseInt(planForm.dailyCredits) || 0,
                maxConcurrentWorkspaces: parseInt(planForm.maxConcurrentWorkspaces) || 1,
                allowsOwnApiKeys: planForm.allowsOwnApiKeys || false,
                features: typeof planForm.features === 'string' 
                    ? planForm.features.split('\n').filter(f => f.trim())
                    : planForm.features || [],
                sortOrder: parseInt(planForm.sortOrder) || 0
            };

            if (creatingPlan) {
                await adminAPI.createSubscriptionPlan(planData);
            } else {
                await adminAPI.updateSubscriptionPlan(editingPlan.id, planData);
            }
            loadAllData();
            setEditingPlan(null);
            setCreatingPlan(false);
            setPlanForm({
                id: '', name: '', description: '', priceMonthly: 0, priceYearly: 0,
                dailyCredits: 0, maxConcurrentWorkspaces: 1, allowsOwnApiKeys: false, features: '', sortOrder: 0
            });
        } catch (error) {
            console.error('Failed to save plan:', error);
            alert(error.response?.data?.detail || 'Failed to save plan');
        } finally {
            setSaving(false);
        }
    };

    const deletePlan = async (planId) => {
        if (!window.confirm('Are you sure you want to delete this plan?')) return;
        try {
            await adminAPI.deleteSubscriptionPlan(planId);
            loadAllData();
        } catch (error) {
            console.error('Failed to delete plan:', error);
            alert(error.response?.data?.detail || 'Cannot delete default plans');
        }
    };

    const distributeDailyCredits = async () => {
        try {
            const res = await adminAPI.distributeDailyCredits();
            alert(res.data.message);
        } catch (error) {
            console.error('Failed to distribute credits:', error);
            alert('Failed to distribute credits');
        }
    };

    // Credit Package management functions
    const openPackageDialog = (pkg = null) => {
        if (pkg) {
            setEditingPackage(pkg);
            setPackageForm({
                name: pkg.name || '',
                credits: pkg.credits || 0,
                price: pkg.price || 0,
                is_active: pkg.is_active !== false
            });
            setCreatingPackage(false);
        } else {
            setEditingPackage(null);
            setPackageForm({
                name: '',
                credits: 0,
                price: 0,
                is_active: true
            });
            setCreatingPackage(true);
        }
    };

    const savePackage = async () => {
        setSaving(true);
        try {
            const packageData = {
                name: packageForm.name,
                credits: parseInt(packageForm.credits) || 0,
                price: parseFloat(packageForm.price) || 0,
                is_active: packageForm.is_active
            };

            if (creatingPackage) {
                await adminAPI.createCreditPackage(packageData);
            } else {
                await adminAPI.updateCreditPackage(editingPackage.id, packageData);
            }
            loadAllData();
            setEditingPackage(null);
            setCreatingPackage(false);
            setPackageForm({ name: '', credits: 0, price: 0, is_active: true });
        } catch (error) {
            console.error('Failed to save package:', error);
            alert(error.response?.data?.detail || 'Failed to save package');
        } finally {
            setSaving(false);
        }
    };

    const deletePackage = async (packageId) => {
        if (!window.confirm('Are you sure you want to delete this credit package?')) return;
        try {
            await adminAPI.deleteCreditPackage(packageId);
            loadAllData();
        } catch (error) {
            console.error('Failed to delete package:', error);
            alert(error.response?.data?.detail || 'Failed to delete package');
        }
    };

    // Free AI Provider functions
    const toggleProvider = async (providerId, enabled) => {
        try {
            const apiKey = providerApiKey[providerId] || null;
            await adminAPI.toggleFreeAIProvider(providerId, enabled, apiKey);
            setFreeProviders(prev => prev.map(p => 
                p.id === providerId ? { ...p, enabled } : p
            ));
        } catch (error) {
            console.error('Failed to toggle provider:', error);
            alert('Failed to toggle provider');
        }
    };

    const toggleEmergent = async (enabled) => {
        try {
            await adminAPI.toggleEmergentLLM(enabled);
            setEmergentEnabled(enabled);
        } catch (error) {
            console.error('Failed to toggle Emergent LLM:', error);
            alert('Failed to toggle Emergent LLM');
        }
    };

    // User details functions
    const viewUserDetails = async (userId) => {
        setLoadingDetails(true);
        setSelectedUser(userId);
        try {
            const res = await adminAPI.getUserDetails(userId);
            setUserDetails(res.data);
        } catch (error) {
            console.error('Failed to load user details:', error);
        } finally {
            setLoadingDetails(false);
        }
    };

    if (loading) {
        return (
            <div className="min-h-screen bg-[#030712] flex items-center justify-center">
                <div className="w-8 h-8 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-[#030712]">
            {/* Header */}
            <header className="sticky top-0 z-50 border-b border-white/10 bg-black/50 backdrop-blur-xl">
                <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <div className="flex items-center justify-between h-16">
                        <div className="flex items-center gap-4">
                            <Link to="/dashboard" className="flex items-center gap-2 text-gray-400 hover:text-white transition-colors" data-testid="back-to-dashboard">
                                <ArrowLeft size={20} />
                            </Link>
                            <div className="flex items-center gap-2">
                                <Shield className="w-5 h-5 text-yellow-400" />
                                <span className="text-xl font-bold font-outfit">Admin Panel</span>
                            </div>
                        </div>
                        
                        <div className="flex items-center gap-3">
                            <Button variant="outline" size="sm" onClick={loadAllData} className="border-white/10">
                                <RefreshCw size={16} className="mr-2" />
                                Refresh
                            </Button>
                            <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-yellow-500/10 border border-yellow-500/30">
                                <Shield size={16} className="text-yellow-400" />
                                <span className="text-yellow-300 font-medium">{user?.name}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </header>

            <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
                <Tabs value={activeTab} onValueChange={setActiveTab}>
                    <TabsList className="bg-white/5 border border-white/10 mb-6 flex-wrap">
                        <TabsTrigger value="overview" className="data-[state=active]:bg-fuchsia-500/20">
                            <TrendingUp size={16} className="mr-2" />
                            Overview
                        </TabsTrigger>
                        <TabsTrigger value="users" className="data-[state=active]:bg-fuchsia-500/20">
                            <Users size={16} className="mr-2" />
                            Users
                        </TabsTrigger>
                        <TabsTrigger value="plans" className="data-[state=active]:bg-fuchsia-500/20">
                            <Package size={16} className="mr-2" />
                            Plans
                        </TabsTrigger>
                        <TabsTrigger value="ai-providers" className="data-[state=active]:bg-fuchsia-500/20">
                            <Bot size={16} className="mr-2" />
                            AI Providers
                        </TabsTrigger>
                        <TabsTrigger value="jobs" className="data-[state=active]:bg-fuchsia-500/20">
                            <Play size={16} className="mr-2" />
                            Running Jobs
                        </TabsTrigger>
                        <TabsTrigger value="agents" className="data-[state=active]:bg-fuchsia-500/20">
                            <Brain size={16} className="mr-2" />
                            Agent Activity
                        </TabsTrigger>
                        <TabsTrigger value="knowledge" className="data-[state=active]:bg-fuchsia-500/20">
                            <BookOpen size={16} className="mr-2" />
                            Knowledge Base
                        </TabsTrigger>
                        <TabsTrigger value="health" className="data-[state=active]:bg-fuchsia-500/20">
                            <Server size={16} className="mr-2" />
                            System Health
                        </TabsTrigger>
                        <TabsTrigger value="site-settings" className="data-[state=active]:bg-fuchsia-500/20">
                            <MessageSquare size={16} className="mr-2" />
                            Site Settings
                        </TabsTrigger>
                        <TabsTrigger value="settings" className="data-[state=active]:bg-fuchsia-500/20">
                            <Settings size={16} className="mr-2" />
                            Settings
                        </TabsTrigger>
                    </TabsList>

                    {/* Overview Tab */}
                    <TabsContent value="overview">
                        <motion.div
                            initial={{ opacity: 0, y: 20 }}
                            animate={{ opacity: 1, y: 0 }}
                            className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8"
                        >
                            <div className="glass-card rounded-xl p-5">
                                <div className="flex items-center gap-3 mb-2">
                                    <div className="w-10 h-10 rounded-lg bg-fuchsia-500/20 flex items-center justify-center">
                                        <Users className="w-5 h-5 text-fuchsia-400" />
                                    </div>
                                    <span className="text-gray-400">Total Users</span>
                                </div>
                                <p className="text-3xl font-bold">{stats?.total_users || 0}</p>
                            </div>
                            
                            <div className="glass-card rounded-xl p-5">
                                <div className="flex items-center gap-3 mb-2">
                                    <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
                                        <FolderKanban className="w-5 h-5 text-cyan-400" />
                                    </div>
                                    <span className="text-gray-400">Total Projects</span>
                                </div>
                                <p className="text-3xl font-bold">{stats?.total_projects || 0}</p>
                            </div>
                            
                            <div className="glass-card rounded-xl p-5">
                                <div className="flex items-center gap-3 mb-2">
                                    <div className="w-10 h-10 rounded-lg bg-green-500/20 flex items-center justify-center">
                                        <CreditCard className="w-5 h-5 text-green-400" />
                                    </div>
                                    <span className="text-gray-400">Successful Payments</span>
                                </div>
                                <p className="text-3xl font-bold">{stats?.successful_payments || 0}</p>
                            </div>
                            
                            <div className="glass-card rounded-xl p-5">
                                <div className="flex items-center gap-3 mb-2">
                                    <div className="w-10 h-10 rounded-lg bg-purple-500/20 flex items-center justify-center">
                                        <BookOpen className="w-5 h-5 text-purple-400" />
                                    </div>
                                    <span className="text-gray-400">Knowledge Hits</span>
                                </div>
                                <p className="text-3xl font-bold">{stats?.knowledge_hits || 0}</p>
                            </div>
                        </motion.div>
                    </TabsContent>

                    {/* Plans Tab - Split into Subscription Plans and Credit Packages */}
                    <TabsContent value="plans">
                        <div className="space-y-6">
                            {/* Monthly Subscription Plans Section */}
                            <div className="glass-card rounded-xl overflow-hidden">
                                <div className="p-4 border-b border-white/10 flex items-center justify-between">
                                    <div>
                                        <h2 className="text-xl font-semibold">Monthly Subscription Plans</h2>
                                        <p className="text-sm text-gray-400 mt-1">Recurring subscription plans with daily credit allocation</p>
                                    </div>
                                    <div className="flex gap-2">
                                        <Button variant="outline" size="sm" onClick={distributeDailyCredits} className="border-white/10">
                                            <Zap size={16} className="mr-2" />
                                            Distribute Daily Credits
                                        </Button>
                                        <Button size="sm" onClick={() => openPlanDialog()} className="bg-fuchsia-500 hover:bg-fuchsia-600">
                                            <Plus size={16} className="mr-2" />
                                            New Plan
                                        </Button>
                                    </div>
                                </div>
                                
                                <ScrollArea className="h-[350px]">
                                    <Table>
                                        <TableHeader>
                                            <TableRow className="border-white/10">
                                                <TableHead>Plan</TableHead>
                                                <TableHead>Price/Month</TableHead>
                                                <TableHead>Daily Credits</TableHead>
                                                <TableHead>Workspaces</TableHead>
                                                <TableHead>API Keys</TableHead>
                                                <TableHead>Status</TableHead>
                                                <TableHead className="text-right">Actions</TableHead>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            {plans.map((plan) => (
                                                <TableRow key={plan.plan_id || plan.id} className="border-white/10">
                                                    <TableCell>
                                                        <div>
                                                            <p className="font-medium">{plan.name}</p>
                                                            <p className="text-xs text-gray-400">{plan.plan_id || plan.id}</p>
                                                        </div>
                                                    </TableCell>
                                                    <TableCell>
                                                        <div className="flex items-center gap-1">
                                                            <DollarSign size={14} className="text-green-400" />
                                                            <span>${(plan.priceMonthly || plan.price_monthly || 0).toFixed(2)}</span>
                                                        </div>
                                                    </TableCell>
                                                    <TableCell>
                                                        <div className="flex items-center gap-1">
                                                            <Zap size={14} className="text-fuchsia-400" />
                                                            <span>{plan.dailyCredits || plan.daily_credits || 0}</span>
                                                        </div>
                                                    </TableCell>
                                                    <TableCell>
                                                        <div className="flex items-center gap-1">
                                                            <Layers size={14} className="text-cyan-400" />
                                                            <span>{(plan.maxConcurrentWorkspaces || plan.max_concurrent_workspaces) === -1 ? 'Unlimited' : (plan.maxConcurrentWorkspaces || plan.max_concurrent_workspaces || 1)}</span>
                                                        </div>
                                                    </TableCell>
                                                    <TableCell>
                                                        <span className={`px-2 py-1 rounded text-xs ${
                                                            (plan.allowsOwnApiKeys || plan.allows_own_api_keys)
                                                                ? 'bg-green-500/20 text-green-400' 
                                                                : 'bg-gray-500/20 text-gray-400'
                                                        }`}>
                                                            {(plan.allowsOwnApiKeys || plan.allows_own_api_keys) ? 'Allowed' : 'No'}
                                                        </span>
                                                    </TableCell>
                                                    <TableCell>
                                                        <span className={`px-2 py-1 rounded text-xs ${
                                                            plan.is_active !== false 
                                                                ? 'bg-green-500/20 text-green-400' 
                                                                : 'bg-red-500/20 text-red-400'
                                                        }`}>
                                                            {plan.is_active !== false ? 'Active' : 'Inactive'}
                                                        </span>
                                                    </TableCell>
                                                    <TableCell className="text-right">
                                                        <div className="flex items-center justify-end gap-2">
                                                            <Button
                                                                variant="ghost"
                                                                size="icon"
                                                                onClick={() => openPlanDialog(plan)}
                                                            >
                                                                <Edit2 size={16} />
                                                            </Button>
                                                            {!['free', 'starter', 'pro', 'enterprise'].includes(plan.plan_id || plan.id) && (
                                                                <Button
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    onClick={() => deletePlan(plan.plan_id || plan.id)}
                                                                    className="text-red-400 hover:text-red-300"
                                                                >
                                                                    <Trash2 size={16} />
                                                                </Button>
                                                            )}
                                                        </div>
                                                    </TableCell>
                                                </TableRow>
                                            ))}
                                        </TableBody>
                                    </Table>
                                </ScrollArea>
                            </div>

                            {/* Credit Add-on Packages Section */}
                            <div className="glass-card rounded-xl overflow-hidden">
                                <div className="p-4 border-b border-white/10 flex items-center justify-between">
                                    <div>
                                        <h2 className="text-xl font-semibold">Credit Add-on Packages</h2>
                                        <p className="text-sm text-gray-400 mt-1">One-time credit purchases for users</p>
                                    </div>
                                    <Button size="sm" onClick={() => openPackageDialog()} className="bg-cyan-500 hover:bg-cyan-600">
                                        <Plus size={16} className="mr-2" />
                                        New Package
                                    </Button>
                                </div>
                                
                                <ScrollArea className="h-[300px]">
                                    <Table>
                                        <TableHeader>
                                            <TableRow className="border-white/10">
                                                <TableHead>Package Name</TableHead>
                                                <TableHead>Credits</TableHead>
                                                <TableHead>Price</TableHead>
                                                <TableHead>Price/Credit</TableHead>
                                                <TableHead>Status</TableHead>
                                                <TableHead className="text-right">Actions</TableHead>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            {creditPackages.length === 0 ? (
                                                <TableRow className="border-white/10">
                                                    <TableCell colSpan={6} className="text-center text-gray-500 py-8">
                                                        <Package className="w-8 h-8 mx-auto mb-2 opacity-50" />
                                                        <p>No credit packages yet</p>
                                                        <p className="text-xs mt-1">Click &quot;New Package&quot; to create one</p>
                                                    </TableCell>
                                                </TableRow>
                                            ) : (
                                                creditPackages.map((pkg) => (
                                                    <TableRow key={pkg.id} className="border-white/10">
                                                        <TableCell>
                                                            <div className="flex items-center gap-2">
                                                                <div className="w-8 h-8 rounded-lg bg-cyan-500/20 flex items-center justify-center">
                                                                    <Package size={14} className="text-cyan-400" />
                                                                </div>
                                                                <span className="font-medium">{pkg.name}</span>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className="flex items-center gap-1">
                                                                <Zap size={14} className="text-fuchsia-400" />
                                                                <span className="font-medium">{formatCredits(pkg.credits)}</span>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <div className="flex items-center gap-1">
                                                                <DollarSign size={14} className="text-green-400" />
                                                                <span>${(pkg.price || 0).toFixed(2)}</span>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell>
                                                            <span className="text-gray-400">
                                                                ${pkg.credits > 0 ? (pkg.price / pkg.credits).toFixed(4) : '0.00'}
                                                            </span>
                                                        </TableCell>
                                                        <TableCell>
                                                            <span className={`px-2 py-1 rounded text-xs ${
                                                                pkg.is_active !== false 
                                                                    ? 'bg-green-500/20 text-green-400' 
                                                                    : 'bg-red-500/20 text-red-400'
                                                            }`}>
                                                                {pkg.is_active !== false ? 'Active' : 'Inactive'}
                                                            </span>
                                                        </TableCell>
                                                        <TableCell className="text-right">
                                                            <div className="flex items-center justify-end gap-2">
                                                                <Button
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    onClick={() => openPackageDialog(pkg)}
                                                                >
                                                                    <Edit2 size={16} />
                                                                </Button>
                                                                <Button
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    onClick={() => deletePackage(pkg.id)}
                                                                    className="text-red-400 hover:text-red-300"
                                                                >
                                                                    <Trash2 size={16} />
                                                                </Button>
                                                            </div>
                                                        </TableCell>
                                                    </TableRow>
                                                ))
                                            )}
                                        </TableBody>
                                    </Table>
                                </ScrollArea>
                            </div>
                        </div>
                    </TabsContent>

                    {/* AI Providers Tab */}
                    <TabsContent value="ai-providers">
                        <div className="space-y-6">
                            {/* Emergent LLM */}
                            <div className="glass-card rounded-xl p-6">
                                <div className="flex items-center justify-between mb-4">
                                    <div className="flex items-center gap-3">
                                        <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-fuchsia-500 to-cyan-500 flex items-center justify-center">
                                            <Zap size={20} className="text-white" />
                                        </div>
                                        <div>
                                            <h3 className="font-semibold">Emergent LLM (Default)</h3>
                                            <p className="text-sm text-gray-400">Universal key for OpenAI, Claude, Gemini</p>
                                        </div>
                                    </div>
                                    <Switch
                                        checked={emergentEnabled}
                                        onCheckedChange={toggleEmergent}
                                    />
                                </div>
                                <p className="text-sm text-gray-500">
                                    When enabled, all users without their own API keys will use Emergent LLM.
                                    Supports GPT-4o, Claude Sonnet 4, Gemini 2.5 Flash.
                                </p>
                            </div>
                            
                            {/* Free AI Providers */}
                            <div className="glass-card rounded-xl overflow-hidden">
                                <div className="p-4 border-b border-white/10">
                                    <h2 className="text-xl font-semibold">Free AI Providers</h2>
                                    <p className="text-sm text-gray-400 mt-1">
                                        Enable free-tier AI providers for users. Some require API keys (free to obtain).
                                    </p>
                                </div>
                                
                                <div className="divide-y divide-white/10">
                                    {freeProviders.map((provider) => (
                                        <div key={provider.id} className="p-4">
                                            <div className="flex items-center justify-between mb-3">
                                                <div className="flex items-center gap-3">
                                                    <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${
                                                        provider.enabled ? 'bg-green-500/20' : 'bg-white/5'
                                                    }`}>
                                                        <Bot size={20} className={provider.enabled ? 'text-green-400' : 'text-gray-400'} />
                                                    </div>
                                                    <div>
                                                        <h4 className="font-medium">{provider.name}</h4>
                                                        <p className="text-sm text-gray-400">{provider.description}</p>
                                                    </div>
                                                </div>
                                                <Switch
                                                    checked={provider.enabled}
                                                    onCheckedChange={(enabled) => toggleProvider(provider.id, enabled)}
                                                />
                                            </div>
                                            
                                            {provider.requires_key && (
                                                <div className="mt-3 flex gap-2">
                                                    <div className="relative flex-1">
                                                        <Input
                                                            type="text"
                                                            placeholder={provider.has_key ? provider.api_key || '••••••••••••' : 'Enter API Key'}
                                                            value={providerApiKey[provider.id] || ''}
                                                            onChange={(e) => setProviderApiKey(prev => ({
                                                                ...prev,
                                                                [provider.id]: e.target.value
                                                            }))}
                                                            className="bg-white/5 border-white/10 font-mono text-sm"
                                                        />
                                                    </div>
                                                    <Button
                                                        variant="outline"
                                                        size="sm"
                                                        onClick={() => toggleProvider(provider.id, provider.enabled)}
                                                        disabled={!providerApiKey[provider.id]}
                                                        className="border-white/10"
                                                    >
                                                        Save Key
                                                    </Button>
                                                </div>
                                            )}
                                            
                                            <div className="mt-2 flex flex-wrap gap-1">
                                                {provider.models?.slice(0, 3).map((model) => (
                                                    <span key={model} className="px-2 py-0.5 text-xs rounded bg-white/5 text-gray-400">
                                                        {model}
                                                    </span>
                                                ))}
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </TabsContent>

                    {/* Users Tab */}
                    <TabsContent value="users">
                        <div className="glass-card rounded-xl overflow-hidden">
                            <div className="p-4 border-b border-white/10 flex items-center justify-between">
                                <h2 className="text-xl font-semibold">Users ({filteredUsers.length})</h2>
                                <div className="relative w-64">
                                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                                    <Input
                                        type="text"
                                        placeholder="Search users..."
                                        value={searchQuery}
                                        onChange={(e) => setSearchQuery(e.target.value)}
                                        className="pl-9 bg-white/5 border-white/10"
                                    />
                                </div>
                            </div>
                            
                            <ScrollArea className="h-[500px]">
                                <Table>
                                    <TableHeader>
                                        <TableRow className="border-white/10">
                                            <TableHead>User</TableHead>
                                            <TableHead>Role/Plan</TableHead>
                                            <TableHead>Credits</TableHead>
                                            <TableHead>Stats</TableHead>
                                            <TableHead>IP Info</TableHead>
                                            <TableHead>Last Active</TableHead>
                                            <TableHead className="text-right">Actions</TableHead>
                                        </TableRow>
                                    </TableHeader>
                                    <TableBody>
                                        {filteredUsers.map((userData) => (
                                            <TableRow key={userData.id} className="border-white/10">
                                                <TableCell>
                                                    <div className="flex items-center gap-3">
                                                        <div className="w-8 h-8 rounded-full bg-gradient-to-br from-fuchsia-500 to-cyan-500 flex items-center justify-center overflow-hidden">
                                                            {userData.avatar_url ? (
                                                                <img src={userData.avatar_url} alt="" className="w-full h-full object-cover" />
                                                            ) : (
                                                                <span className="text-white text-sm font-medium">
                                                                    {userData.name?.[0]?.toUpperCase() || 'U'}
                                                                </span>
                                                            )}
                                                        </div>
                                                        <div>
                                                            <p className="font-medium">{userData.name}</p>
                                                            <p className="text-xs text-gray-400">{userData.email}</p>
                                                        </div>
                                                    </div>
                                                </TableCell>
                                                <TableCell>
                                                    <div className="space-y-1">
                                                        <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${
                                                            userData.role === 'admin' 
                                                                ? 'bg-yellow-500/20 text-yellow-400' 
                                                                : 'bg-gray-500/20 text-gray-400'
                                                        }`}>
                                                            {userData.role === 'admin' ? <Shield size={10} /> : <User size={10} />}
                                                            {userData.role}
                                                        </span>
                                                        <div>
                                                            <span className={`px-2 py-0.5 rounded text-xs ${
                                                                userData.plan === 'enterprise' ? 'bg-purple-500/20 text-purple-400' :
                                                                userData.plan === 'pro' ? 'bg-fuchsia-500/20 text-fuchsia-400' :
                                                                userData.plan === 'starter' ? 'bg-cyan-500/20 text-cyan-400' :
                                                                'bg-gray-500/20 text-gray-400'
                                                            }`}>
                                                                {userData.plan || 'free'}
                                                            </span>
                                                        </div>
                                                    </div>
                                                </TableCell>
                                                <TableCell>
                                                    <div className="space-y-1">
                                                        <div className="flex items-center gap-1">
                                                            <Zap size={12} className="text-fuchsia-400" />
                                                            <span className="text-sm">{formatCredits(userData.credits)}</span>
                                                        </div>
                                                        {userData.analytics?.credits_used > 0 && (
                                                            <p className="text-xs text-gray-500">
                                                                Used: {formatCredits(userData.analytics.credits_used)}
                                                            </p>
                                                        )}
                                                    </div>
                                                </TableCell>
                                                <TableCell>
                                                    <div className="space-y-0.5 text-xs">
                                                        <div className="flex items-center gap-1 text-gray-400">
                                                            <MessageSquare size={10} />
                                                            <span>{userData.analytics?.total_conversations || 0} chats</span>
                                                        </div>
                                                        <div className="flex items-center gap-1 text-gray-400">
                                                            <FolderKanban size={10} />
                                                            <span>{userData.analytics?.total_projects || 0} projects</span>
                                                        </div>
                                                        <div className="flex items-center gap-1 text-gray-400">
                                                            <Play size={10} />
                                                            <span>{userData.analytics?.completed_jobs || 0}/{userData.analytics?.total_jobs || 0} jobs</span>
                                                        </div>
                                                    </div>
                                                </TableCell>
                                                <TableCell>
                                                    <div className="space-y-0.5 text-xs">
                                                        <div className="flex items-center gap-1 text-gray-400" title="Registration IP">
                                                            <MapPin size={10} />
                                                            <span>{userData.analytics?.registration_ip || userData.registration_ip || 'Unknown'}</span>
                                                        </div>
                                                        {userData.analytics?.last_login_ip && userData.analytics.last_login_ip !== userData.analytics?.registration_ip && (
                                                            <div className="flex items-center gap-1 text-gray-500" title="Last Login IP">
                                                                <span>Last: {userData.analytics.last_login_ip}</span>
                                                            </div>
                                                        )}
                                                    </div>
                                                </TableCell>
                                                <TableCell className="text-xs text-gray-400">
                                                    <div>
                                                        {userData.analytics?.last_login_at ? (
                                                            <span>{formatRelativeTime(userData.analytics.last_login_at)}</span>
                                                        ) : userData.last_login_at ? (
                                                            <span>{formatRelativeTime(userData.last_login_at)}</span>
                                                        ) : (
                                                            <span className="text-gray-500">Never</span>
                                                        )}
                                                    </div>
                                                    <div className="text-gray-500">
                                                        Joined {formatDate(userData.created_at)}
                                                    </div>
                                                </TableCell>
                                                <TableCell className="text-right">
                                                    <div className="flex items-center justify-end gap-1">
                                                        <Button
                                                            variant="ghost"
                                                            size="icon"
                                                            onClick={() => viewUserDetails(userData.id)}
                                                            title="View Details"
                                                            className="h-8 w-8"
                                                        >
                                                            <Eye size={14} />
                                                        </Button>
                                                        <Button
                                                            variant="ghost"
                                                            size="icon"
                                                            onClick={() => openEditDialog(userData)}
                                                            className="h-8 w-8"
                                                        >
                                                            <Edit2 size={14} />
                                                        </Button>
                                                        {userData.id !== user?.id && (
                                                            <Button
                                                                variant="ghost"
                                                                size="icon"
                                                                onClick={() => deleteUser(userData.id)}
                                                                className="text-red-400 hover:text-red-300 h-8 w-8"
                                                            >
                                                                <Trash2 size={14} />
                                                            </Button>
                                                        )}
                                                    </div>
                                                </TableCell>
                                            </TableRow>
                                        ))}
                                    </TableBody>
                                </Table>
                            </ScrollArea>
                        </div>
                    </TabsContent>

                    {/* Running Jobs Tab */}
                    <TabsContent value="jobs">
                        <div className="glass-card rounded-xl overflow-hidden">
                            <div className="p-4 border-b border-white/10">
                                <h2 className="text-xl font-semibold">Running Jobs ({runningJobs.length})</h2>
                            </div>
                            
                            {runningJobs.length === 0 ? (
                                <div className="p-8 text-center text-gray-400">
                                    <Play className="w-12 h-12 mx-auto mb-3 opacity-50" />
                                    <p>No jobs currently running</p>
                                </div>
                            ) : (
                                <Table>
                                    <TableHeader>
                                        <TableRow className="border-white/10">
                                            <TableHead>Job Type</TableHead>
                                            <TableHead>User</TableHead>
                                            <TableHead>Project</TableHead>
                                            <TableHead>Status</TableHead>
                                            <TableHead>Started</TableHead>
                                        </TableRow>
                                    </TableHeader>
                                    <TableBody>
                                        {runningJobs.map((job) => (
                                            <TableRow key={job.id} className="border-white/10">
                                                <TableCell className="font-medium">{job.job_type}</TableCell>
                                                <TableCell>{job.user_name || job.user_email}</TableCell>
                                                <TableCell>{job.project_name || '-'}</TableCell>
                                                <TableCell>
                                                    <span className={`px-2 py-1 rounded text-xs ${
                                                        job.status === 'running' ? 'bg-green-500/20 text-green-400' : 'bg-yellow-500/20 text-yellow-400'
                                                    }`}>
                                                        {job.status}
                                                    </span>
                                                </TableCell>
                                                <TableCell className="text-gray-400">
                                                    {formatRelativeTime(job.started_at || job.created_at)}
                                                </TableCell>
                                            </TableRow>
                                        ))}
                                    </TableBody>
                                </Table>
                            )}
                        </div>
                    </TabsContent>

                    {/* Agent Activity Tab */}
                    <TabsContent value="agents">
                        <div className="glass-card rounded-xl overflow-hidden">
                            <div className="p-4 border-b border-white/10">
                                <h2 className="text-xl font-semibold">Agent Activity</h2>
                            </div>
                            
                            <ScrollArea className="h-[500px]">
                                {agentActivity.length === 0 ? (
                                    <div className="p-8 text-center text-gray-400">
                                        <Brain className="w-12 h-12 mx-auto mb-3 opacity-50" />
                                        <p>No agent activity recorded</p>
                                    </div>
                                ) : (
                                    <Table>
                                        <TableHeader>
                                            <TableRow className="border-white/10">
                                                <TableHead>Agent</TableHead>
                                                <TableHead>Action</TableHead>
                                                <TableHead>Status</TableHead>
                                                <TableHead>Duration</TableHead>
                                                <TableHead>Tokens</TableHead>
                                                <TableHead>Time</TableHead>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            {agentActivity.map((activity) => (
                                                <TableRow key={activity.id} className="border-white/10">
                                                    <TableCell className="font-medium">{activity.agentId || activity.agent_id}</TableCell>
                                                    <TableCell>{activity.action}</TableCell>
                                                    <TableCell>
                                                        <span className={`px-2 py-1 rounded text-xs ${
                                                            (activity.success ?? activity.status === 'completed') ? 'bg-green-500/20 text-green-400' :
                                                            (activity.error || activity.status === 'failed') ? 'bg-red-500/20 text-red-400' :
                                                            'bg-yellow-500/20 text-yellow-400'
                                                        }`}>
                                                            {activity.success ? 'completed' : activity.error ? 'failed' : (activity.status || 'pending')}
                                                        </span>
                                                    </TableCell>
                                                    <TableCell>{activity.durationMs || activity.duration_ms ? `${activity.durationMs || activity.duration_ms}ms` : '-'}</TableCell>
                                                    <TableCell>{(activity.tokensUsed || activity.tokens_used || activity.tokens_input || 0) + (activity.tokens_output || 0)}</TableCell>
                                                    <TableCell className="text-gray-400">
                                                        {formatRelativeTime(activity.timestamp || activity.created_at)}
                                                    </TableCell>
                                                </TableRow>
                                            ))}
                                        </TableBody>
                                    </Table>
                                )}
                            </ScrollArea>
                        </div>
                    </TabsContent>

                    {/* Knowledge Base Tab */}
                    <TabsContent value="knowledge">
                        <div className="glass-card rounded-xl overflow-hidden">
                            <div className="p-4 border-b border-white/10 flex items-center justify-between">
                                <h2 className="text-xl font-semibold">Knowledge Base ({knowledgeBase.length} entries)</h2>
                                <Button variant="outline" size="sm" onClick={invalidateExpiredKnowledge} className="border-white/10">
                                    Invalidate Expired
                                </Button>
                            </div>
                            
                            <ScrollArea className="h-[500px]">
                                {knowledgeBase.length === 0 ? (
                                    <div className="p-8 text-center text-gray-400">
                                        <BookOpen className="w-12 h-12 mx-auto mb-3 opacity-50" />
                                        <p>Knowledge base is empty</p>
                                    </div>
                                ) : (
                                    <div className="divide-y divide-white/10">
                                        {knowledgeBase.map((entry) => (
                                            <div key={entry.id} className="p-4">
                                                <div className="flex items-start justify-between gap-4">
                                                    <div className="flex-1 min-w-0">
                                                        <p className="font-medium text-sm mb-1 truncate">{entry.question || entry.question_text}</p>
                                                        <p className="text-xs text-gray-400 line-clamp-2">{entry.answer || entry.answer_text}</p>
                                                        <div className="flex items-center gap-4 mt-2 text-xs text-gray-500">
                                                            <span>Hits: {entry.hitCount || entry.hit_count || entry.usage_count || 0}</span>
                                                            <span>Provider: {entry.provider || 'unknown'}</span>
                                                            <span className={(entry.isValid ?? entry.is_valid) ? 'text-green-400' : 'text-red-400'}>
                                                                {(entry.isValid ?? entry.is_valid) ? 'Valid' : 'Invalid'}
                                                            </span>
                                                        </div>
                                                    </div>
                                                    <Button
                                                        variant="ghost"
                                                        size="icon"
                                                        onClick={() => deleteKnowledgeEntry(entry.id)}
                                                        className="text-red-400"
                                                    >
                                                        <Trash2 size={16} />
                                                    </Button>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </ScrollArea>
                        </div>
                    </TabsContent>

                    {/* System Health Tab */}
                    <TabsContent value="health">
                        <div className="space-y-6">
                            {/* Quick Status Overview */}
                            {systemHealth && (
                                <>
                                    {/* System Info */}
                                    <div className="glass-card rounded-xl p-6">
                                        <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                            <Server className="w-5 h-5 text-fuchsia-400" />
                                            System Information
                                        </h3>
                                        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">Operating System</p>
                                                <p className="font-medium">{systemHealth.system?.os || 'N/A'}</p>
                                            </div>
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">Machine Name</p>
                                                <p className="font-medium">{systemHealth.system?.machineName || 'N/A'}</p>
                                            </div>
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">CPU Cores</p>
                                                <p className="font-medium">{systemHealth.system?.processors || 'N/A'} cores</p>
                                            </div>
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">.NET Version</p>
                                                <p className="font-medium">{systemHealth.system?.dotnetVersion || 'N/A'}</p>
                                            </div>
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">Architecture</p>
                                                <p className="font-medium">{systemHealth.system?.is64Bit ? '64-bit' : '32-bit'}</p>
                                            </div>
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">User</p>
                                                <p className="font-medium">{systemHealth.system?.userName || 'N/A'}</p>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Memory Info */}
                                    <div className="glass-card rounded-xl p-6">
                                        <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                            <Activity className="w-5 h-5 text-cyan-400" />
                                            Memory Usage
                                        </h3>
                                        <div className="grid md:grid-cols-3 gap-4">
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">Process Memory</p>
                                                <p className="font-medium text-lg">{systemHealth.memory?.processMemory || 'N/A'}</p>
                                            </div>
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">GC Memory</p>
                                                <p className="font-medium text-lg">{systemHealth.memory?.gcMemory || 'N/A'}</p>
                                            </div>
                                            <div className="bg-white/5 rounded-lg p-4">
                                                <p className="text-sm text-gray-400">GC Collections</p>
                                                <p className="font-medium text-sm">
                                                    Gen0: {systemHealth.memory?.gcCollections?.gen0 || 0} | 
                                                    Gen1: {systemHealth.memory?.gcCollections?.gen1 || 0} | 
                                                    Gen2: {systemHealth.memory?.gcCollections?.gen2 || 0}
                                                </p>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Storage/Drives */}
                                    <div className="glass-card rounded-xl p-6">
                                        <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                            <HardDrive className="w-5 h-5 text-green-400" />
                                            Storage Drives
                                        </h3>
                                        <div className="space-y-4">
                                            {systemHealth.storage?.drives?.map((drive, idx) => (
                                                <div key={idx} className="bg-white/5 rounded-lg p-4">
                                                    <div className="flex items-center justify-between mb-2">
                                                        <span className="font-medium">{drive.name} ({drive.type})</span>
                                                        <span className="text-sm text-gray-400">{drive.format}</span>
                                                    </div>
                                                    <div className="w-full bg-white/10 rounded-full h-2 mb-2">
                                                        <div 
                                                            className="bg-gradient-to-r from-fuchsia-500 to-cyan-500 h-2 rounded-full"
                                                            style={{ width: drive.percentUsed }}
                                                        />
                                                    </div>
                                                    <div className="flex justify-between text-xs text-gray-400">
                                                        <span>Used: {drive.usedSpace}</span>
                                                        <span>Free: {drive.freeSpace}</span>
                                                        <span>Total: {drive.totalSize}</span>
                                                    </div>
                                                </div>
                                            )) || <p className="text-gray-400">No drive information available</p>}
                                        </div>
                                    </div>

                                    {/* Service Status */}
                                    <div className="grid md:grid-cols-2 gap-4">
                                        {/* Database Status */}
                                        <div className="glass-card rounded-xl p-6">
                                            <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                                <Database className="w-5 h-5 text-yellow-400" />
                                                Database
                                            </h3>
                                            <div className="flex items-center gap-3">
                                                <div className={`w-3 h-3 rounded-full ${
                                                    systemHealth.database?.status === 'healthy' ? 'bg-green-500' : 'bg-red-500'
                                                }`} />
                                                <span>{systemHealth.database?.connection || 'Unknown'}</span>
                                            </div>
                                            <p className="text-sm text-gray-400 mt-2">{systemHealth.database?.type || 'MySQL'}</p>
                                        </div>

                                        {/* API Status */}
                                        <div className="glass-card rounded-xl p-6">
                                            <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                                <Wifi className="w-5 h-5 text-blue-400" />
                                                API Server
                                            </h3>
                                            <div className="flex items-center gap-3">
                                                <div className={`w-3 h-3 rounded-full ${
                                                    systemHealth.api?.status === 'healthy' ? 'bg-green-500' : 'bg-red-500'
                                                }`} />
                                                <span>Running on port {systemHealth.api?.port || 8001}</span>
                                            </div>
                                            <p className="text-sm text-gray-400 mt-2">Uptime: {systemHealth.api?.uptime || 'N/A'}</p>
                                        </div>
                                    </div>

                                    {/* AI Services Status */}
                                    <div className="glass-card rounded-xl p-6">
                                        <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                            <Bot className="w-5 h-5 text-purple-400" />
                                            AI Services
                                        </h3>
                                        {systemHealth.ai_services && (
                                            <div className="space-y-3">
                                                <div className="flex items-center justify-between bg-white/5 rounded-lg p-3">
                                                    <span>Emergent LLM</span>
                                                    <span className={systemHealth.ai_services.emergent_llm?.enabled ? 'text-green-400' : 'text-gray-400'}>
                                                        {systemHealth.ai_services.emergent_llm?.enabled ? 'Enabled' : 'Disabled'}
                                                    </span>
                                                </div>
                                                <div className="flex items-center justify-between bg-white/5 rounded-lg p-3">
                                                    <span>Free Providers Active</span>
                                                    <span className="text-cyan-400">
                                                        {systemHealth.ai_services.free_providers?.length || 0}
                                                    </span>
                                                </div>
                                                <div className="flex items-center justify-between bg-white/5 rounded-lg p-3">
                                                    <span>Local LLM (Ollama)</span>
                                                    <span className="text-gray-400">
                                                        {systemHealth.ai_services.local_llm?.url || 'Not configured'}
                                                    </span>
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                </>
                            )}
                            
                            {!systemHealth && (
                                <div className="glass-card rounded-xl p-8 text-center">
                                    <RefreshCw className="w-8 h-8 text-gray-500 mx-auto mb-3 animate-spin" />
                                    <p className="text-gray-400">Loading system health data...</p>
                                </div>
                            )}
                        </div>
                    </TabsContent>

                    {/* Site Settings Tab - Announcements & Admin Features */}
                    <TabsContent value="site-settings">
                        <div className="space-y-6">
                            {/* Announcement Banner */}
                            <div className="glass-card rounded-xl p-6">
                                <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                    <MessageSquare className="w-5 h-5 text-cyan-400" />
                                    Login Page Announcement
                                </h3>
                                <p className="text-sm text-gray-400 mb-4">
                                    Display a message to all users on the login page (e.g., maintenance notices, early access warnings)
                                </p>
                                
                                <div className="space-y-4">
                                    <div className="flex items-center justify-between">
                                        <Label>Enable Announcement</Label>
                                        <Switch
                                            checked={siteSettings.announcement_enabled}
                                            onCheckedChange={(checked) => setSiteSettings(prev => ({ ...prev, announcement_enabled: checked }))}
                                        />
                                    </div>
                                    
                                    <div className="space-y-2">
                                        <Label>Message Type</Label>
                                        <Select
                                            value={siteSettings.announcement_type}
                                            onValueChange={(value) => setSiteSettings(prev => ({ ...prev, announcement_type: value }))}
                                        >
                                            <SelectTrigger className="bg-white/5 border-white/10">
                                                <SelectValue placeholder="Select type" />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="info">ℹ️ Info (Blue)</SelectItem>
                                                <SelectItem value="warning">⚠️ Warning (Yellow)</SelectItem>
                                                <SelectItem value="success">✅ Success (Green)</SelectItem>
                                                <SelectItem value="error">❌ Error (Red)</SelectItem>
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    
                                    <div className="space-y-2">
                                        <Label>Announcement Message</Label>
                                        <Textarea
                                            value={siteSettings.announcement_message}
                                            onChange={(e) => setSiteSettings(prev => ({ ...prev, announcement_message: e.target.value }))}
                                            placeholder="e.g., 🚧 This server is in early access development. Everything is subject to change. Expect bugs - we're working through them!"
                                            className="bg-white/5 border-white/10 min-h-[100px]"
                                        />
                                    </div>
                                    
                                    {/* Preview */}
                                    {siteSettings.announcement_message && (
                                        <div className="space-y-2">
                                            <Label className="text-gray-400">Preview:</Label>
                                            <div className={`px-4 py-3 rounded-lg border ${
                                                siteSettings.announcement_type === 'info' ? 'bg-blue-500/10 border-blue-500/30 text-blue-300' :
                                                siteSettings.announcement_type === 'warning' ? 'bg-amber-500/10 border-amber-500/30 text-amber-300' :
                                                siteSettings.announcement_type === 'success' ? 'bg-green-500/10 border-green-500/30 text-green-300' :
                                                'bg-red-500/10 border-red-500/30 text-red-300'
                                            }`}>
                                                <p className="text-sm">{siteSettings.announcement_message}</p>
                                            </div>
                                        </div>
                                    )}
                                </div>
                            </div>

                            {/* Admin Auto-Friend Setting */}
                            <div className="glass-card rounded-xl p-6">
                                <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                    <Users className="w-5 h-5 text-fuchsia-400" />
                                    Admin Social Settings
                                </h3>
                                <p className="text-sm text-gray-400 mb-4">
                                    Configure how admins interact with users in the friends system
                                </p>
                                
                                <div className="space-y-4">
                                    <div className="flex items-center justify-between p-4 rounded-lg bg-white/5">
                                        <div>
                                            <Label className="text-white">Auto-Friend Admins</Label>
                                            <p className="text-sm text-gray-400 mt-1">
                                                Automatically add all admins as friends to every user. This allows admins to send direct messages and provide support.
                                            </p>
                                        </div>
                                        <Switch
                                            checked={siteSettings.admins_auto_friend}
                                            onCheckedChange={(checked) => setSiteSettings(prev => ({ ...prev, admins_auto_friend: checked }))}
                                        />
                                    </div>
                                </div>
                            </div>

                            {/* Admins Online Status Panel */}
                            <AdminsOnlinePanel users={users} currentUserId={user?.id} />

                            {/* Maintenance Mode */}
                            <div className="glass-card rounded-xl p-6">
                                <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                    <AlertCircle className="w-5 h-5 text-amber-400" />
                                    Maintenance Mode
                                </h3>
                                <div className="flex items-center justify-between p-4 rounded-lg bg-white/5">
                                    <div>
                                        <Label className="text-white">Enable Maintenance Mode</Label>
                                        <p className="text-sm text-gray-400 mt-1">
                                            When enabled, only admins can log in. Regular users will see a maintenance message.
                                        </p>
                                    </div>
                                    <Switch
                                        checked={siteSettings.maintenance_mode}
                                        onCheckedChange={(checked) => setSiteSettings(prev => ({ ...prev, maintenance_mode: checked }))}
                                    />
                                </div>
                            </div>

                            {/* Save Button */}
                            <div className="flex justify-end">
                                <Button
                                    onClick={async () => {
                                        setSavingSiteSettings(true);
                                        try {
                                            await siteSettingsAPI.update(siteSettings);
                                            alert('Site settings saved successfully!');
                                        } catch (error) {
                                            console.error('Failed to save site settings:', error);
                                            alert('Failed to save settings. Please try again.');
                                        } finally {
                                            setSavingSiteSettings(false);
                                        }
                                    }}
                                    disabled={savingSiteSettings}
                                    className="bg-gradient-to-r from-fuchsia-500 to-cyan-500 hover:from-fuchsia-600 hover:to-cyan-600"
                                >
                                    {savingSiteSettings ? (
                                        <RefreshCw size={16} className="mr-2 animate-spin" />
                                    ) : (
                                        <CheckCircle size={16} className="mr-2" />
                                    )}
                                    Save Site Settings
                                </Button>
                            </div>
                        </div>
                    </TabsContent>

                    {/* Settings Tab */}
                    <TabsContent value="settings">
                        <div className="space-y-6">
                            {/* Credit Configuration */}
                            <div className="glass-card rounded-xl p-6">
                                <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                                    <Zap className="w-5 h-5 text-fuchsia-400" />
                                    Credit Configuration
                                </h3>
                                <div className="grid md:grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label>Credits per 1K tokens (General Chat)</Label>
                                        <Input
                                            type="number"
                                            step="0.1"
                                            value={creditConfig.chat}
                                            onChange={(e) => setCreditConfig({ ...creditConfig, chat: parseFloat(e.target.value) })}
                                            className="bg-white/5 border-white/10"
                                        />
                                        <p className="text-xs text-gray-400">Applied to general assistant chats</p>
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Credits per 1K tokens (Project Generation)</Label>
                                        <Input
                                            type="number"
                                            step="0.1"
                                            value={creditConfig.project}
                                            onChange={(e) => setCreditConfig({ ...creditConfig, project: parseFloat(e.target.value) })}
                                            className="bg-white/5 border-white/10"
                                        />
                                        <p className="text-xs text-gray-400">Applied to project builds and pipeline execution</p>
                                    </div>
                                </div>
                                <Button onClick={updateCreditConfig} className="mt-4 bg-fuchsia-500 hover:bg-fuchsia-600">
                                    Save Credit Configuration
                                </Button>
                            </div>

                            {/* Other Settings */}
                            <div className="glass-card rounded-xl p-6">
                                <h3 className="text-lg font-semibold mb-4">System Settings</h3>
                                <div className="space-y-4">
                                    {Object.entries(settings).filter(([k]) => !k.includes('credits_per')).map(([key, value]) => (
                                        <div key={key} className="flex items-center justify-between py-2 border-b border-white/10">
                                            <div>
                                                <p className="font-medium">{key.replace(/_/g, ' ')}</p>
                                            </div>
                                            <span className="text-gray-400">{String(value)}</span>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </TabsContent>
                </Tabs>
            </main>

            {/* Edit User Dialog */}
            <Dialog open={!!editingUser} onOpenChange={() => setEditingUser(null)}>
                <DialogContent className="bg-[#0B0F19] border-white/10">
                    <DialogHeader>
                        <DialogTitle>Edit User</DialogTitle>
                    </DialogHeader>
                    {editingUser && (
                        <div className="space-y-4 py-4">
                            <div className="space-y-2">
                                <Label>Role</Label>
                                <Select value={editForm.role} onValueChange={(v) => setEditForm({ ...editForm, role: v })}>
                                    <SelectTrigger className="bg-white/5 border-white/10">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent className="bg-[#0B0F19] border-white/10">
                                        <SelectItem value="user">User</SelectItem>
                                        <SelectItem value="admin">Admin</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                            <div className="space-y-2">
                                <Label>Plan</Label>
                                <Select value={editForm.plan} onValueChange={(v) => setEditForm({ ...editForm, plan: v })}>
                                    <SelectTrigger className="bg-white/5 border-white/10">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent className="bg-[#0B0F19] border-white/10">
                                        {plans.filter(p => p.is_active !== false).map(plan => (
                                            <SelectItem key={plan.plan_id} value={plan.plan_id}>
                                                {plan.name} ({plan.max_concurrent_workspaces} workspaces)
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                            </div>
                            <div className="space-y-2">
                                <Label>Credits</Label>
                                <Input
                                    type="number"
                                    value={editForm.credits}
                                    onChange={(e) => setEditForm({ ...editForm, credits: e.target.value })}
                                    className="bg-white/5 border-white/10"
                                />
                            </div>
                            <div className="flex items-center justify-between">
                                <Label>Credits Enabled</Label>
                                <Switch
                                    checked={editForm.credits_enabled}
                                    onCheckedChange={(v) => setEditForm({ ...editForm, credits_enabled: v })}
                                />
                            </div>
                            <p className="text-xs text-gray-400">When disabled, user can use AI without credit deduction</p>
                            <Button
                                onClick={saveUserChanges}
                                disabled={saving}
                                className="w-full bg-fuchsia-500 hover:bg-fuchsia-600"
                            >
                                {saving ? 'Saving...' : 'Save Changes'}
                            </Button>
                        </div>
                    )}
                </DialogContent>
            </Dialog>

            {/* Plan Edit/Create Dialog */}
            <Dialog open={!!editingPlan || creatingPlan} onOpenChange={() => { setEditingPlan(null); setCreatingPlan(false); }}>
                <DialogContent className="bg-[#0B0F19] border-white/10 max-w-lg">
                    <DialogHeader>
                        <DialogTitle>{creatingPlan ? 'Create New Plan' : 'Edit Plan'}</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-4 py-4 max-h-[60vh] overflow-y-auto">
                        {creatingPlan && (
                            <div className="space-y-2">
                                <Label>Plan ID (unique, lowercase)</Label>
                                <Input
                                    value={planForm.plan_id}
                                    onChange={(e) => setPlanForm({ ...planForm, plan_id: e.target.value.toLowerCase().replace(/\s/g, '_') })}
                                    placeholder="e.g., basic, premium"
                                    className="bg-white/5 border-white/10"
                                />
                            </div>
                        )}
                        <div className="space-y-2">
                            <Label>Plan Name</Label>
                            <Input
                                value={planForm.name}
                                onChange={(e) => setPlanForm({ ...planForm, name: e.target.value })}
                                placeholder="e.g., Basic Plan"
                                className="bg-white/5 border-white/10"
                            />
                        </div>
                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label>Price/Month ($)</Label>
                                <Input
                                    type="number"
                                    step="0.01"
                                    value={planForm.price_monthly}
                                    onChange={(e) => setPlanForm({ ...planForm, price_monthly: e.target.value })}
                                    className="bg-white/5 border-white/10"
                                />
                            </div>
                            <div className="space-y-2">
                                <Label>Daily Credits</Label>
                                <Input
                                    type="number"
                                    value={planForm.daily_credits}
                                    onChange={(e) => setPlanForm({ ...planForm, daily_credits: e.target.value })}
                                    className="bg-white/5 border-white/10"
                                />
                            </div>
                        </div>
                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label>Max Concurrent Workspaces</Label>
                                <Input
                                    type="number"
                                    min="1"
                                    value={planForm.max_concurrent_workspaces}
                                    onChange={(e) => setPlanForm({ ...planForm, max_concurrent_workspaces: e.target.value })}
                                    className="bg-white/5 border-white/10"
                                />
                                <p className="text-xs text-gray-400">How many AI jobs can run at once</p>
                            </div>
                            <div className="space-y-2">
                                <Label>Max Projects (-1 = unlimited)</Label>
                                <Input
                                    type="number"
                                    value={planForm.max_projects}
                                    onChange={(e) => setPlanForm({ ...planForm, max_projects: e.target.value })}
                                    className="bg-white/5 border-white/10"
                                />
                            </div>
                        </div>
                        <div className="flex items-center justify-between">
                            <Label>Allow Own API Keys</Label>
                            <Switch
                                checked={planForm.allows_own_api_keys}
                                onCheckedChange={(v) => setPlanForm({ ...planForm, allows_own_api_keys: v })}
                            />
                        </div>
                        <p className="text-xs text-gray-400">When enabled, users can add their own OpenAI/Anthropic keys to bypass credits</p>
                        <div className="space-y-2">
                            <Label>Features (one per line)</Label>
                            <Textarea
                                value={planForm.features}
                                onChange={(e) => setPlanForm({ ...planForm, features: e.target.value })}
                                placeholder="Feature 1&#10;Feature 2&#10;Feature 3"
                                rows={5}
                                className="bg-white/5 border-white/10"
                            />
                        </div>
                        <Button
                            onClick={savePlan}
                            disabled={saving || (creatingPlan && !planForm.plan_id)}
                            className="w-full bg-fuchsia-500 hover:bg-fuchsia-600"
                        >
                            {saving ? 'Saving...' : (creatingPlan ? 'Create Plan' : 'Save Changes')}
                        </Button>
                    </div>
                </DialogContent>
            </Dialog>

            {/* Credit Package Dialog */}
            <Dialog open={!!editingPackage || creatingPackage} onOpenChange={() => { setEditingPackage(null); setCreatingPackage(false); }}>
                <DialogContent className="bg-[#0B0F19] border-white/10 max-w-md">
                    <DialogHeader>
                        <DialogTitle>{creatingPackage ? 'Create New Credit Package' : 'Edit Credit Package'}</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-4 py-4">
                        <div className="space-y-2">
                            <Label>Package Name</Label>
                            <Input
                                value={packageForm.name}
                                onChange={(e) => setPackageForm({ ...packageForm, name: e.target.value })}
                                placeholder="e.g., Starter Pack"
                                className="bg-white/5 border-white/10"
                            />
                        </div>
                        
                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label>Credits</Label>
                                <Input
                                    type="number"
                                    value={packageForm.credits}
                                    onChange={(e) => setPackageForm({ ...packageForm, credits: parseInt(e.target.value) || 0 })}
                                    placeholder="100"
                                    className="bg-white/5 border-white/10"
                                />
                            </div>
                            <div className="space-y-2">
                                <Label>Price ($)</Label>
                                <Input
                                    type="number"
                                    step="0.01"
                                    value={packageForm.price}
                                    onChange={(e) => setPackageForm({ ...packageForm, price: parseFloat(e.target.value) || 0 })}
                                    placeholder="9.99"
                                    className="bg-white/5 border-white/10"
                                />
                            </div>
                        </div>

                        <div className="flex items-center justify-between p-3 bg-white/5 rounded-lg">
                            <div>
                                <Label>Active</Label>
                                <p className="text-xs text-gray-400">Package visible to users</p>
                            </div>
                            <Switch
                                checked={packageForm.is_active}
                                onCheckedChange={(checked) => setPackageForm({ ...packageForm, is_active: checked })}
                            />
                        </div>

                        {packageForm.credits > 0 && packageForm.price > 0 && (
                            <div className="p-3 bg-cyan-500/10 rounded-lg border border-cyan-500/20">
                                <p className="text-sm text-cyan-400">
                                    Price per credit: ${(packageForm.price / packageForm.credits).toFixed(4)}
                                </p>
                            </div>
                        )}
                        
                        <Button 
                            onClick={savePackage} 
                            disabled={saving || !packageForm.name || packageForm.credits <= 0}
                            className="w-full bg-cyan-500 hover:bg-cyan-600"
                        >
                            {saving ? 'Saving...' : (creatingPackage ? 'Create Package' : 'Save Changes')}
                        </Button>
                    </div>
                </DialogContent>
            </Dialog>
        </div>
    );
}
