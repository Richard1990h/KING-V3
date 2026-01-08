import { createContext, useContext, useState, useEffect } from 'react';
import { profileAPI } from './api';

const defaultTheme = {
    primary_color: "#d946ef",      // fuchsia-500
    secondary_color: "#06b6d4",    // cyan-500
    background_color: "#030712",   // gray-950
    card_color: "#0B0F19",
    text_color: "#ffffff",
    hover_color: "#a855f7",        // purple-500
    credits_color: "#d946ef",      // fuchsia-500
    background_image: null
};

const ThemeContext = createContext(null);

// Get initial theme from localStorage synchronously
const getInitialTheme = () => {
    if (typeof window !== 'undefined') {
        const savedTheme = localStorage.getItem('userTheme');
        if (savedTheme) {
            try {
                return { ...defaultTheme, ...JSON.parse(savedTheme) };
            } catch (e) {
                console.error('Failed to parse saved theme');
            }
        }
    }
    return defaultTheme;
};

export const ThemeProvider = ({ children }) => {
    const [theme, setTheme] = useState(getInitialTheme);
    const [loading, setLoading] = useState(false);

    // Apply CSS variables whenever theme changes
    useEffect(() => {
        const root = document.documentElement;
        root.style.setProperty('--primary-color', theme.primary_color);
        root.style.setProperty('--secondary-color', theme.secondary_color);
        root.style.setProperty('--background-color', theme.background_color);
        root.style.setProperty('--card-color', theme.card_color);
        root.style.setProperty('--text-color', theme.text_color);
        root.style.setProperty('--hover-color', theme.hover_color);
        root.style.setProperty('--credits-color', theme.credits_color);
        
        // Apply background
        if (theme.background_image) {
            document.body.style.backgroundImage = `url(${theme.background_image})`;
            document.body.style.backgroundSize = 'cover';
            document.body.style.backgroundAttachment = 'fixed';
        } else {
            document.body.style.backgroundImage = 'none';
            document.body.style.backgroundColor = theme.background_color;
        }
    }, [theme]);

    const updateTheme = async (newTheme) => {
        const merged = { ...theme, ...newTheme };
        setTheme(merged);
        localStorage.setItem('userTheme', JSON.stringify(merged));
        
        // Save to backend
        try {
            await profileAPI.updateTheme(merged);
        } catch (error) {
            console.error('Failed to save theme to server:', error);
        }
    };

    const loadUserTheme = async () => {
        try {
            const res = await profileAPI.get();
            if (res.data.theme) {
                const userTheme = { ...defaultTheme, ...res.data.theme };
                setTheme(userTheme);
                localStorage.setItem('userTheme', JSON.stringify(userTheme));
            }
        } catch (error) {
            console.error('Failed to load user theme:', error);
        }
    };

    const resetTheme = async () => {
        setTheme(defaultTheme);
        localStorage.setItem('userTheme', JSON.stringify(defaultTheme));
        try {
            await profileAPI.updateTheme(defaultTheme);
        } catch (error) {
            console.error('Failed to reset theme:', error);
        }
    };

    return (
        <ThemeContext.Provider value={{ 
            theme, 
            updateTheme, 
            loadUserTheme, 
            resetTheme,
            loading,
            defaultTheme 
        }}>
            {children}
        </ThemeContext.Provider>
    );
};

export const useTheme = () => {
    const context = useContext(ThemeContext);
    if (!context) {
        throw new Error('useTheme must be used within a ThemeProvider');
    }
    return context;
};

export default ThemeContext;
