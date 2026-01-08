import axios from 'axios';

const BACKEND_URL = process.env.REACT_APP_BACKEND_URL;
const API_BASE = `${BACKEND_URL}/api`;

// Create axios instance
const api = axios.create({
    baseURL: API_BASE,
    headers: {
        'Content-Type': 'application/json'
    }
});

// Add auth token to requests
api.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

// Handle auth errors
api.interceptors.response.use(
    (response) => response,
    (error) => {
        if (error.response?.status === 401) {
            localStorage.removeItem('token');
            localStorage.removeItem('user');
            window.location.href = '/login';
        }
        return Promise.reject(error);
    }
);

// Auth API
export const authAPI = {
    register: (data) => api.post('/auth/register', data),
    login: (data) => api.post('/auth/login', data),
    getMe: () => api.get('/auth/me'),
    updateLanguage: (language) => api.put('/auth/language', { language })
};

// Projects API
export const projectsAPI = {
    getAll: () => api.get('/projects'),
    get: (id) => api.get(`/projects/${id}`),
    create: (data) => api.post('/projects', data),
    update: (id, data) => api.put(`/projects/${id}`, data),
    delete: (id) => api.delete(`/projects/${id}`),
    build: (id) => api.post(`/projects/${id}/build`),
    run: (id) => api.post(`/projects/${id}/run`),
    getRuns: (id) => api.get(`/projects/${id}/runs`),
    export: (id) => api.post(`/projects/${id}/export`),
    uploadFiles: (id, files) => {
        const formData = new FormData();
        files.forEach(f => formData.append('files', f));
        return api.post(`/projects/${id}/upload`, formData, {
            headers: { 'Content-Type': 'multipart/form-data' }
        });
    },
    uploadZip: (id, file) => {
        const formData = new FormData();
        formData.append('file', file);
        return api.post(`/projects/${id}/upload-zip`, formData, {
            headers: { 'Content-Type': 'multipart/form-data' }
        });
    },
    getScanEstimate: (id) => api.get(`/projects/${id}/scan-estimate`),
    scan: (id) => api.post(`/projects/${id}/scan`)
};

// Files API
export const filesAPI = {
    getAll: (projectId) => api.get(`/projects/${projectId}/files`),
    create: (projectId, data) => api.post(`/projects/${projectId}/files`, data),
    update: (projectId, fileId, data) => api.put(`/projects/${projectId}/files/${fileId}`, data),
    delete: (projectId, fileId) => api.delete(`/projects/${projectId}/files/${fileId}`)
};

// Chat API
export const chatAPI = {
    getHistory: (projectId) => api.get(`/projects/${projectId}/chat`),
    send: (projectId, message, agentsEnabled = [], conversationId = null, multiAgentMode = false) => 
        api.post(`/projects/${projectId}/chat`, { 
            project_id: projectId, 
            message, 
            agents_enabled: agentsEnabled,
            conversation_id: conversationId,
            multi_agent_mode: multiAgentMode
        }),
    clear: (projectId) => api.delete(`/projects/${projectId}/chat`)
};

// Jobs API (Multi-Agent Pipeline)
export const jobsAPI = {
    create: (projectId, prompt, multiAgentMode = true) => 
        api.post('/jobs/create', { project_id: projectId, prompt, multi_agent_mode: multiAgentMode }),
    approve: (jobId, approved = true, modifiedTasks = null) => 
        api.post(`/jobs/${jobId}/approve`, { job_id: jobId, approved, modified_tasks: modifiedTasks }),
    continue: (jobId, approved = true) => 
        api.post(`/jobs/${jobId}/continue`, { job_id: jobId, approved }),
    get: (jobId) => api.get(`/jobs/${jobId}`),
    getAll: (limit = 20) => api.get(`/jobs?limit=${limit}`),
    // SSE endpoint for real-time execution updates
    getExecuteUrl: (jobId) => `${API_BASE}/jobs/${jobId}/execute`
};

// Todos API
export const todosAPI = {
    getAll: (projectId) => api.get(`/projects/${projectId}/todos`),
    create: (projectId, text, priority = 'medium') => 
        api.post(`/projects/${projectId}/todos`, { project_id: projectId, text, priority }),
    update: (projectId, todoId, data) => api.put(`/projects/${projectId}/todos/${todoId}`, data),
    delete: (projectId, todoId) => api.delete(`/projects/${projectId}/todos/${todoId}`)
};

// Cost Estimation API
export const costAPI = {
    estimate: (message, multiAgentMode = false) => 
        api.post('/estimate-cost', { message, multi_agent_mode: multiAgentMode })
};

