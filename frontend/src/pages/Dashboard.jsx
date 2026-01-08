import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { projectsAPI } from '../lib/api';
import { formatDate, formatCredits, LANGUAGES } from '../lib/utils';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { 
    Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger 
} from '../components/ui/dialog';
import {
    Select, SelectContent, SelectItem, SelectTrigger, SelectValue
} from '../components/ui/select';
import {
    DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger
} from '../components/ui/dropdown-menu';
import { 
    Zap, Plus, Search, Code, Calendar, ArrowRight, 
    User, LogOut, CreditCard, Settings, LayoutDashboard, Shield
} from 'lucide-react';

export default function Dashboard() {
    const { user, logout } = useAuth();
    const { t } = useI18n();
    const navigate = useNavigate();
    const [projects, setProjects] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchQuery, setSearchQuery] = useState('');
    const [showNewProject, setShowNewProject] = useState(false);
    const [newProject, setNewProject] = useState({ name: '', description: '', language: 'Python' });
    const [creating, setCreating] = useState(false);

    useEffect(() => {
        loadProjects();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const loadProjects = async () => {
        try {
            const res = await projectsAPI.getAll();
            setProjects(res.data);
        } catch (error) {
            console.error('Failed to load projects:', error);
        } finally {
            setLoading(false);
        }
    };

    const createProject = async () => {
        if (!newProject.name.trim()) return;
        setCreating(true);
        try {
            const res = await projectsAPI.create(newProject);
            setShowNewProject(false);
            setNewProject({ name: '', description: '', language: 'Python' });
            navigate(`/workspace/${res.data.id}`);
        } catch (error) {
            console.error('Failed to create project:', error);
        } finally {
            setCreating(false);
        }
    };

    const filteredProjects = projects.filter(p => 
        p.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        p.description?.toLowerCase().includes(searchQuery.toLowerCase())
    );

    const handleLogout = () => {
        logout();
        navigate('/');
    };

    return (
        <div className="min-h-screen bg-[#030712]">
            {/* Header */}
            <header className="sticky top-0 z-50 border-b border-white/10 bg-black/50 backdrop-blur-xl">
                <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <div className="flex items-center justify-between h-16">
                        <Link to="/dashboard" className="flex items-center gap-3" data-testid="dashboard-logo">
                            <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-fuchsia-500 to-cyan-500 flex items-center justify-center">
                                <Zap className="w-5 h-5 text-white" />
                            </div>
                            <span className="text-xl font-bold text-white font-outfit">LITTLE HELPER AI</span>
                        </Link>
                        
                        <div className="flex items-center gap-4">
                            {/* Admin badge */}
                            {user?.role === 'admin' && (
                                <Link to="/admin" data-testid="admin-link">
                                    <Button variant="outline" size="sm" className="border-yellow-500/50 text-yellow-400 hover:bg-yellow-500/10">
                                        <Shield size={16} className="mr-1" />
                                        Admin
                                    </Button>
                                </Link>
                            )}
                            
                            {/* Credits */}
                            <Link to="/credits" data-testid="credits-link">
                                <Button variant="outline" className="border-fuchsia-500/50 hover:bg-fuchsia-500/10 glow-primary">
                                    <Zap size={16} className="mr-2 text-fuchsia-400" />
                                    <span className="text-fuchsia-300">{formatCredits(user?.credits || 0)}</span>
                                    <span className="text-gray-500 ml-1">{t('nav_credits').toLowerCase()}</span>
                                </Button>
                            </Link>
                            
                            {/* User menu */}
                            <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                    <Button variant="ghost" size="icon" className="rounded-full" data-testid="user-menu">
                                        <div className="w-9 h-9 rounded-full bg-gradient-to-br from-fuchsia-500 to-cyan-500 flex items-center justify-center overflow-hidden">
                                            {user?.avatar_url ? (
                                                <img src={user.avatar_url} alt="Avatar" className="w-full h-full object-cover" />
                                            ) : (
                                                <span className="text-white font-medium">{user?.name?.[0]?.toUpperCase() || 'U'}</span>
                                            )}
                                        </div>
                                    </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end" className="w-56 bg-[#0B0F19] border-white/10">
                                    <div className="px-3 py-2">
                                        <p className="font-medium">{user?.display_name || user?.name}</p>
                                        <p className="text-sm text-gray-400">{user?.email}</p>
                                    </div>
                                    <DropdownMenuSeparator className="bg-white/10" />
                                    <DropdownMenuItem asChild>
                                        <Link to="/profile" className="cursor-pointer" data-testid="profile-menu-item">
                                            <User size={16} className="mr-2" />
                                            {t('nav_profile')}
                                        </Link>
                                    </DropdownMenuItem>
                                    <DropdownMenuItem className="cursor-pointer" data-testid="dashboard-menu-item">
                                        <LayoutDashboard size={16} className="mr-2" />
                                        {t('nav_dashboard')}
                                    </DropdownMenuItem>
                                    <DropdownMenuItem asChild>
                                        <Link to="/credits" className="cursor-pointer" data-testid="buy-credits-menu-item">
                                            <CreditCard size={16} className="mr-2" />
                                            {t('nav_buy_credits')}
                                        </Link>
                                    </DropdownMenuItem>
                                    <DropdownMenuSeparator className="bg-white/10" />
                                    <DropdownMenuItem onClick={handleLogout} className="cursor-pointer text-red-400" data-testid="logout-menu-item">
                                        <LogOut size={16} className="mr-2" />
                                        {t('nav_logout')}
                                    </DropdownMenuItem>
                                </DropdownMenuContent>
                            </DropdownMenu>
                        </div>
                    </div>
                </div>
            </header>

            {/* Main content */}
            <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
                {/* Page header */}
                <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-8">
                    <div>
                        <h1 className="text-3xl font-bold font-outfit">{t('dashboard_title')}</h1>
                        <p className="text-gray-400 mt-1">{projects.length} {t('dashboard_projects_count')}</p>
                    </div>
                    
                    <div className="flex items-center gap-3 w-full sm:w-auto">
                        {/* Search */}
                        <div className="relative flex-1 sm:w-64">
                            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                            <Input
                                type="text"
                                placeholder={t('dashboard_search_projects')}
                                value={searchQuery}
                                onChange={(e) => setSearchQuery(e.target.value)}
                                className="pl-9 bg-white/5 border-white/10"
                                data-testid="search-projects"
                            />
                        </div>
                        
                        {/* New Project Dialog */}
                        <Dialog open={showNewProject} onOpenChange={setShowNewProject}>
                            <DialogTrigger asChild>
                                <Button className="bg-fuchsia-500 hover:bg-fuchsia-600 glow-primary btn-glow" data-testid="new-project-btn">
                                    <Plus size={20} className="mr-2" />
                                    {t('dashboard_new_project')}
                                </Button>
                            </DialogTrigger>
                            <DialogContent className="bg-[#0B0F19] border-white/10">
                                <DialogHeader>
                                    <DialogTitle>{t('dashboard_new_project')}</DialogTitle>
                                </DialogHeader>
                                <div className="space-y-4 py-4">
                                    <div className="space-y-2">
                                        <Label>{t('dashboard_project_name')}</Label>
                                        <Input
                                            value={newProject.name}
                                            onChange={(e) => setNewProject({ ...newProject, name: e.target.value })}
                                            placeholder="My Awesome Project"
                                            className="bg-white/5 border-white/10"
                                            data-testid="project-name-input"
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label>{t('dashboard_project_description')}</Label>
                                        <Input
                                            value={newProject.description}
                                            onChange={(e) => setNewProject({ ...newProject, description: e.target.value })}
                                            placeholder="A brief description..."
                                            className="bg-white/5 border-white/10"
                                            data-testid="project-description-input"
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label>{t('dashboard_project_language')}</Label>
                                        <Select 
                                            value={newProject.language} 
                                            onValueChange={(v) => setNewProject({ ...newProject, language: v })}
                                        >
                                            <SelectTrigger className="bg-white/5 border-white/10" data-testid="project-language-select">
                                                <SelectValue />
                                            </SelectTrigger>
                                            <SelectContent className="bg-[#0B0F19] border-white/10">
                                                {Object.keys(LANGUAGES).map((lang) => (
                                                    <SelectItem key={lang} value={lang}>{lang}</SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <Button
                                        onClick={createProject}
                                        disabled={creating || !newProject.name.trim()}
                                        className="w-full bg-fuchsia-500 hover:bg-fuchsia-600"
                                        data-testid="create-project-submit"
                                    >
                                        {creating ? (
                                            <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                                        ) : (
                                            <>{t('dashboard_create_project')}</>
                                        )}
                                    </Button>
                                </div>
                            </DialogContent>
                        </Dialog>
                    </div>
                </div>

                {/* Projects grid */}
                {loading ? (
                    <div className="flex items-center justify-center py-20">
                        <div className="w-8 h-8 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
                    </div>
                ) : filteredProjects.length === 0 ? (
                    <motion.div
                        initial={{ opacity: 0 }}
                        animate={{ opacity: 1 }}
                        className="text-center py-20"
                    >
                        {projects.length === 0 ? (
                            <>
                                <div className="w-16 h-16 rounded-2xl bg-fuchsia-500/10 flex items-center justify-center mx-auto mb-4">
                                    <Code className="w-8 h-8 text-fuchsia-400" />
                                </div>
                                <h3 className="text-xl font-semibold mb-2">{t('dashboard_no_projects')}</h3>
                                <p className="text-gray-400 mb-6">{t('dashboard_create_first')}</p>
                                <Button 
                                    onClick={() => setShowNewProject(true)}
                                    className="bg-fuchsia-500 hover:bg-fuchsia-600"
                                    data-testid="create-first-project"
                                >
                                    <Plus size={20} className="mr-2" />
                                    {t('dashboard_create_project')}
                                </Button>
                            </>
                        ) : (
                            <>
                                <Search className="w-12 h-12 text-gray-600 mx-auto mb-4" />
                                <h3 className="text-xl font-semibold mb-2">No matching projects</h3>
                                <p className="text-gray-400">Try a different search term</p>
                            </>
                        )}
                    </motion.div>
                ) : (
                    <motion.div
                        initial={{ opacity: 0 }}
                        animate={{ opacity: 1 }}
                        className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4"
                    >
                        {filteredProjects.map((project, index) => (
                            <motion.div
                                key={project.id}
                                initial={{ opacity: 0, y: 20 }}
                                animate={{ opacity: 1, y: 0 }}
                                transition={{ delay: index * 0.05 }}
                            >
                                <Link 
                                    to={`/workspace/${project.id}`}
                                    className="block glass-card rounded-xl p-5 hover:border-fuchsia-500/30 transition-all group"
                                    data-testid={`project-card-${project.id}`}
                                >
                                    <div className="flex items-start justify-between mb-3">
                                        <div 
                                            className="w-10 h-10 rounded-lg flex items-center justify-center"
                                            style={{ backgroundColor: `${LANGUAGES[project.language]?.color || '#D946EF'}20` }}
                                        >
                                            <Code size={20} style={{ color: LANGUAGES[project.language]?.color || '#D946EF' }} />
                                        </div>
                                        <ArrowRight className="w-5 h-5 text-gray-600 group-hover:text-fuchsia-400 group-hover:translate-x-1 transition-all" />
                                    </div>
                                    
                                    <h3 className="font-semibold text-lg mb-1 group-hover:text-fuchsia-300 transition-colors">{project.name}</h3>
                                    <p className="text-sm text-fuchsia-400 mb-2">{project.language}</p>
                                    
                                    {project.description && (
                                        <p className="text-sm text-gray-400 mb-3 line-clamp-2">{project.description}</p>
                                    )}
                                    
                                    <div className="flex items-center gap-2 text-xs text-gray-500">
                                        <Calendar size={12} />
                                        <span>{formatDate(project.created_at || project.createdAt)}</span>
                                    </div>
                                </Link>
                            </motion.div>
                        ))}
                    </motion.div>
                )}
            </main>
        </div>
    );
}
