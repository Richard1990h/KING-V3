import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AuthProvider, useAuth } from "./lib/auth";
import { ThemeProvider } from "./lib/theme";
import { I18nProvider } from "./lib/i18n";
import { Toaster } from "./components/ui/sonner";
import GlobalAssistant from "./components/GlobalAssistant";
import { FriendsSidebar } from "./components/FriendsSidebar";
import { useState } from "react";

// Pages
import Landing from "./pages/Landing";
import Login from "./pages/auth/Login";
import Register from "./pages/auth/Register";
import Dashboard from "./pages/Dashboard";
import Workspace from "./pages/Workspace";
import Credits from "./pages/Credits";
import Admin from "./pages/Admin";
import Profile from "./pages/Profile";

// Protected Route Component
const ProtectedRoute = ({ children }) => {
    const { user, loading } = useAuth();
    
    if (loading) {
        return (
            <div className="min-h-screen bg-[#030712] flex items-center justify-center">
                <div className="w-8 h-8 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
            </div>
        );
    }
    
    if (!user) {
        return <Navigate to="/login" replace />;
    }
    
    return children;
};

// Public Route (redirect to dashboard if logged in)
const PublicRoute = ({ children }) => {
    const { user, loading } = useAuth();
    
    if (loading) {
        return (
            <div className="min-h-screen bg-[#030712] flex items-center justify-center">
                <div className="w-8 h-8 border-2 border-fuchsia-500/30 border-t-fuchsia-500 rounded-full animate-spin" />
            </div>
        );
    }
    
    if (user) {
        return <Navigate to="/dashboard" replace />;
    }
    
    return children;
};

// Layout with Global Assistant and Friends Sidebar
const LayoutWithAssistant = ({ children }) => {
    const [friendsSidebarOpen, setFriendsSidebarOpen] = useState(false);
    const { user } = useAuth();
    const { unreadDMs, pendingRequests } = useNotifications(user?.id);
    const totalNotifications = (unreadDMs || 0) + (pendingRequests || 0);
    
    return (
        <>
            {children}
            <GlobalAssistant />
            <FriendsSidebar 
                isOpen={friendsSidebarOpen} 
                onClose={() => setFriendsSidebarOpen(false)} 
            />
            {/* Friends toggle button with notification badge */}
            <button
                onClick={() => setFriendsSidebarOpen(!friendsSidebarOpen)}
                className="fixed left-4 bottom-4 w-12 h-12 rounded-full bg-gradient-to-r from-purple-500 to-indigo-500 shadow-lg flex items-center justify-center hover:scale-110 transition-transform z-[50]"
                title="Friends & Messages"
                data-testid="friends-toggle-btn"
            >
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/>
                    <circle cx="9" cy="7" r="4"/>
                    <path d="M22 21v-2a4 4 0 0 0-3-3.87"/>
                    <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
                </svg>
                {/* Notification badge */}
                {totalNotifications > 0 && (
                    <span className="absolute -top-1 -right-1 w-5 h-5 bg-red-500 rounded-full flex items-center justify-center text-xs font-bold text-white animate-pulse">
                        {totalNotifications > 9 ? '9+' : totalNotifications}
                    </span>
                )}
            </button>
        </>
    );
};

function AppRoutes() {
    return (
        <Routes>
            {/* Public routes */}
            <Route path="/" element={<Landing />} />
            <Route path="/login" element={
                <PublicRoute>
                    <Login />
                </PublicRoute>
            } />
            <Route path="/register" element={
                <PublicRoute>
                    <Register />
                </PublicRoute>
            } />
            
            {/* Protected routes with Global Assistant */}
            <Route path="/dashboard" element={
                <ProtectedRoute>
                    <LayoutWithAssistant>
                        <Dashboard />
                    </LayoutWithAssistant>
                </ProtectedRoute>
            } />
            <Route path="/workspace/:projectId" element={
                <ProtectedRoute>
                    <LayoutWithAssistant>
                        <Workspace />
                    </LayoutWithAssistant>
                </ProtectedRoute>
            } />
            <Route path="/credits" element={
                <ProtectedRoute>
                    <LayoutWithAssistant>
                        <Credits />
                    </LayoutWithAssistant>
                </ProtectedRoute>
            } />
            <Route path="/credits/success" element={
                <ProtectedRoute>
                    <LayoutWithAssistant>
                        <Credits />
                    </LayoutWithAssistant>
                </ProtectedRoute>
            } />
            <Route path="/admin" element={
                <ProtectedRoute>
                    <LayoutWithAssistant>
                        <Admin />
                    </LayoutWithAssistant>
                </ProtectedRoute>
            } />
            <Route path="/settings" element={
                <ProtectedRoute>
                    <LayoutWithAssistant>
                        <Profile />
                    </LayoutWithAssistant>
                </ProtectedRoute>
            } />
            <Route path="/profile" element={
                <ProtectedRoute>
                    <LayoutWithAssistant>
                        <Profile />
                    </LayoutWithAssistant>
                </ProtectedRoute>
            } />
            
            {/* Catch all */}
            <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
    );
}

function App() {
    return (
        <BrowserRouter>
            <AuthProvider>
                <ThemeProvider>
                    <I18nProvider>
                        <AppRoutes />
                        <Toaster />
                    </I18nProvider>
                </ThemeProvider>
            </AuthProvider>
        </BrowserRouter>
    );
}

export default App;
