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

// Layout with Global Assistant
const LayoutWithAssistant = ({ children }) => {
    return (
        <>
            {children}
            <GlobalAssistant />
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