// Global Assistant API
export const assistantAPI = {
    getHistory: (conversationId = null) => 
        api.get(`/assistant/chat${conversationId ? `?conversation_id=${conversationId}` : ''}`),
    send: (message, conversationId = null) => 
        api.post('/assistant/chat', { message, conversation_id: conversationId })
};

// Agents API
export const agentsAPI = {
    getAll: () => api.get('/agents')
};

// AI Providers API
export const aiProvidersAPI = {
    getAvailable: () => api.get('/ai-providers'),
    getUserProviders: () => api.get('/ai-providers/user'),
    addProvider: (data) => api.post('/ai-providers/user', data),
    deleteProvider: (provider) => api.delete(`/ai-providers/user/${provider}`)
};

// Credits API
export const creditsAPI = {
    getPackages: () => api.get('/credits/packages'),
    getBalance: () => api.get('/credits/balance'),
    getHistory: (limit = 50) => api.get(`/credits/history?limit=${limit}`),
    purchase: (packageId, originUrl) => api.post('/credits/purchase', { package_id: packageId, origin_url: originUrl }),
    checkStatus: (sessionId) => api.get(`/credits/status/${sessionId}`)
};

// LLM API
export const llmAPI = {
    generate: (prompt, model = 'local', maxTokens = 2000, temperature = 0.7) =>
        api.post('/llm/generate', { prompt, model, max_tokens: maxTokens, temperature }),
    getModels: () => api.get('/llm/models')
};

// Admin API
export const adminAPI = {
    getUsers: () => api.get('/admin/users'),
    getUserDetails: (id) => api.get(`/admin/users/${id}/details`),
    updateUser: (id, data) => api.put(`/admin/users/${id}`, data),
    deleteUser: (id) => api.delete(`/admin/users/${id}`),
    bulkAddCredits: (amount, userIds = null) => api.post('/admin/users/bulk-credits', { amount, user_ids: userIds }),
    getStats: () => api.get('/admin/stats'),
    getRunningJobs: () => api.get('/admin/running-jobs'),
    getAgentActivity: (limit = 100) => api.get(`/admin/agent-activity?limit=${limit}`),
    getKnowledgeBase: (limit = 100) => api.get(`/admin/knowledge-base?limit=${limit}`),
    deleteKnowledgeEntry: (id) => api.delete(`/admin/knowledge-base/${id}`),
    invalidateExpiredKnowledge: () => api.post('/admin/knowledge-base/invalidate-expired'),
    getSystemHealth: () => api.get('/admin/system-health'),
    getSettings: () => api.get('/admin/settings'),
    updateSetting: (key, value) => api.put(`/admin/settings/${key}?value=${value}`),
    updateCreditConfig: (chat, project) => api.put('/admin/credit-config', { 
        chat_rate: chat, 
        project_rate: project 
    }),
    // Subscription Plan Management
    getAllPlans: () => api.get('/plans/all'),
    getSubscriptionPlans: () => api.get('/admin/subscription-plans'),
    createSubscriptionPlan: (data) => api.post('/admin/subscription-plans', data),
    updateSubscriptionPlan: (planId, data) => api.put(`/admin/subscription-plans/${planId}`, data),
    deleteSubscriptionPlan: (planId) => api.delete(`/admin/subscription-plans/${planId}`),
    // Credit Package Management (Add-ons)
    getCreditPackages: () => api.get('/admin/credit-packages'),
    createCreditPackage: (data) => api.post('/admin/credit-packages', data),
    updateCreditPackage: (packageId, data) => api.put(`/admin/credit-packages/${packageId}`, data),
    deleteCreditPackage: (packageId) => api.delete(`/admin/credit-packages/${packageId}`),
    // Legacy plan endpoints
    createPlan: (data) => api.post('/admin/plans', data),
    updatePlan: (planId, data) => api.put(`/admin/plans/${planId}`, data),
    deletePlan: (planId) => api.delete(`/admin/plans/${planId}`),
    distributeDailyCredits: () => api.post('/admin/distribute-daily-credits'),
    // IP Records
    getIPRecords: (limit = 100) => api.get(`/admin/ip-records?limit=${limit}`),
    // Default Settings
    getDefaults: () => api.get('/admin/defaults'),
    updateDefaults: (data) => api.put('/admin/defaults', null, { params: data }),
    // Free AI Providers
    getFreeAIProviders: () => api.get('/admin/free-ai-providers'),
    toggleFreeAIProvider: (providerId, enabled, apiKey = null) => 
        api.put(`/admin/free-ai-providers/${providerId}`, null, { 
            params: { enabled, api_key: apiKey } 
        }),
    // AI Settings
    getAISettings: () => api.get('/admin/ai-settings'),
    toggleEmergentLLM: (enabled) => api.put(`/admin/ai-settings/emergent-toggle?enabled=${enabled}`),
    // Google Drive Config
    getGoogleDriveConfig: () => api.get('/admin/google-drive-config'),
    updateGoogleDriveConfig: (data) => api.put('/admin/google-drive-config', data)
};

