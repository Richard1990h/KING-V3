import { useState, useEffect } from 'react';
import { Link, useSearchParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { creditsAPI, plansAPI } from '../lib/api';
import { formatCredits, formatCurrency } from '../lib/utils';
import { Button } from '../components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs';
import { 
    Zap, ArrowLeft, Check, CreditCard, Sparkles, Star, Crown,
    Calendar, Package, Infinity, Users, Key, Clock
} from 'lucide-react';

const PACKAGE_ICONS = {
    starter: Star,
    pro: Sparkles,
    enterprise: Crown
};

const PLAN_ICONS = {
    free: Star,
    starter: Sparkles,
    pro: Crown,
    openai: Key,
    enterprise: Infinity
};

export default function Credits() {
    const { user, refreshUser } = useAuth();
    const { t } = useI18n();
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();
    const [packages, setPackages] = useState({});
    const [plans, setPlans] = useState([]);
    const [loading, setLoading] = useState(true);
    const [purchasing, setPurchasing] = useState(null);
    const [subscribing, setSubscribing] = useState(null);
    const [checkingPayment, setCheckingPayment] = useState(false);
    const [paymentStatus, setPaymentStatus] = useState(null);
    const [activeTab, setActiveTab] = useState('plans');

    useEffect(() => {
        loadData();
        
        // Check for payment callback
        const sessionId = searchParams.get('session_id');
        if (sessionId) {
            checkPaymentStatus(sessionId);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const loadData = async () => {
        try {
            const [packagesRes, plansRes] = await Promise.all([
                creditsAPI.getPackages(),
                plansAPI.getAll()
            ]);
            setPackages(packagesRes.data);
            setPlans(plansRes.data.filter(p => p.is_active));
        } catch (error) {
            console.error('Failed to load data:', error);
        } finally {
            setLoading(false);
        }
    };

    const checkPaymentStatus = async (sessionId) => {
        setCheckingPayment(true);
        let attempts = 0;
        const maxAttempts = 5;
        
        const poll = async () => {
            try {
                const res = await creditsAPI.checkStatus(sessionId);
                if (res.data.payment_status === 'paid') {
                    setPaymentStatus({ success: true, credits: res.data.credits_added });
                    await refreshUser();
                    return;
                } else if (res.data.status === 'expired') {
                    setPaymentStatus({ success: false, message: 'Payment session expired' });
                    return;
                }
                
                attempts++;
                if (attempts < maxAttempts) {
                    setTimeout(poll, 2000);
                } else {
                    setPaymentStatus({ success: false, message: 'Payment status check timed out' });
                }
            } catch (error) {
                setPaymentStatus({ success: false, message: 'Failed to verify payment' });
            }
        };
        
        await poll();
        setCheckingPayment(false);
    };

    const purchasePackage = async (packageId) => {
        setPurchasing(packageId);
        try {
            const originUrl = window.location.origin;
            const res = await creditsAPI.purchase(packageId, originUrl);
            window.location.href = res.data.url;
        } catch (error) {
            console.error('Failed to initiate purchase:', error);
            setPurchasing(null);
        }
    };

    const subscribeToPlan = async (planId) => {
        if (planId === 'free') {
            // Free plan subscription
            setSubscribing(planId);
            try {
                await plansAPI.subscribe(planId);
                await refreshUser();
                setPaymentStatus({ success: true, message: 'Successfully switched to Free plan!' });
            } catch (error) {
                console.error('Failed to subscribe:', error);
                setPaymentStatus({ success: false, message: 'Failed to subscribe to plan' });
            } finally {
                setSubscribing(null);
            }
            return;
        }
        
        setSubscribing(planId);
        try {
            const originUrl = window.location.origin;
            const res = await plansAPI.subscribe(planId, originUrl);
            if (res.data.checkout_url) {
                window.location.href = res.data.checkout_url;
            }
        } catch (error) {
            console.error('Failed to initiate subscription:', error);
            setSubscribing(null);
        }
    };

    // Sort plans by price
    const sortedPlans = [...plans].sort((a, b) => a.price_monthly - b.price_monthly);

    return (
        <div className="min-h-screen bg-[var(--background-color,#030712)] grid-bg">
            {/* Background effects */}
            <div className="absolute inset-0 overflow-hidden">
                <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-fuchsia-500/10 rounded-full blur-3xl" />
                <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-cyan-500/10 rounded-full blur-3xl" />
            </div>

            {/* Header */}
            <header className="relative z-10 sticky top-0 border-b border-white/10 bg-black/50 backdrop-blur-xl">
                <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
                    <div className="flex items-center justify-between h-16">
                        <Link to="/dashboard" className="flex items-center gap-2 text-gray-400 hover:text-white transition-colors" data-testid="back-to-dashboard">
                            <ArrowLeft size={20} />
                            <span>{t('nav_back_dashboard')}</span>
                        </Link>
                        
                        <div className="flex items-center gap-4">
                            {user?.plan && user.plan !== 'free' && (
                                <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-cyan-500/10 border border-cyan-500/30">
                                    <Crown className="w-4 h-4 text-cyan-400" />
                                    <span className="text-cyan-300 text-sm font-medium capitalize">{user.plan} {t('common_current_plan')}</span>
                                </div>
                            )}
                            <div className="flex items-center gap-2 px-4 py-2 rounded-lg bg-fuchsia-500/10 border border-fuchsia-500/30">
                                <Zap className="w-5 h-5 text-fuchsia-400" />
                                <span className="text-fuchsia-300 font-semibold">{formatCredits(user?.credits || 0)}</span>
                                <span className="text-gray-500">{t('nav_credits').toLowerCase()}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </header>

            <main className="relative z-10 max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
                {/* Payment status message */}
                {checkingPayment && (
                    <motion.div
                        initial={{ opacity: 0, y: -20 }}
                        animate={{ opacity: 1, y: 0 }}
                        className="mb-8 p-4 rounded-xl bg-fuchsia-500/10 border border-fuchsia-500/30 text-center"
                    >
                        <div className="flex items-center justify-center gap-3">
                            <div className="w-5 h-5 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
                            <span className="text-fuchsia-300">Verifying payment...</span>
                        </div>
                    </motion.div>
                )}

                {paymentStatus && (
                    <motion.div
                        initial={{ opacity: 0, y: -20 }}
                        animate={{ opacity: 1, y: 0 }}
                        className={`mb-8 p-4 rounded-xl border text-center ${
                            paymentStatus.success 
                                ? 'bg-green-500/10 border-green-500/30' 
                                : 'bg-red-500/10 border-red-500/30'
                        }`}
                    >
                        {paymentStatus.success ? (
                            <div className="flex items-center justify-center gap-3">
                                <Check className="w-5 h-5 text-green-400" />
                                <span className="text-green-300">
                                    {paymentStatus.credits 
                                        ? `Payment successful! ${paymentStatus.credits} credits added to your account.`
                                        : paymentStatus.message || 'Success!'
                                    }
                                </span>
                            </div>
                        ) : (
                            <span className="text-red-300">{paymentStatus.message}</span>
                        )}
                    </motion.div>
                )}

                {/* Title */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    className="text-center mb-8"
                >
                    <h1 className="text-4xl font-bold font-outfit mb-4">{t('credits_title')}</h1>
                    <p className="text-gray-400 text-lg">{t('credits_subtitle')}</p>
                </motion.div>

                {/* Tabs */}
                <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
                    <TabsList className="grid w-full max-w-md mx-auto grid-cols-2 mb-8 bg-white/5 p-1 rounded-lg">
                        <TabsTrigger 
                            value="plans" 
                            className="data-[state=active]:bg-fuchsia-500 data-[state=active]:text-white rounded-md transition-all"
                            data-testid="plans-tab"
                        >
                            <Calendar size={16} className="mr-2" />
                            {t('credits_monthly_plans')}
                        </TabsTrigger>
                        <TabsTrigger 
                            value="addons"
                            className="data-[state=active]:bg-fuchsia-500 data-[state=active]:text-white rounded-md transition-all"
                            data-testid="addons-tab"
                        >
                            <Package size={16} className="mr-2" />
                            {t('credits_addon_credits')}
                        </TabsTrigger>
                    </TabsList>

                    {/* Monthly Plans Tab */}
                    <TabsContent value="plans">
                        {loading ? (
                            <div className="flex justify-center py-12">
                                <div className="w-8 h-8 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
                            </div>
                        ) : (
                            <>
                                <motion.div
                                    initial={{ opacity: 0 }}
                                    animate={{ opacity: 1 }}
                                    className="grid md:grid-cols-3 lg:grid-cols-5 gap-4"
                                >
                                    {sortedPlans.map((plan, index) => {
                                        const Icon = PLAN_ICONS[plan.id] || Star;
                                        const isCurrentPlan = user?.plan === plan.id;
                                        const isPopular = plan.id === 'pro';
                                        
                                        return (
                                            <motion.div
                                                key={plan.id}
                                                initial={{ opacity: 0, y: 20 }}
                                                animate={{ opacity: 1, y: 0 }}
                                                transition={{ delay: index * 0.1 }}
                                                className={`relative glass-card rounded-2xl p-5 flex flex-col ${
                                                    isPopular ? 'border-fuchsia-500/50 ring-2 ring-fuchsia-500/20' : ''
                                                } ${isCurrentPlan ? 'border-green-500/50 ring-2 ring-green-500/20' : ''}`}
                                                data-testid={`plan-${plan.id}`}
                                            >
                                                {isPopular && !isCurrentPlan && (
                                                    <div className="absolute -top-3 left-1/2 -translate-x-1/2 px-3 py-1 bg-fuchsia-500 rounded-full text-xs font-semibold whitespace-nowrap">
                                                        MOST POPULAR
                                                    </div>
                                                )}
                                                {isCurrentPlan && (
                                                    <div className="absolute -top-3 left-1/2 -translate-x-1/2 px-3 py-1 bg-green-500 rounded-full text-xs font-semibold whitespace-nowrap">
                                                        CURRENT PLAN
                                                    </div>
                                                )}
                                                
                                                <div className="flex items-center gap-3 mb-4">
                                                    <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
                                                        isPopular ? 'bg-fuchsia-500/20' : isCurrentPlan ? 'bg-green-500/20' : 'bg-white/5'
                                                    }`}>
                                                        <Icon className={`w-5 h-5 ${
                                                            isPopular ? 'text-fuchsia-400' : isCurrentPlan ? 'text-green-400' : 'text-gray-400'
                                                        }`} />
                                                    </div>
                                                    <h3 className="font-semibold">{plan.name}</h3>
                                                </div>
                                                
                                                <div className="mb-4">
                                                    <span className="text-3xl font-bold">{formatCurrency(plan.price_monthly)}</span>
                                                    <span className="text-gray-400 text-sm">/mo</span>
                                                </div>
                                                
                                                <ul className="space-y-2 mb-4 flex-grow">
                                                    <li className="flex items-center gap-2 text-xs text-gray-300">
                                                        <Zap size={12} className="text-fuchsia-400 flex-shrink-0" />
                                                        <span>{plan.daily_credits} credits/day</span>
                                                    </li>
                                                    <li className="flex items-center gap-2 text-xs text-gray-300">
                                                        <Users size={12} className="text-cyan-400 flex-shrink-0" />
                                                        <span>{plan.max_concurrent_workspaces} workspace{plan.max_concurrent_workspaces > 1 ? 's' : ''}</span>
                                                    </li>
                                                    {plan.allows_own_api_keys && (
                                                        <li className="flex items-center gap-2 text-xs text-gray-300">
                                                            <Key size={12} className="text-green-400 flex-shrink-0" />
                                                            <span>Own API keys</span>
                                                        </li>
                                                    )}
                                                    {plan.features && plan.features.slice(0, 2).map((feature, i) => (
                                                        <li key={i} className="flex items-center gap-2 text-xs text-gray-300">
                                                            <Check size={12} className="text-green-400 flex-shrink-0" />
                                                            <span className="truncate">{feature}</span>
                                                        </li>
                                                    ))}
                                                </ul>
                                                
                                                <Button
                                                    onClick={() => subscribeToPlan(plan.id)}
                                                    disabled={isCurrentPlan || subscribing === plan.id}
                                                    className={`w-full text-sm ${
                                                        isCurrentPlan
                                                            ? 'bg-green-500/20 text-green-300 cursor-not-allowed'
                                                            : isPopular 
                                                                ? 'bg-fuchsia-500 hover:bg-fuchsia-600 glow-primary' 
                                                                : 'bg-white/10 hover:bg-white/20'
                                                    }`}
                                                    data-testid={`subscribe-${plan.id}`}
                                                >
                                                    {subscribing === plan.id ? (
                                                        <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                                                    ) : isCurrentPlan ? (
                                                        'Current Plan'
                                                    ) : plan.price_monthly === 0 ? (
                                                        'Switch to Free'
                                                    ) : (
                                                        'Subscribe'
                                                    )}
                                                </Button>
                                            </motion.div>
                                        );
                                    })}
                                </motion.div>

                                {/* Plan Benefits Info */}
                                <motion.div
                                    initial={{ opacity: 0, y: 20 }}
                                    animate={{ opacity: 1, y: 0 }}
                                    transition={{ delay: 0.3 }}
                                    className="mt-8 p-6 glass-card rounded-xl"
                                >
                                    <h3 className="font-semibold mb-4 flex items-center gap-2">
                                        <Clock className="w-5 h-5 text-fuchsia-400" />
                                        How Monthly Plans Work
                                    </h3>
                                    <div className="grid md:grid-cols-3 gap-4 text-sm text-gray-400">
                                        <div>
                                            <p className="font-medium text-white mb-1">Daily Credits</p>
                                            <p>Credits refresh daily based on your plan. Unused credits roll over up to a maximum balance.</p>
                                        </div>
                                        <div>
                                            <p className="font-medium text-white mb-1">Concurrent Workspaces</p>
                                            <p>Higher plans allow more projects to be built simultaneously by the AI agents.</p>
                                        </div>
                                        <div>
                                            <p className="font-medium text-white mb-1">Own API Keys</p>
                                            <p>Pro+ plans let you use your own OpenAI/Anthropic keys, bypassing credit usage entirely.</p>
                                        </div>
                                    </div>
                                </motion.div>
                            </>
                        )}
                    </TabsContent>

                    {/* Add-on Credits Tab */}
                    <TabsContent value="addons">
                        {loading ? (
                            <div className="flex justify-center py-12">
                                <div className="w-8 h-8 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
                            </div>
                        ) : (
                            <>
                                <motion.div
                                    initial={{ opacity: 0, y: 20 }}
                                    animate={{ opacity: 1, y: 0 }}
                                    className="text-center mb-8"
                                >
                                    <p className="text-gray-400">
                                        Need more credits? Purchase add-on packs anytime to boost your balance.
                                        <br />
                                        <span className="text-fuchsia-400">These are one-time purchases that never expire.</span>
                                    </p>
                                </motion.div>

                                <motion.div
                                    initial={{ opacity: 0 }}
                                    animate={{ opacity: 1 }}
                                    className="grid md:grid-cols-3 gap-6 max-w-4xl mx-auto"
                                >
                                    {Object.entries(packages).map(([id, pkg], index) => {
                                        const Icon = PACKAGE_ICONS[id] || Star;
                                        const isPopular = id === 'pro';
                                        
                                        return (
                                            <motion.div
                                                key={id}
                                                initial={{ opacity: 0, y: 20 }}
                                                animate={{ opacity: 1, y: 0 }}
                                                transition={{ delay: index * 0.1 }}
                                                className={`relative glass-card rounded-2xl p-6 ${
                                                    isPopular ? 'border-fuchsia-500/50 ring-2 ring-fuchsia-500/20' : ''
                                                }`}
                                                data-testid={`package-${id}`}
                                            >
                                                {isPopular && (
                                                    <div className="absolute -top-3 left-1/2 -translate-x-1/2 px-3 py-1 bg-fuchsia-500 rounded-full text-xs font-semibold">
                                                        BEST VALUE
                                                    </div>
                                                )}
                                                
                                                <div className="flex items-center gap-3 mb-4">
                                                    <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${
                                                        isPopular ? 'bg-fuchsia-500/20' : 'bg-white/5'
                                                    }`}>
                                                        <Icon className={`w-6 h-6 ${isPopular ? 'text-fuchsia-400' : 'text-gray-400'}`} />
                                                    </div>
                                                    <div>
                                                        <h3 className="font-semibold text-lg">{pkg.name}</h3>
                                                        <p className="text-sm text-gray-400">{pkg.credits} credits</p>
                                                    </div>
                                                </div>
                                                
                                                <div className="mb-6">
                                                    <span className="text-4xl font-bold">{formatCurrency(pkg.price)}</span>
                                                    <span className="text-gray-400 ml-2">USD</span>
                                                    <p className="text-xs text-gray-500 mt-1">
                                                        ${(pkg.price / pkg.credits * 100).toFixed(1)}¢ per credit
                                                    </p>
                                                </div>
                                                
                                                <ul className="space-y-2 mb-6">
                                                    <li className="flex items-center gap-2 text-sm text-gray-300">
                                                        <Check size={16} className="text-green-400" />
                                                        {pkg.credits} AI credits
                                                    </li>
                                                    <li className="flex items-center gap-2 text-sm text-gray-300">
                                                        <Check size={16} className="text-green-400" />
                                                        Never expires
                                                    </li>
                                                    <li className="flex items-center gap-2 text-sm text-gray-300">
                                                        <Check size={16} className="text-green-400" />
                                                        Instant delivery
                                                    </li>
                                                    <li className="flex items-center gap-2 text-sm text-gray-300">
                                                        <Check size={16} className="text-green-400" />
                                                        Works with any plan
                                                    </li>
                                                </ul>
                                                
                                                <Button
                                                    onClick={() => purchasePackage(id)}
                                                    disabled={purchasing === id}
                                                    className={`w-full ${
                                                        isPopular 
                                                            ? 'bg-fuchsia-500 hover:bg-fuchsia-600 glow-primary' 
                                                            : 'bg-white/10 hover:bg-white/20'
                                                    }`}
                                                    data-testid={`buy-${id}`}
                                                >
                                                    {purchasing === id ? (
                                                        <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                                                    ) : (
                                                        <>
                                                            <CreditCard size={18} className="mr-2" />
                                                            Buy Now
                                                        </>
                                                    )}
                                                </Button>
                                            </motion.div>
                                        );
                                    })}
                                </motion.div>
                            </>
                        )}
                    </TabsContent>
                </Tabs>

                {/* FAQ */}
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.3 }}
                    className="mt-16 text-center"
                >
                    <h2 className="text-2xl font-bold mb-6">Frequently Asked Questions</h2>
                    <div className="max-w-2xl mx-auto space-y-4 text-left">
                        <div className="glass-card rounded-xl p-4 border-red-500/30 bg-red-500/5">
                            <h3 className="font-semibold mb-2 text-red-400">⚠️ Refund Policy - Please Read First</h3>
                            <p className="text-sm text-gray-400">
                                <strong className="text-red-300">Credits and subscriptions are non-refundable and non-transferable. All sales are final.</strong>{' '}
                                Credits are a consumable digital product and once purchased, they are immediately available for use. 
                                We encourage you to start with a smaller package or the free plan to ensure our platform meets your needs.
                            </p>
                        </div>
                        <div className="glass-card rounded-xl p-4">
                            <h3 className="font-semibold mb-2">What's the difference between plans and add-ons?</h3>
                            <p className="text-sm text-gray-400">Monthly plans give you daily credits that refresh each day, plus features like more workspaces and API key support. Add-on credits are one-time purchases that top up your balance and never expire.</p>
                        </div>
                        <div className="glass-card rounded-xl p-4">
                            <h3 className="font-semibold mb-2">What are credits used for?</h3>
                            <p className="text-sm text-gray-400">Credits are used for AI interactions, including chat messages, code generation, and project builds. Each AI operation consumes credits based on the complexity of the task.</p>
                        </div>
                        <div className="glass-card rounded-xl p-4">
                            <h3 className="font-semibold mb-2">Do add-on credits expire?</h3>
                            <p className="text-sm text-gray-400">No, add-on credits never expire. Use them whenever you need them. Daily credits from plans also roll over up to a maximum balance.</p>
                        </div>
                        <div className="glass-card rounded-xl p-4">
                            <h3 className="font-semibold mb-2">Can I use my own API key?</h3>
                            <p className="text-sm text-gray-400">Yes! If you configure your own OpenAI, Anthropic, or other API keys in Profile settings (requires Pro plan or higher), you won't be charged any credits. You'll only pay your API provider directly.</p>
                        </div>
                        <div className="glass-card rounded-xl p-4">
                            <h3 className="font-semibold mb-2">Can I change my plan anytime?</h3>
                            <p className="text-sm text-gray-400">Yes! You can upgrade or downgrade your plan at any time. When upgrading, you'll get immediate access to higher limits. When downgrading, changes take effect at your next billing cycle.</p>
                        </div>
                    </div>
                </motion.div>
            </main>
        </div>
    );
}
