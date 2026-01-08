import { useState, useEffect, useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../lib/auth';
import { useTheme } from '../lib/theme';
import { useI18n } from '../lib/i18n';
import { profileAPI, aiProvidersAPI, creditsAPI, plansAPI } from '../lib/api';
import { formatCredits } from '../lib/utils';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Switch } from '../components/ui/switch';
import { ScrollArea } from '../components/ui/scroll-area';
import {
    Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger
} from '../components/ui/dialog';
import {
    Select, SelectContent, SelectItem, SelectTrigger, SelectValue
} from '../components/ui/select';
import { 
    ArrowLeft, User, Camera, Lock, Palette, Check, RefreshCw,
    Eye, EyeOff, Trash2, Globe, Key, Zap, Plus, LogOut,
    AlertCircle, CheckCircle, Crown, CreditCard
} from 'lucide-react';

const LANGUAGES = [
    { code: 'en', name: 'English', flag: '🇺🇸' },
    { code: 'es', name: 'Español', flag: '🇪🇸' },
    { code: 'fr', name: 'Français', flag: '🇫🇷' },
    { code: 'de', name: 'Deutsch', flag: '🇩🇪' },
    { code: 'zh', name: '中文', flag: '🇨🇳' },
    { code: 'ja', name: '日本語', flag: '🇯🇵' }
];

const PROVIDER_INFO = {
    openai: { name: 'OpenAI', color: '#10B981', models: ['gpt-4o', 'gpt-4o-mini', 'gpt-4-turbo', 'gpt-3.5-turbo'] },
    anthropic: { name: 'Anthropic', color: '#D946EF', models: ['claude-3-opus', 'claude-3-sonnet', 'claude-3-haiku'] },
    google: { name: 'Google AI', color: '#3B82F6', models: ['gemini-pro', 'gemini-pro-vision'] },
    azure: { name: 'Azure OpenAI', color: '#06B6D4', models: ['gpt-4', 'gpt-35-turbo'] },
    local: { name: 'Local (Ollama)', color: '#F59E0B', models: ['qwen2.5-coder:1.5b', 'codellama', 'mistral'] }
};

// ColorPicker component
const ColorPicker = ({ label, value, onChange }) => (
    <div className="flex items-center justify-between">
        <Label className="text-sm">{label}</Label>
        <div className="flex items-center gap-2">
            <input
                type="color"
                value={value || '#000000'}
                onChange={(e) => onChange(e.target.value)}
                className="w-10 h-8 rounded cursor-pointer border border-white/10"
            />
            <Input
                value={value || ''}
                onChange={(e) => onChange(e.target.value)}
                className="w-24 h-8 text-xs bg-white/5 border-white/10"
                placeholder="#000000"
            />
        </div>
    </div>
);

