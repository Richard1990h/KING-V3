import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Button } from '../components/ui/button';
import { 
    Zap, ArrowRight, Play, LayoutGrid, Search, Code, 
    TestTube, Bug, CheckCircle, Sparkles, Globe
} from 'lucide-react';

const agents = [
    { id: 'planner', name: 'Planner', icon: LayoutGrid, color: '#D946EF', description: 'Generates multiple approaches and selects optimal path' },
    { id: 'researcher', name: 'Researcher', icon: Search, color: '#06B6D4', description: 'Gathers knowledge from the digital world' },
    { id: 'developer', name: 'Developer', icon: Code, color: '#10B981', description: 'Writes code using Chain-of-Thought reasoning' },
    { id: 'test_designer', name: 'Test Designer', icon: TestTube, color: '#F59E0B', description: 'Creates automated tests without seeing code' },
    { id: 'executor', name: 'Executor', icon: Play, color: '#3B82F6', description: 'Runs code and captures real errors' },
    { id: 'debugger', name: 'Debugger', icon: Bug, color: '#EF4444', description: 'Fixes errors using learned patterns' },
    { id: 'verifier', name: 'Verifier', icon: CheckCircle, color: '#8B5CF6', description: 'Validates output matches user intent' }
];

const containerVariants = {
    hidden: { opacity: 0 },
    visible: {
        opacity: 1,
        transition: { staggerChildren: 0.1 }
    }
};

const itemVariants = {
    hidden: { opacity: 0, y: 20 },
    visible: { opacity: 1, y: 0 }
};