// Friends API
export const friendsAPI = {
    getFriends: () => api.get('/friends'),
    sendRequest: (email) => api.post('/friends/request', { email }),
    getRequests: () => api.get('/friends/requests'),
    respondToRequest: (requestId, action) => api.put(`/friends/requests/${requestId}`, { action }),
    removeFriend: (friendUserId) => api.delete(`/friends/${friendUserId}`),
    // Direct Messages
    getMessages: (friendUserId, limit = 50) => api.get(`/friends/dm/${friendUserId}?limit=${limit}`),
    sendMessage: (friendUserId, message) => api.post(`/friends/dm/${friendUserId}`, { message }),
    getUnreadCount: () => api.get('/friends/dm/unread')
};

// Collaborators API
export const collaboratorsAPI = {
    getCollaborators: (projectId) => api.get(`/projects/${projectId}/collaborators`),
    addCollaborator: (projectId, userId, permission = 'edit') => 
        api.post(`/projects/${projectId}/collaborators`, { userId, permission }),
    updateCollaborator: (projectId, userId, permission) => 
        api.put(`/projects/${projectId}/collaborators/${userId}`, { permission }),
    removeCollaborator: (projectId, userId) => 
        api.delete(`/projects/${projectId}/collaborators/${userId}`),
    setCreditMode: (projectId, mode) => 
        api.put(`/projects/${projectId}/collaborators/credit-mode`, { mode })
};

// Collaboration API (sharing & real-time)
export const collaborationAPI = {
    createShareLink: (projectId) => api.post(`/collaboration/${projectId}/share`),
    validateShareLink: (shareToken) => api.get(`/collaboration/share/validate/${shareToken}`),
    downloadProject: (projectId) => api.get(`/collaboration/${projectId}/download`, { responseType: 'blob' }),
    exportToDrive: (projectId) => api.post(`/collaboration/${projectId}/export/drive`)
};

// Subscription/Plans API
export const plansAPI = {
    getAll: () => api.get('/plans'),
    getUserSubscription: () => api.get('/user/subscription'),
    subscribe: (planId, originUrl) => api.post('/user/subscribe', { plan_id: planId, origin_url: originUrl }),
    checkWorkspaceLimit: () => api.get('/user/workspace-limit')
};

// User API Keys API
export const userKeysAPI = {
    getAll: () => api.get('/user/api-keys'),
    add: (provider, apiKey, modelPreference = null) => api.post('/user/api-keys', { 
        provider, 
        api_key: apiKey, 
        model_preference: modelPreference 
    }),
    delete: (provider) => api.delete(`/user/api-keys/${provider}`),
    setDefault: (provider) => api.put(`/user/api-keys/${provider}/default`)
};

// User Profile API
export const profileAPI = {
    get: () => api.get('/user/profile'),
    update: (data) => api.put('/user/profile', data),
    updateTheme: (theme) => api.put('/user/theme', theme),
    uploadAvatar: (formData) => api.post('/user/avatar', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
    }),
    changePassword: (currentPassword, newPassword) => api.put('/user/password', {
        current_password: currentPassword,
        new_password: newPassword
    }),
    // Google Drive integration
    getGoogleDriveConfig: () => api.get('/user/google-drive'),
    saveGoogleDriveConfig: (config) => api.put('/user/google-drive', config)
};

// Credit Packages API (add-ons)
export const creditPackagesAPI = {
    getAll: () => api.get('/credits/packages'),
    purchaseAddon: (packageId, originUrl) => api.post('/credits/purchase-addon', {
        package_id: packageId,
        origin_url: originUrl
    })
};

// Languages API
export const languagesAPI = {
    getAll: () => api.get('/languages')
};

// Health API
export const healthAPI = {
    check: () => api.get('/health')
};

// Site Settings API (announcements, maintenance mode, etc.)
export const siteSettingsAPI = {
    get: () => api.get('/site-settings'),
    getPublic: () => api.get('/site-settings/public'),
    update: (settings) => api.put('/site-settings', settings)
};

export default api;