export default function Profile() {
    const { user, refreshUser, logout } = useAuth();
    const { theme, updateTheme, resetTheme, defaultTheme } = useTheme();
    const { t, language, setLanguage, supportedLanguages } = useI18n();
    const navigate = useNavigate();
    
    const [profile, setProfile] = useState({ name: '', display_name: '', avatar_url: null });
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [saved, setSaved] = useState(false);
    
    // Password change
    const [showPasswordDialog, setShowPasswordDialog] = useState(false);
    const [passwordForm, setPasswordForm] = useState({ current: '', new: '', confirm: '' });
    const [showCurrentPassword, setShowCurrentPassword] = useState(false);
    const [showNewPassword, setShowNewPassword] = useState(false);
    const [passwordError, setPasswordError] = useState('');
    const [changingPassword, setChangingPassword] = useState(false);
    
    // Theme editing
    const [editingTheme, setEditingTheme] = useState(null);
    
    // Avatar upload
    const fileInputRef = useRef(null);
    const [uploadingAvatar, setUploadingAvatar] = useState(false);
    
    // AI Provider state (from Settings)
    const [availableProviders, setAvailableProviders] = useState({});
    const [userProviders, setUserProviders] = useState([]);
    const [loadingProviders, setLoadingProviders] = useState(true);
    const [showAddProvider, setShowAddProvider] = useState(false);
    const [newProvider, setNewProvider] = useState({ provider: '', api_key: '', model_preference: '', is_default: false });
    const [showApiKey, setShowApiKey] = useState(false);
    const [addingProvider, setAddingProvider] = useState(false);
    const [planAllowsApiKeys, setPlanAllowsApiKeys] = useState(false);
    
    // Credit history
    const [creditHistory, setCreditHistory] = useState([]);
    
    // Google Drive integration
    const [googleDrive, setGoogleDrive] = useState({
        isConnected: false,
        email: '',
        accessToken: '',
        refreshToken: ''
    });
    const [savingGoogleDrive, setSavingGoogleDrive] = useState(false);

    useEffect(() => {
        loadProfile();
        loadProviders();
        loadCreditHistory();
        checkPlanPermissions();
        loadGoogleDriveConfig();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const loadProfile = async () => {
        try {
            const res = await profileAPI.get();
            setProfile({
                name: res.data.name || '',
                display_name: res.data.display_name || res.data.name || '',
                avatar_url: res.data.avatar_url
            });
            setEditingTheme(res.data.theme || defaultTheme);
        } catch (error) {
            console.error('Failed to load profile:', error);
        } finally {
            setLoading(false);
        }
    };

    const loadProviders = async () => {
        try {
            const [availableRes, userRes] = await Promise.all([
                aiProvidersAPI.getAvailable(),
                aiProvidersAPI.getUserProviders()
            ]);
            setAvailableProviders(availableRes.data);
            setUserProviders(userRes.data);
        } catch (error) {
            console.error('Failed to load providers:', error);
        } finally {
            setLoadingProviders(false);
        }
    };

    const checkPlanPermissions = async () => {
        try {
            const res = await plansAPI.getUserSubscription();
            const plan = res.data.plan;
            setPlanAllowsApiKeys(plan?.allows_own_api_keys || false);
        } catch (error) {
            console.error('Failed to check plan permissions:', error);
            setPlanAllowsApiKeys(false);
        }
    };

    const loadCreditHistory = async () => {
        try {
            const res = await creditsAPI.getHistory(20);
            setCreditHistory(res.data);
        } catch (error) {
            console.error('Failed to load credit history:', error);
        }
    };

    const saveProfile = async () => {
        setSaving(true);
        try {
            await profileAPI.update({
                name: profile.name,
                display_name: profile.display_name
            });
            await refreshUser();
            setSaved(true);
            setTimeout(() => setSaved(false), 2000);
        } catch (error) {
            console.error('Failed to save profile:', error);
        } finally {
            setSaving(false);
        }
    };

    const handleAvatarUpload = async (e) => {
        const file = e.target.files?.[0];
        if (!file) return;
        
        if (!file.type.startsWith('image/')) {
            alert('Please select an image file');
            return;
        }
        
        if (file.size > 5 * 1024 * 1024) {
            alert('Image must be less than 5MB');
            return;
        }
        
        setUploadingAvatar(true);
        try {
            const formData = new FormData();
            formData.append('file', file);
            const res = await profileAPI.uploadAvatar(formData);
            setProfile(prev => ({ ...prev, avatar_url: res.data.avatar_url }));
            await refreshUser();
        } catch (error) {
            console.error('Failed to upload avatar:', error);
            alert('Failed to upload avatar');
        } finally {
            setUploadingAvatar(false);
        }
    };

    const removeAvatar = async () => {
        try {
            await profileAPI.update({ avatar_url: '' });
            setProfile(prev => ({ ...prev, avatar_url: null }));
            await refreshUser();
        } catch (error) {
            console.error('Failed to remove avatar:', error);
        }
    };

    const changePassword = async () => {
        setPasswordError('');
        
        if (passwordForm.new !== passwordForm.confirm) {
            setPasswordError(t('error_password_mismatch', 'Passwords do not match'));
            return;
        }
        
        if (passwordForm.new.length < 8) {
            setPasswordError(t('error_password_min', 'Password must be at least 8 characters'));
            return;
        }
        
        setChangingPassword(true);
        try {
            await profileAPI.changePassword(passwordForm.current, passwordForm.new);
            setShowPasswordDialog(false);
            setPasswordForm({ current: '', new: '', confirm: '' });
            alert('Password changed successfully');
        } catch (error) {
            setPasswordError(error.response?.data?.detail || 'Failed to change password');
        } finally {
            setChangingPassword(false);
        }
    };

    const saveTheme = async () => {
        setSaving(true);
        try {
            await updateTheme(editingTheme);
            setSaved(true);
            setTimeout(() => setSaved(false), 2000);
        } catch (error) {
            console.error('Failed to save theme:', error);
        } finally {
            setSaving(false);
        }
    };

    const handleResetTheme = async () => {
        setEditingTheme(defaultTheme);
        await resetTheme();
    };

    const addProvider = async () => {
        if (!newProvider.provider || !newProvider.api_key) return;
        setAddingProvider(true);
        try {
            await aiProvidersAPI.addProvider(newProvider);
            await loadProviders();
            setShowAddProvider(false);
            setNewProvider({ provider: '', api_key: '', model_preference: '', is_default: false });
        } catch (error) {
            console.error('Failed to add provider:', error);
            alert(error.response?.data?.detail || 'Failed to add provider');
        } finally {
            setAddingProvider(false);
        }
    };

    const deleteProvider = async (provider) => {
        if (!window.confirm('Are you sure you want to remove this AI provider?')) return;
        try {
            await aiProvidersAPI.deleteProvider(provider);
            await loadProviders();
        } catch (error) {
            console.error('Failed to delete provider:', error);
        }
    };

    const handleLogout = () => {
        logout();
        navigate('/');
    };

    if (loading) {
        return (
            <div className="min-h-screen bg-[#030712] flex items-center justify-center">
                <div className="w-8 h-8 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-[var(--background-color,#030712)]">
            {/* Header */}
            <header className="border-b border-white/10 bg-[var(--card-color,#0B0F19)]/80 backdrop-blur-sm sticky top-0 z-10">
                <div className="max-w-4xl mx-auto px-6 py-4 flex items-center justify-between">
                    <div className="flex items-center gap-4">
                        <Link to="/dashboard">
                            <Button variant="ghost" size="icon">
                                <ArrowLeft size={20} />
                            </Button>
                        </Link>
                        <h1 className="text-xl font-bold">{t('settings_profile', 'Profile & Settings')}</h1>
                    </div>
                    <div className="flex items-center gap-3">
                        {saved && (
                            <motion.div
                                initial={{ opacity: 0, x: 20 }}
                                animate={{ opacity: 1, x: 0 }}
                                className="flex items-center gap-2 text-green-400"
                            >
                                <Check size={16} />
                                <span className="text-sm">{t('common_success', 'Saved')}</span>
                            </motion.div>
                        )}
                        <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-fuchsia-500/10 border border-fuchsia-500/30">
                            <Zap className="w-4 h-4" style={{ color: 'var(--credits-color, #d946ef)' }} />
                            <span style={{ color: 'var(--credits-color, #d946ef)' }} className="font-semibold">
                                {formatCredits(user?.credits || 0)}
                            </span>
                        </div>
                    </div>
                </div>
            </header>

            <main className="max-w-4xl mx-auto px-6 py-8 space-y-8">
                {/* Avatar & Basic Info */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    className="glass-card rounded-xl p-6"
                >
                    <h2 className="text-lg font-semibold mb-6 flex items-center gap-2">
                        <User className="w-5 h-5" style={{ color: 'var(--primary-color)' }} />
                        {t('settings_profile', 'Profile Information')}
                    </h2>
                    
                    <div className="flex flex-col md:flex-row gap-8">
                        {/* Avatar */}
                        <div className="flex flex-col items-center gap-4">
                            <div className="relative">
                                <div className="w-32 h-32 rounded-full overflow-hidden bg-white/10 flex items-center justify-center border-4 border-white/10">
                                    {profile.avatar_url ? (
                                        <img src={profile.avatar_url} alt="Avatar" className="w-full h-full object-cover" />
                                    ) : (
                                        <User size={48} className="text-gray-400" />
                                    )}
                                </div>
                                <button
                                    onClick={() => fileInputRef.current?.click()}
                                    disabled={uploadingAvatar}
                                    className="absolute bottom-0 right-0 w-10 h-10 rounded-full flex items-center justify-center border-2 border-white/20"
                                    style={{ backgroundColor: 'var(--primary-color)' }}
                                >
                                    {uploadingAvatar ? (
                                        <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                                    ) : (
                                        <Camera size={18} className="text-white" />
                                    )}
                                </button>
                                <input
                                    ref={fileInputRef}
                                    type="file"
                                    accept="image/*"
                                    onChange={handleAvatarUpload}
                                    className="hidden"
                                />
                            </div>
                            {profile.avatar_url && (
                                <Button variant="ghost" size="sm" onClick={removeAvatar} className="text-red-400">
                                    <Trash2 size={14} className="mr-1" />
                                    Remove
                                </Button>
                            )}
                            <div className="text-center">
                                <span className={`text-xs px-2 py-0.5 rounded ${
                                    user?.plan === 'enterprise' ? 'bg-purple-500/20 text-purple-400' :
                                    user?.plan === 'pro' ? 'bg-fuchsia-500/20 text-fuchsia-400' :
                                    user?.plan === 'starter' ? 'bg-cyan-500/20 text-cyan-400' :
                                    'bg-gray-500/20 text-gray-400'
                                }`}>
                                    {user?.plan || 'free'} plan
                                </span>
                            </div>
                        </div>
                        
                        {/* Name fields */}
                        <div className="flex-1 space-y-4">
                            <div className="space-y-2">
                                <Label>{t('auth_name', 'Full Name')}</Label>
                                <Input
                                    value={profile.name}
                                    onChange={(e) => setProfile(prev => ({ ...prev, name: e.target.value }))}
                                    className="bg-white/5 border-white/10"
                                    placeholder="John Doe"
                                />
                            </div>
                            <div className="space-y-2">
                                <Label>Display Name (AI will use this)</Label>
                                <Input
                                    value={profile.display_name}
                                    onChange={(e) => setProfile(prev => ({ ...prev, display_name: e.target.value }))}
                                    className="bg-white/5 border-white/10"
                                    placeholder="How you want AI to address you"
                                />
                                <p className="text-xs text-gray-500">The AI assistant will address you by this name</p>
                            </div>
                            <div className="space-y-2">
                                <Label>{t('auth_email', 'Email')}</Label>
                                <Input
                                    value={user?.email || ''}
                                    disabled
                                    className="bg-white/5 border-white/10 opacity-50 cursor-not-allowed"
                                />
                            </div>
                            <Button onClick={saveProfile} disabled={saving} style={{ backgroundColor: 'var(--primary-color)' }}>
                                {saving ? t('common_loading', 'Saving...') : t('settings_update_profile', 'Update Profile')}
                            </Button>
                        </div>
                    </div>
                </motion.div>

                {/* Password Change */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.1 }}
                    className="glass-card rounded-xl p-6"
                >
                    <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
                        <Lock className="w-5 h-5" style={{ color: 'var(--primary-color)' }} />
                        {t('settings_change_password', 'Security')}
                    </h2>
                    <p className="text-sm text-gray-400 mb-4">Update your password to keep your account secure</p>
                    <Button variant="outline" onClick={() => setShowPasswordDialog(true)} className="border-white/20">
                        <Lock size={16} className="mr-2" />
                        {t('settings_change_password', 'Change Password')}
                    </Button>
                </motion.div>

                {/* Language */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.15 }}
                    className="glass-card rounded-xl p-6"
                >
                    <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
                        <Globe className="w-5 h-5" style={{ color: 'var(--primary-color)' }} />
                        {t('settings_language', 'Language')}
                    </h2>
                    <p className="text-sm text-gray-400 mb-4">Select your preferred language for the interface</p>
                    <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                        {LANGUAGES.map(lang => (
                            <button
                                key={lang.code}
                                onClick={() => setLanguage(lang.code)}
                                className={`p-3 rounded-lg border transition-all flex items-center gap-3 ${
                                    language === lang.code
                                        ? 'border-fuchsia-500 bg-fuchsia-500/10'
                                        : 'border-white/10 bg-white/5 hover:bg-white/10'
                                }`}
                            >
                                <span className="text-2xl">{lang.flag}</span>
                                <span className="font-medium">{lang.name}</span>
                            </button>
                        ))}
                    </div>
                </motion.div>

                {/* AI Providers (from Settings) */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.2 }}
                    className="glass-card rounded-xl p-6"
                >
                    <div className="flex items-center justify-between mb-4">
                        <div className="flex items-center gap-3">
                            <Key className="w-5 h-5" style={{ color: 'var(--primary-color)' }} />
                            <h2 className="text-lg font-semibold">AI Providers</h2>
                            {!planAllowsApiKeys && (
                                <span className="flex items-center gap-1 px-2 py-0.5 rounded bg-amber-500/20 text-amber-400 text-xs">
                                    <Lock size={12} />
                                    Pro+ Required
                                </span>
                            )}
                        </div>
                        {planAllowsApiKeys ? (
                            <Dialog open={showAddProvider} onOpenChange={setShowAddProvider}>
                                <DialogTrigger asChild>
                                    <Button variant="outline" size="sm" className="border-fuchsia-500/50 text-fuchsia-400">
                                        <Plus size={16} className="mr-1" />
                                        Add Provider
                                    </Button>
                                </DialogTrigger>
                                <DialogContent className="bg-[#0B0F19] border-white/10">
                                    <DialogHeader>
                                        <DialogTitle>Add AI Provider</DialogTitle>
                                    </DialogHeader>
                                    <div className="space-y-4 py-4">
                                        <div className="space-y-2">
                                            <Label>Provider</Label>
                                            <Select 
                                                value={newProvider.provider} 
                                                onValueChange={(v) => setNewProvider({ ...newProvider, provider: v, model_preference: '' })}
                                            >
                                                <SelectTrigger className="bg-white/5 border-white/10">
                                                    <SelectValue placeholder="Select provider" />
                                                </SelectTrigger>
                                                <SelectContent className="bg-[#0B0F19] border-white/10">
                                                    {Object.entries(PROVIDER_INFO).map(([id, config]) => (
                                                        <SelectItem key={id} value={id}>
                                                            {config.name}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        
                                        {newProvider.provider && newProvider.provider !== 'local' && (
                                            <div className="space-y-2">
                                                <Label>API Key</Label>
                                                <div className="relative">
                                                    <Input
                                                        type={showApiKey ? 'text' : 'password'}
                                                        value={newProvider.api_key}
                                                        onChange={(e) => setNewProvider({ ...newProvider, api_key: e.target.value })}
                                                        placeholder="sk-..."
                                                        className="bg-white/5 border-white/10 pr-10"
                                                    />
                                                    <button
                                                        type="button"
                                                        onClick={() => setShowApiKey(!showApiKey)}
                                                        className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400"
                                                    >
                                                        {showApiKey ? <EyeOff size={16} /> : <Eye size={16} />}
                                                    </button>
                                                </div>
                                            </div>
                                        )}
                                        
                                        {newProvider.provider && PROVIDER_INFO[newProvider.provider] && (
                                            <div className="space-y-2">
                                                <Label>Preferred Model</Label>
                                                <Select 
                                                    value={newProvider.model_preference} 
                                                    onValueChange={(v) => setNewProvider({ ...newProvider, model_preference: v })}
                                                >
                                                    <SelectTrigger className="bg-white/5 border-white/10">
                                                        <SelectValue placeholder="Select model" />
                                                    </SelectTrigger>
                                                    <SelectContent className="bg-[#0B0F19] border-white/10">
                                                        {PROVIDER_INFO[newProvider.provider].models.map((model) => (
                                                            <SelectItem key={model} value={model}>{model}</SelectItem>
                                                        ))}
                                                    </SelectContent>
                                                </Select>
                                            </div>
                                        )}
                                        
                                        <div className="flex items-center justify-between">
                                            <Label>Set as Default</Label>
                                            <Switch
                                                checked={newProvider.is_default}
                                                onCheckedChange={(v) => setNewProvider({ ...newProvider, is_default: v })}
                                            />
                                        </div>
                                        
                                        <Button
                                            onClick={addProvider}
                                            disabled={addingProvider || !newProvider.provider || (newProvider.provider !== 'local' && !newProvider.api_key)}
                                            className="w-full"
                                            style={{ backgroundColor: 'var(--primary-color)' }}
                                        >
                                            {addingProvider ? 'Adding...' : 'Add Provider'}
                                        </Button>
                                    </div>
                                </DialogContent>
                            </Dialog>
                        ) : (
                            <Link to="/credits">
                                <Button variant="outline" size="sm" className="border-amber-500/50 text-amber-400 hover:bg-amber-500/10">
                                    <Crown size={16} className="mr-1" />
                                    Upgrade Plan
                                </Button>
                            </Link>
                        )}
                    </div>
                    
                    {!planAllowsApiKeys && (
                        <div className="mb-4 p-4 rounded-lg bg-amber-500/10 border border-amber-500/30">
                            <div className="flex items-start gap-3">
                                <Lock className="w-5 h-5 text-amber-400 mt-0.5" />
                                <div>
                                    <p className="font-medium text-amber-300">Feature Locked</p>
                                    <p className="text-sm text-gray-400 mt-1">
                                        Custom AI providers are available on Pro, OpenAI, and Enterprise plans. 
                                        Upgrade your plan to use your own API keys and bypass credit usage.
                                    </p>
                                </div>
                            </div>
                        </div>
                    )}
                    
                    <p className="text-sm text-gray-400 mb-4">
                        Configure your own API keys to use different AI providers.
                    </p>
                    
                    {loadingProviders ? (
                        <div className="flex justify-center py-4">
                            <div className="w-6 h-6 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
                        </div>
                    ) : userProviders.length === 0 ? (
                        <div className="text-center py-6 text-gray-500">
                            <Key className="w-8 h-8 mx-auto mb-2 opacity-50" />
                            <p>No AI providers configured</p>
                            <p className="text-xs">Add your own API keys to use different AI models</p>
                        </div>
                    ) : (
                        <div className="space-y-3">
                            {userProviders.map((provider) => (
                                <div 
                                    key={provider.id} 
                                    className="flex items-center justify-between p-3 rounded-lg bg-white/5 border border-white/10"
                                >
                                    <div className="flex items-center gap-3">
                                        <div 
                                            className="w-8 h-8 rounded-lg flex items-center justify-center"
                                            style={{ backgroundColor: `${PROVIDER_INFO[provider.provider]?.color || '#666'}20` }}
                                        >
                                            <Key size={16} style={{ color: PROVIDER_INFO[provider.provider]?.color || '#666' }} />
                                        </div>
                                        <div>
                                            <p className="font-medium">{PROVIDER_INFO[provider.provider]?.name || provider.provider}</p>
                                            <p className="text-xs text-gray-400">
                                                {provider.model_preference || 'Default model'}
                                                {provider.is_default && (
                                                    <span className="ml-2 px-1.5 py-0.5 rounded bg-fuchsia-500/20 text-fuchsia-400">Default</span>
                                                )}
                                            </p>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        {provider.is_active ? (
                                            <CheckCircle size={16} className="text-green-400" />
                                        ) : (
                                            <AlertCircle size={16} className="text-yellow-400" />
                                        )}
                                        <Button
                                            variant="ghost"
                                            size="icon"
                                            onClick={() => deleteProvider(provider.provider)}
                                            className="text-red-400 hover:text-red-300"
                                        >
                                            <Trash2 size={16} />
                                        </Button>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </motion.div>

                {/* Credit History */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.25 }}
                    className="glass-card rounded-xl p-6"
                >
                    <div className="flex items-center justify-between mb-4">
                        <h2 className="text-lg font-semibold flex items-center gap-2">
                            <CreditCard className="w-5 h-5" style={{ color: 'var(--primary-color)' }} />
                            Recent Credit Activity
                        </h2>
                        <Link to="/credits" className="text-sm text-fuchsia-400 hover:text-fuchsia-300">
                            Buy more credits
                        </Link>
                    </div>
                    
                    {creditHistory.length === 0 ? (
                        <p className="text-gray-500 text-sm text-center py-4">No credit activity yet</p>
                    ) : (
                        <ScrollArea className="h-48">
                            <div className="space-y-2">
                                {creditHistory.map((entry) => (
                                    <div key={entry.id} className="flex items-center justify-between py-2 border-b border-white/5">
                                        <div>
                                            <p className="text-sm">{entry.reason}</p>
                                            <p className="text-xs text-gray-500">
                                                {new Date(entry.created_at).toLocaleString()}
                                            </p>
                                        </div>
                                        <span className={`font-medium ${entry.delta > 0 ? 'text-green-400' : 'text-red-400'}`}>
                                            {entry.delta > 0 ? '+' : ''}{entry.delta}
                                        </span>
                                    </div>
                                ))}
                            </div>
                        </ScrollArea>
                    )}
                </motion.div>

                {/* Theme Customization */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.3 }}
                    className="glass-card rounded-xl p-6"
                >
                    <div className="flex items-center justify-between mb-6">
                        <h2 className="text-lg font-semibold flex items-center gap-2">
                            <Palette className="w-5 h-5" style={{ color: 'var(--primary-color)' }} />
                            {t('settings_theme', 'Theme Customization')}
                        </h2>
                        <Button variant="ghost" size="sm" onClick={handleResetTheme}>
                            <RefreshCw size={14} className="mr-1" />
                            {t('theme_reset', 'Reset')}
                        </Button>
                    </div>
                    
                    <div className="grid gap-4 md:grid-cols-2">
                        <ColorPicker 
                            label={t('theme_primary', 'Primary Color')} 
                            value={editingTheme?.primary_color}
                            onChange={(v) => setEditingTheme(prev => ({ ...prev, primary_color: v }))}
                        />
                        <ColorPicker 
                            label={t('theme_secondary', 'Secondary Color')} 
                            value={editingTheme?.secondary_color}
                            onChange={(v) => setEditingTheme(prev => ({ ...prev, secondary_color: v }))}
                        />
                        <ColorPicker 
                            label={t('theme_background', 'Background')} 
                            value={editingTheme?.background_color}
                            onChange={(v) => setEditingTheme(prev => ({ ...prev, background_color: v }))}
                        />
                        <ColorPicker 
                            label={t('theme_card', 'Card Color')} 
                            value={editingTheme?.card_color}
                            onChange={(v) => setEditingTheme(prev => ({ ...prev, card_color: v }))}
                        />
                        <ColorPicker 
                            label={t('theme_text', 'Text Color')} 
                            value={editingTheme?.text_color}
                            onChange={(v) => setEditingTheme(prev => ({ ...prev, text_color: v }))}
                        />
                        <ColorPicker 
                            label={t('theme_hover', 'Hover Color')} 
                            value={editingTheme?.hover_color}
                            onChange={(v) => setEditingTheme(prev => ({ ...prev, hover_color: v }))}
                        />
                        <ColorPicker 
                            label={t('theme_credits', 'Credits Color')} 
                            value={editingTheme?.credits_color}
                            onChange={(v) => setEditingTheme(prev => ({ ...prev, credits_color: v }))}
                        />
                    </div>
                    
                    {/* Background Image */}
                    <div className="mt-6 space-y-2">
                        <Label>{t('theme_bg_image', 'Background Image URL')}</Label>
                        <Input
                            value={editingTheme?.background_image || ''}
                            onChange={(e) => setEditingTheme(prev => ({ ...prev, background_image: e.target.value }))}
                            className="bg-white/5 border-white/10"
                            placeholder="https://example.com/image.jpg"
                        />
                    </div>
                    
                    {/* Theme Preview */}
                    <div className="mt-6 p-4 rounded-lg border border-white/10" style={{ backgroundColor: editingTheme?.card_color }}>
                        <p className="text-sm mb-2" style={{ color: editingTheme?.text_color }}>Preview</p>
                        <div className="flex gap-2">
                            <div className="w-8 h-8 rounded" style={{ backgroundColor: editingTheme?.primary_color }} title="Primary" />
                            <div className="w-8 h-8 rounded" style={{ backgroundColor: editingTheme?.secondary_color }} title="Secondary" />
                            <div className="w-8 h-8 rounded" style={{ backgroundColor: editingTheme?.hover_color }} title="Hover" />
                            <div className="w-8 h-8 rounded" style={{ backgroundColor: editingTheme?.credits_color }} title="Credits" />
                        </div>
                    </div>
                    
                    <Button onClick={saveTheme} disabled={saving} className="mt-4" style={{ backgroundColor: 'var(--primary-color)' }}>
                        {saving ? t('common_loading', 'Saving...') : t('common_save', 'Save Theme')}
                    </Button>
                </motion.div>

                {/* Account Actions (Logout) */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.35 }}
                    className="glass-card rounded-xl p-6"
                >
                    <h2 className="text-lg font-semibold mb-4">Account</h2>
                    <Button
                        variant="destructive"
                        onClick={handleLogout}
                        className="w-full bg-red-500/10 border border-red-500/30 text-red-400 hover:bg-red-500/20"
                    >
                        <LogOut size={18} className="mr-2" />
                        Sign Out
                    </Button>
                </motion.div>
            </main>

            {/* Password Change Dialog */}
            <Dialog open={showPasswordDialog} onOpenChange={setShowPasswordDialog}>
                <DialogContent className="bg-[#0B0F19] border-white/10">
                    <DialogHeader>
                        <DialogTitle>{t('settings_change_password', 'Change Password')}</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-4 py-4">
                        {passwordError && (
                            <div className="p-3 rounded bg-red-500/10 border border-red-500/30 text-red-400 text-sm">
                                {passwordError}
                            </div>
                        )}
                        <div className="space-y-2">
                            <Label>{t('settings_current_password', 'Current Password')}</Label>
                            <div className="relative">
                                <Input
                                    type={showCurrentPassword ? 'text' : 'password'}
                                    value={passwordForm.current}
                                    onChange={(e) => setPasswordForm(prev => ({ ...prev, current: e.target.value }))}
                                    className="bg-white/5 border-white/10 pr-10"
                                />
                                <button
                                    type="button"
                                    onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400"
                                >
                                    {showCurrentPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                                </button>
                            </div>
                        </div>
                        <div className="space-y-2">
                            <Label>{t('settings_new_password', 'New Password')}</Label>
                            <div className="relative">
                                <Input
                                    type={showNewPassword ? 'text' : 'password'}
                                    value={passwordForm.new}
                                    onChange={(e) => setPasswordForm(prev => ({ ...prev, new: e.target.value }))}
                                    className="bg-white/5 border-white/10 pr-10"
                                />
                                <button
                                    type="button"
                                    onClick={() => setShowNewPassword(!showNewPassword)}
                                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400"
                                >
                                    {showNewPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                                </button>
                            </div>
                        </div>
                        <div className="space-y-2">
                            <Label>{t('settings_confirm_password', 'Confirm New Password')}</Label>
                            <Input
                                type="password"
                                value={passwordForm.confirm}
                                onChange={(e) => setPasswordForm(prev => ({ ...prev, confirm: e.target.value }))}
                                className="bg-white/5 border-white/10"
                            />
                        </div>
                        <Button
                            onClick={changePassword}
                            disabled={changingPassword || !passwordForm.current || !passwordForm.new || !passwordForm.confirm}
                            className="w-full"
                            style={{ backgroundColor: 'var(--primary-color)' }}
                        >
                            {changingPassword ? t('common_loading', 'Changing...') : t('settings_change_password', 'Change Password')}
                        </Button>
                    </div>
                </DialogContent>
            </Dialog>
        </div>
    );
}