export default function Landing() {
    return (
        <div className="min-h-screen bg-[#030712] overflow-hidden">
            {/* Navigation */}
            <motion.nav 
                initial={{ y: -20, opacity: 0 }}
                animate={{ y: 0, opacity: 1 }}
                className="sticky top-0 z-50 border-b border-white/10 bg-black/50 backdrop-blur-xl"
            >
                <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <div className="flex items-center justify-between h-16">
                        <Link to="/" className="flex items-center gap-3" data-testid="logo">
                            <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-fuchsia-500 to-cyan-500 flex items-center justify-center">
                                <Zap className="w-5 h-5 text-white" />
                            </div>
                            <span className="text-xl font-bold text-white font-outfit tracking-tight">LITTLE HELPER AI</span>
                        </Link>
                        
                        <div className="flex items-center gap-4">
                            <Link to="/login" data-testid="login-nav">
                                <Button variant="ghost" className="text-gray-300 hover:text-white">
                                    Login
                                </Button>
                            </Link>
                            <Link to="/register" data-testid="get-started-nav">
                                <Button className="bg-fuchsia-500 hover:bg-fuchsia-600 glow-primary btn-glow">
                                    Get Started
                                </Button>
                            </Link>
                        </div>
                    </div>
                </div>
            </motion.nav>

            {/* Hero Section */}
            <section className="relative min-h-[calc(100vh-64px)] flex items-center grid-bg">
                {/* Animated background */}
                <div className="absolute inset-0 overflow-hidden">
                    <div className="absolute top-1/4 left-1/4 w-[500px] h-[500px] bg-fuchsia-500/20 rounded-full blur-[128px] animate-pulse" />
                    <div className="absolute bottom-1/4 right-1/4 w-[500px] h-[500px] bg-cyan-500/20 rounded-full blur-[128px] animate-pulse" style={{ animationDelay: '1s' }} />
                </div>
                
                <div className="relative z-10 max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-20">
                    <div className="grid lg:grid-cols-2 gap-12 items-center">
                        {/* Left content */}
                        <motion.div
                            initial={{ opacity: 0, x: -50 }}
                            animate={{ opacity: 1, x: 0 }}
                            transition={{ duration: 0.6 }}
                        >
                            <div className="inline-flex items-center gap-2 px-4 py-2 rounded-full bg-fuchsia-500/10 border border-fuchsia-500/20 mb-6">
                                <Sparkles className="w-4 h-4 text-fuchsia-400" />
                                <span className="text-sm text-fuchsia-300 font-medium">YOUR AI CODING ASSISTANT</span>
                            </div>
                            
                            <h1 className="text-4xl sm:text-5xl lg:text-6xl font-bold font-outfit mb-6 leading-tight">
                                <span className="text-white">Build Complete</span>
                                <br />
                                <span className="gradient-text">Projects with AI</span>
                            </h1>
                            
                            <p className="text-lg text-gray-400 mb-8 max-w-lg leading-relaxed">
                                Our smart AI pipeline plans, codes, tests, and debugs your projects automatically. 
                                Like having a senior development team that never sleeps.
                            </p>
                            
                            <div className="flex flex-wrap gap-4">
                                <Link to="/register" data-testid="start-building-btn">
                                    <Button size="lg" className="bg-fuchsia-500 hover:bg-fuchsia-600 glow-primary btn-glow h-14 px-8 text-lg">
                                        Start Building
                                        <ArrowRight className="ml-2 w-5 h-5" />
                                    </Button>
                                </Link>
                                <Button 
                                    size="lg" 
                                    variant="outline" 
                                    className="border-white/20 hover:bg-white/5 h-14 px-8 text-lg"
                                    data-testid="watch-demo-btn"
                                >
                                    <Play className="mr-2 w-5 h-5" />
                                    Watch Demo
                                </Button>
                            </div>
                        </motion.div>
                        
                        {/* Right content - Preview */}
                        <motion.div
                            initial={{ opacity: 0, x: 50 }}
                            animate={{ opacity: 1, x: 0 }}
                            transition={{ duration: 0.6, delay: 0.2 }}
                            className="relative"
                        >
                            <div className="glass-card rounded-2xl p-1 overflow-hidden">
                                <div className="bg-[#0B0F19] rounded-xl overflow-hidden">
                                    {/* Preview header */}
                                    <div className="flex items-center gap-2 px-4 py-3 border-b border-white/10">
                                        <div className="flex gap-1.5">
                                            <div className="w-3 h-3 rounded-full bg-red-500" />
                                            <div className="w-3 h-3 rounded-full bg-yellow-500" />
                                            <div className="w-3 h-3 rounded-full bg-green-500" />
                                        </div>
                                        <span className="text-sm text-gray-400 ml-2 font-mono">workspace</span>
                                    </div>
                                    
                                    {/* Preview content */}
                                    <div className="relative aspect-video">
                                        <img 
                                            src="https://images.unsplash.com/photo-1678845536613-5cf0ec5245cd?crop=entropy&cs=srgb&fm=jpg&q=85&w=800"
                                            alt="AI Network Visualization"
                                            className="w-full h-full object-cover opacity-60"
                                        />
                                        <div className="absolute inset-0 bg-gradient-to-t from-[#0B0F19] via-transparent to-transparent" />
                                        
                                        {/* Pipeline indicator */}
                                        <div className="absolute bottom-4 left-4 right-4">
                                            <div className="glass rounded-lg p-3">
                                                <div className="flex items-center gap-2 mb-2">
                                                    <div className="w-2 h-2 rounded-full bg-green-400 pulse-glow" style={{ color: '#10B981' }} />
                                                    <span className="text-sm font-medium text-green-400">Smart Pipeline Active</span>
                                                </div>
                                                <div className="flex gap-2">
                                                    {agents.slice(0, 6).map((agent) => (
                                                        <div 
                                                            key={agent.id}
                                                            className="w-8 h-8 rounded-lg flex items-center justify-center"
                                                            style={{ backgroundColor: `${agent.color}20` }}
                                                        >
                                                            <agent.icon size={16} style={{ color: agent.color }} />
                                                        </div>
                                                    ))}
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            {/* Floating badge */}
                            <motion.div 
                                className="absolute -top-4 -right-4 glass-card rounded-xl px-4 py-2"
                                animate={{ y: [0, -10, 0] }}
                                transition={{ duration: 3, repeat: Infinity }}
                            >
                                <div className="flex items-center gap-2">
                                    <Globe className="w-4 h-4 text-cyan-400" />
                                    <span className="text-sm font-medium">6+ Languages</span>
                                </div>
                            </motion.div>
                        </motion.div>
                    </div>
                </div>
            </section>

            {/* Agents Section */}
            <section className="relative py-24 border-t border-white/5">
                <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <motion.div
                        initial={{ opacity: 0, y: 20 }}
                        whileInView={{ opacity: 1, y: 0 }}
                        viewport={{ once: true }}
                        className="text-center mb-16"
                    >
                        <span className="text-sm font-medium text-fuchsia-400 tracking-wider uppercase">
                            THE SMART PIPELINE
                        </span>
                        <h2 className="text-3xl sm:text-4xl lg:text-5xl font-bold font-outfit mt-4 mb-6">
                            7 Intelligent AI Agents
                        </h2>
                        <p className="text-gray-400 max-w-2xl mx-auto text-lg">
                            Based on cutting-edge research with multi-path planning, semantic verification, 
                            and long-horizon memory. Each agent has one job and does it exceptionally well.
                        </p>
                    </motion.div>
                    
                    <motion.div
                        variants={containerVariants}
                        initial="hidden"
                        whileInView="visible"
                        viewport={{ once: true }}
                        className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-7 gap-4"
                    >
                        {agents.map((agent, index) => (
                            <motion.div
                                key={agent.id}
                                variants={itemVariants}
                                whileHover={{ scale: 1.05, y: -5 }}
                                className="glass-card rounded-xl p-4 text-center group cursor-pointer"
                                data-testid={`agent-${agent.id}`}
                            >
                                <div 
                                    className="w-12 h-12 rounded-xl mx-auto mb-3 flex items-center justify-center transition-all duration-300 group-hover:scale-110"
                                    style={{ backgroundColor: `${agent.color}20` }}
                                >
                                    <agent.icon 
                                        size={24} 
                                        style={{ color: agent.color }}
                                        className="transition-transform duration-300 group-hover:scale-110"
                                    />
                                </div>
                                <h3 className="font-semibold text-white mb-1">{agent.name}</h3>
                                <p className="text-xs text-gray-400 leading-relaxed">{agent.description}</p>
                            </motion.div>
                        ))}
                    </motion.div>
                    
                    {/* Pipeline flow */}
                    <motion.div 
                        initial={{ opacity: 0 }}
                        whileInView={{ opacity: 1 }}
                        viewport={{ once: true }}
                        className="mt-12 flex items-center justify-center flex-wrap gap-2 text-sm"
                    >
                        <span className="text-gray-500">Your Request</span>
                        {agents.map((agent, i) => (
                            <span key={agent.id} className="flex items-center gap-2">
                                <ArrowRight className="w-4 h-4 text-gray-600" />
                                <span style={{ color: agent.color }}>{agent.name}</span>
                            </span>
                        ))}
                        <ArrowRight className="w-4 h-4 text-gray-600" />
                        <span className="text-green-400 font-medium">Working Code</span>
                    </motion.div>
                </div>
            </section>

            {/* CTA Section */}
            <section className="relative py-24 border-t border-white/5">
                <div className="absolute inset-0 bg-gradient-to-t from-fuchsia-500/5 to-transparent" />
                <div className="relative max-w-4xl mx-auto px-4 text-center">
                    <motion.div
                        initial={{ opacity: 0, y: 20 }}
                        whileInView={{ opacity: 1, y: 0 }}
                        viewport={{ once: true }}
                    >
                        <h2 className="text-3xl sm:text-4xl font-bold font-outfit mb-6">
                            Ready to build something amazing?
                        </h2>
                        <p className="text-gray-400 mb-8 text-lg">
                            Start with 100 free credits. No credit card required.
                        </p>
                        <Link to="/register" data-testid="cta-get-started">
                            <Button size="lg" className="bg-fuchsia-500 hover:bg-fuchsia-600 glow-primary btn-glow h-14 px-12 text-lg">
                                Get Started Free
                                <ArrowRight className="ml-2 w-5 h-5" />
                            </Button>
                        </Link>
                    </motion.div>
                </div>
            </section>

            {/* Footer */}
            <footer className="border-t border-white/5 py-8">
                <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <div className="flex flex-col md:flex-row items-center justify-between gap-4">
                        <div className="flex items-center gap-3">
                            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-fuchsia-500 to-cyan-500 flex items-center justify-center">
                                <Zap className="w-4 h-4 text-white" />
                            </div>
                            <span className="font-semibold">LittleHelper AI</span>
                        </div>
                        <p className="text-gray-500 text-sm">
                            © 2024 LittleHelper AI. All rights reserved.
                        </p>
                    </div>
                </div>
            </footer>
        </div>
    );
}
