import { createContext, useContext, useState, useEffect } from 'react';
import { authAPI } from './api';

// Comprehensive translation strings for all supported languages
const translations = {
    en: {
        // Navigation
        nav_dashboard: "Dashboard",
        nav_credits: "Credits",
        nav_settings: "Settings",
        nav_admin: "Admin",
        nav_logout: "Sign Out",
        nav_profile: "Profile & Settings",
        nav_buy_credits: "Buy Credits",
        nav_back_dashboard: "Back to Dashboard",
        nav_back_home: "Back to home",
        
        // Common
        common_save: "Save",
        common_cancel: "Cancel",
        common_delete: "Delete",
        common_edit: "Edit",
        common_create: "Create",
        common_loading: "Loading...",
        common_saving: "Saving...",
        common_error: "Error",
        common_success: "Saved",
        common_confirm: "Confirm",
        common_back: "Back",
        common_next: "Next",
        common_search: "Search",
        common_filter: "Filter",
        common_export: "Export",
        common_import: "Import",
        common_upload: "Upload",
        common_download: "Download",
        common_close: "Close",
        common_open: "Open",
        common_yes: "Yes",
        common_no: "No",
        common_remove: "Remove",
        common_add: "Add",
        common_update: "Update",
        common_view: "View",
        common_subscribe: "Subscribe",
        common_buy_now: "Buy Now",
        common_current_plan: "Current Plan",
        
        // Auth
        auth_login: "Login",
        auth_register: "Sign Up",
        auth_email: "Email",
        auth_password: "Password",
        auth_name: "Full Name",
        auth_forgot_password: "Forgot Password?",
        auth_no_account: "Don't have an account?",
        auth_have_account: "Already have an account?",
        auth_create_account: "Create Account",
        auth_sign_in: "Sign In",
        auth_sign_up: "Sign Up",
        auth_welcome_back: "Welcome back",
        auth_sign_in_continue: "Sign in to continue building",
        auth_create_your_account: "Create your account",
        auth_start_building: "Start building with AI today",
        auth_password_min: "At least 6 characters",
        auth_tos_agree: "I agree to the",
        auth_tos: "Terms of Service",
        auth_tos_acknowledge: "and acknowledge that AI-generated code may contain errors",
        
        // Landing
        landing_title: "Build Software with AI",
        landing_subtitle: "Let AI agents write your code",
        landing_start_building: "Start Building",
        landing_watch_demo: "Watch Demo",
        landing_features: "Features",
        landing_pricing: "Pricing",
        landing_about: "About",
        
        // Dashboard
        dashboard_title: "Your Projects",
        dashboard_new_project: "New Project",
        dashboard_no_projects: "No projects yet",
        dashboard_create_first: "Create your first project to get started",
        dashboard_recent: "Recent Projects",
        dashboard_all: "All Projects",
        dashboard_search_projects: "Search projects...",
        dashboard_project_name: "Project Name",
        dashboard_project_description: "Description (optional)",
        dashboard_project_language: "Language",
        dashboard_create_project: "Create Project",
        
        // Workspace
        workspace_chat: "Chat",
        workspace_output: "Output",
        workspace_todo: "To-Do",
        workspace_files: "Files",
        workspace_agents: "Agents",
        workspace_multi_agent: "Multi-Agent Mode",
        workspace_multi_agent_on: "AI agents work together to build full projects",
        workspace_multi_agent_off: "AI generates and analyzes code directly in chat",
        workspace_send: "Send",
        workspace_type_message: "Type your message...",
        workspace_build: "Build",
        workspace_run: "Run",
        workspace_save: "Save",
        workspace_new_file: "New File",
        workspace_upload_files: "Upload Files",
        workspace_upload_desc: "Upload individual files or a ZIP archive for the AI to work on.",
        workspace_select_files: "Click to select files",
        workspace_uploading: "Uploading...",
        workspace_approve_build: "Approve & Build",
        workspace_agent_working: "is working...",
        workspace_code_editor: "Code Editor",
        workspace_file_tree: "File Tree",
        workspace_no_files: "No files yet",
        workspace_create_file: "Create a file",
        workspace_or_use_ai: "or use Multi-Agent Mode to generate code",
        workspace_no_file_selected: "No file selected",
        workspace_select_file: "Select a file to view and edit its content",
        workspace_hover_name: "hover for name",
        workspace_insufficient_credits: "Insufficient credits. Please purchase more credits to continue.",
        
        // Project count
        dashboard_projects_count: "projects",
        dashboard_project_count: "project",
        
        // Credits
        credits_title: "Plans & Credits",
        credits_subtitle: "Choose a plan or buy add-on credits to power your AI development",
        credits_balance: "Your Balance",
        credits_buy: "Buy Credits",
        credits_history: "Transaction History",
        credits_no_refunds: "Credits are non-refundable and non-transferable. All sales are final.",
        credits_monthly_plans: "Monthly Plans",
        credits_addon_credits: "Add-on Credits",
        credits_addon_desc: "Need more credits? Purchase add-on packs anytime to boost your balance.",
        credits_never_expire: "These are one-time purchases that never expire.",
        credits_how_plans_work: "How Monthly Plans Work",
        credits_daily_credits_desc: "Credits refresh daily based on your plan. Unused credits roll over up to a maximum balance.",
        credits_workspaces_desc: "Higher plans allow more projects to be built simultaneously by the AI agents.",
        credits_api_keys_desc: "Pro+ plans let you use your own OpenAI/Anthropic keys, bypassing credit usage entirely.",
        credits_per_credit: "per credit",
        credits_ai_credits: "AI credits",
        credits_never_expires: "Never expires",
        credits_instant_delivery: "Instant delivery",
        credits_works_any_plan: "Works with any plan",
        credits_best_value: "BEST VALUE",
        credits_most_popular: "MOST POPULAR",
        credits_day: "day",
        credits_month: "mo",
        
        // FAQ
        faq_title: "Frequently Asked Questions",
        faq_diff_plans_addons: "What's the difference between plans and add-ons?",
        faq_diff_plans_addons_answer: "Monthly plans give you daily credits that refresh each day, plus features like more workspaces and API key support. Add-on credits are one-time purchases that top up your balance and never expire.",
        faq_what_credits: "What are credits used for?",
        faq_what_credits_answer: "Credits are used for AI interactions, including chat messages, code generation, and project builds. Each AI operation consumes credits based on the complexity of the task.",
        faq_credits_expire: "Do add-on credits expire?",
        faq_credits_expire_answer: "No, add-on credits never expire. Use them whenever you need them. Daily credits from plans also roll over up to a maximum balance.",
        faq_own_api_key: "Can I use my own API key?",
        faq_own_api_key_answer: "Yes! If you configure your own OpenAI, Anthropic, or other API keys in Profile settings (requires Pro plan or higher), you won't be charged any credits. You'll only pay your API provider directly.",
        faq_change_plan: "Can I change my plan anytime?",
        faq_change_plan_answer: "Yes! You can upgrade or downgrade your plan at any time. When upgrading, you'll get immediate access to higher limits. When downgrading, changes take effect at your next billing cycle.",
        faq_refund_policy: "Refund Policy",
        faq_refund_answer: "Credits and subscriptions are non-refundable and non-transferable. All sales are final. We encourage you to start with a smaller package or the free plan to ensure our platform meets your needs.",
        
        // Profile/Settings
        settings_title: "Profile & Settings",
        settings_profile: "Profile Information",
        settings_theme: "Theme Customization",
        settings_language: "Language",
        settings_ai_providers: "AI Providers",
        settings_credit_activity: "Recent Credit Activity",
        settings_change_password: "Change Password",
        settings_current_password: "Current Password",
        settings_new_password: "New Password",
        settings_confirm_password: "Confirm New Password",
        settings_update_profile: "Update Profile",
        settings_upload_avatar: "Upload Avatar",
        settings_display_name: "Display Name (AI will use this)",
        settings_display_name_hint: "The AI assistant will address you by this name",
        settings_security: "Security",
        settings_security_desc: "Update your password to keep your account secure",
        settings_account: "Account",
        settings_add_provider: "Add Provider",
        settings_provider: "Provider",
        settings_api_key: "API Key",
        settings_model: "Preferred Model",
        settings_set_default: "Set as Default",
        settings_no_providers: "No AI providers configured",
        settings_add_api_keys: "Add your own API keys to use different AI models",
        settings_feature_locked: "Feature Locked",
        settings_feature_locked_desc: "Custom AI providers are available on Pro, OpenAI, and Enterprise plans. Upgrade your plan to use your own API keys and bypass credit usage.",
        settings_upgrade_plan: "Upgrade Plan",
        settings_pro_required: "Pro+ Required",
        settings_buy_more: "Buy more credits",
        settings_no_activity: "No credit activity yet",
        
        // Theme
        theme_primary: "Primary Color",
        theme_secondary: "Secondary Color",
        theme_background: "Background",
        theme_card: "Card Color",
        theme_text: "Text Color",
        theme_hover: "Hover Color",
        theme_credits: "Credits Color",
        theme_bg_image: "Background Image URL",
        theme_reset: "Reset",
        theme_preview: "Preview",
        theme_save: "Save Theme",
        
        // Plans
        plan_free: "Free",
        plan_starter: "Starter",
        plan_pro: "Pro",
        plan_openai: "OpenAI",
        plan_enterprise: "Enterprise",
        plan_upgrade: "Upgrade Plan",
        plan_current: "Current Plan",
        plan_switch_free: "Switch to Free",
        plan_workspaces: "workspace",
        plan_workspaces_plural: "workspaces",
        plan_daily_credits: "credits/day",
        plan_own_api_keys: "Own API keys",
        plan_daily_credits_title: "Daily Credits",
        plan_concurrent_workspaces: "Concurrent Workspaces",
        
        // Admin
        admin_title: "Admin Panel",
        admin_users: "Users",
        admin_plans: "Plans",
        admin_stats: "Statistics",
        admin_settings: "Settings",
        admin_ai_providers: "AI Providers",
        admin_ip_records: "IP Records",
        admin_system_health: "System Health",
        admin_running_jobs: "Running Jobs",
        admin_distribute_credits: "Distribute Daily Credits",
        admin_new_plan: "New Plan",
        admin_edit_plan: "Edit Plan",
        admin_delete_plan: "Delete Plan",
        admin_total_users: "Total Users",
        admin_total_projects: "Total Projects",
        admin_total_credits: "Total Credits Used",
        admin_active_jobs: "Active Jobs",
        
        // Global Assistant
        assistant_title: "LittleHelper AI",
        assistant_greeting: "Hi! I'm LittleHelper. How can I assist you today?",
        assistant_placeholder: "Ask me anything...",
        assistant_conversations: "Conversations",
        assistant_new_conversation: "New conversation",
        assistant_history: "Conversation history",
        assistant_close: "Close assistant",
        
        // TOS
        tos_title: "Terms of Service",
        tos_please_read: "Please read and accept to continue",
        tos_updated: "Updated Terms of Service",
        tos_review: "Please review and accept to continue",
        tos_important: "Important Notice",
        tos_important_desc: "LittleHelper AI generates code using artificial intelligence. You are responsible for reviewing and testing all generated code before use. We are not liable for any issues arising from the use of AI-generated content.",
        tos_accept: "I Accept the Terms",
        tos_decline: "Decline",
        tos_decline_logout: "Decline & Log Out",
        tos_must_accept: "You must accept the Terms of Service to continue using LittleHelper AI",
        tos_click_accept: "By clicking \"I Accept\", you agree to our Terms of Service and Privacy Policy",
        
        // Errors
        error_login_failed: "Login failed. Please try again.",
        error_register_failed: "Registration failed. Please try again.",
        error_invalid_credentials: "Invalid email or password",
        error_email_exists: "Email already registered",
        error_password_mismatch: "Passwords do not match",
        error_password_min: "Password must be at least 8 characters",
        error_tos_required: "You must accept the Terms of Service to register",
        error_generic: "Something went wrong. Please try again.",
        
        // Success messages
        success_password_changed: "Password changed successfully",
        success_profile_updated: "Profile updated successfully",
        success_theme_saved: "Theme saved successfully",
        success_payment: "Payment successful!",
        success_credits_added: "credits added to your account.",
    },
    es: {
        // Navigation
        nav_dashboard: "Panel",
        nav_credits: "Créditos",
        nav_settings: "Configuración",
        nav_admin: "Admin",
        nav_logout: "Cerrar Sesión",
        nav_profile: "Perfil y Configuración",
        nav_buy_credits: "Comprar Créditos",
        nav_back_dashboard: "Volver al Panel",
        nav_back_home: "Volver al inicio",
        
        // Common
        common_save: "Guardar",
        common_cancel: "Cancelar",
        common_delete: "Eliminar",
        common_edit: "Editar",
        common_create: "Crear",
        common_loading: "Cargando...",
        common_saving: "Guardando...",
        common_error: "Error",
        common_success: "Guardado",
        common_confirm: "Confirmar",
        common_back: "Atrás",
        common_next: "Siguiente",
        common_search: "Buscar",
        common_filter: "Filtrar",
        common_export: "Exportar",
        common_import: "Importar",
        common_upload: "Subir",
        common_download: "Descargar",
        common_close: "Cerrar",
        common_open: "Abrir",
        common_yes: "Sí",
        common_no: "No",
        common_remove: "Quitar",
        common_add: "Añadir",
        common_update: "Actualizar",
        common_view: "Ver",
        common_subscribe: "Suscribirse",
        common_buy_now: "Comprar Ahora",
        common_current_plan: "Plan Actual",
        
        // Auth
        auth_login: "Iniciar Sesión",
        auth_register: "Registrarse",
        auth_email: "Correo Electrónico",
        auth_password: "Contraseña",
        auth_name: "Nombre Completo",
        auth_forgot_password: "¿Olvidaste tu contraseña?",
        auth_no_account: "¿No tienes una cuenta?",
        auth_have_account: "¿Ya tienes una cuenta?",
        auth_create_account: "Crear Cuenta",
        auth_sign_in: "Entrar",
        auth_sign_up: "Registrarse",
        auth_welcome_back: "Bienvenido de nuevo",
        auth_sign_in_continue: "Inicia sesión para continuar construyendo",
        auth_create_your_account: "Crea tu cuenta",
        auth_start_building: "Comienza a construir con IA hoy",
        auth_password_min: "Al menos 6 caracteres",
        auth_tos_agree: "Acepto los",
        auth_tos: "Términos de Servicio",
        auth_tos_acknowledge: "y reconozco que el código generado por IA puede contener errores",
        
        // Landing
        landing_title: "Construye Software con IA",
        landing_subtitle: "Deja que los agentes de IA escriban tu código",
        landing_start_building: "Comenzar a Construir",
        landing_watch_demo: "Ver Demo",
        landing_features: "Características",
        landing_pricing: "Precios",
        landing_about: "Acerca de",
        
        // Dashboard
        dashboard_title: "Tus Proyectos",
        dashboard_new_project: "Nuevo Proyecto",
        dashboard_no_projects: "Sin proyectos aún",
        dashboard_create_first: "Crea tu primer proyecto para comenzar",
        dashboard_recent: "Proyectos Recientes",
        dashboard_all: "Todos los Proyectos",
        dashboard_search_projects: "Buscar proyectos...",
        dashboard_project_name: "Nombre del Proyecto",
        dashboard_project_description: "Descripción (opcional)",
        dashboard_project_language: "Lenguaje",
        dashboard_create_project: "Crear Proyecto",
        
        // Workspace
        workspace_chat: "Chat",
        workspace_output: "Salida",
        workspace_todo: "Tareas",
        workspace_files: "Archivos",
        workspace_agents: "Agentes",
        workspace_multi_agent: "Modo Multi-Agente",
        workspace_multi_agent_on: "Los agentes de IA trabajan juntos para construir proyectos completos",
        workspace_multi_agent_off: "La IA genera y analiza código directamente en el chat",
        workspace_send: "Enviar",
        workspace_type_message: "Escribe tu mensaje...",
        workspace_build: "Construir",
        workspace_run: "Ejecutar",
        workspace_save: "Guardar",
        workspace_new_file: "Nuevo Archivo",
        workspace_upload_files: "Subir Archivos",
        workspace_upload_desc: "Sube archivos individuales o un archivo ZIP para que la IA trabaje.",
        workspace_select_files: "Haz clic para seleccionar archivos",
        workspace_uploading: "Subiendo...",
        workspace_approve_build: "Aprobar y Construir",
        workspace_agent_working: "está trabajando...",
        workspace_code_editor: "Editor de Código",
        workspace_file_tree: "Árbol de Archivos",
        
        // Credits
        credits_title: "Planes y Créditos",
        credits_subtitle: "Elige un plan o compra créditos adicionales para potenciar tu desarrollo con IA",
        credits_balance: "Tu Saldo",
        credits_buy: "Comprar Créditos",
        credits_history: "Historial de Transacciones",
        credits_no_refunds: "Los créditos no son reembolsables ni transferibles. Todas las ventas son finales.",
        credits_monthly_plans: "Planes Mensuales",
        credits_addon_credits: "Créditos Adicionales",
        credits_addon_desc: "¿Necesitas más créditos? Compra paquetes adicionales en cualquier momento.",
        credits_never_expire: "Estas son compras únicas que nunca expiran.",
        credits_how_plans_work: "Cómo Funcionan los Planes Mensuales",
        credits_daily_credits_desc: "Los créditos se actualizan diariamente según tu plan. Los créditos no usados se acumulan hasta un saldo máximo.",
        credits_workspaces_desc: "Los planes superiores permiten más proyectos construidos simultáneamente por los agentes de IA.",
        credits_api_keys_desc: "Los planes Pro+ te permiten usar tus propias claves de OpenAI/Anthropic, sin usar créditos.",
        credits_per_credit: "por crédito",
        credits_ai_credits: "créditos de IA",
        credits_never_expires: "Nunca expira",
        credits_instant_delivery: "Entrega instantánea",
        credits_works_any_plan: "Funciona con cualquier plan",
        credits_best_value: "MEJOR VALOR",
        credits_most_popular: "MÁS POPULAR",
        credits_day: "día",
        credits_month: "mes",
        
        // FAQ
        faq_title: "Preguntas Frecuentes",
        faq_diff_plans_addons: "¿Cuál es la diferencia entre planes y adicionales?",
        faq_diff_plans_addons_answer: "Los planes mensuales te dan créditos diarios que se actualizan cada día, además de funciones como más espacios de trabajo y soporte de claves API. Los créditos adicionales son compras únicas que recargan tu saldo y nunca expiran.",
        faq_what_credits: "¿Para qué se usan los créditos?",
        faq_what_credits_answer: "Los créditos se usan para interacciones con IA, incluyendo mensajes de chat, generación de código y construcción de proyectos. Cada operación de IA consume créditos según la complejidad de la tarea.",
        faq_credits_expire: "¿Los créditos adicionales expiran?",
        faq_credits_expire_answer: "No, los créditos adicionales nunca expiran. Úsalos cuando los necesites. Los créditos diarios de los planes también se acumulan hasta un saldo máximo.",
        faq_own_api_key: "¿Puedo usar mi propia clave API?",
        faq_own_api_key_answer: "¡Sí! Si configuras tus propias claves de OpenAI, Anthropic u otras en la configuración del Perfil (requiere plan Pro o superior), no se te cobrarán créditos. Solo pagarás directamente a tu proveedor de API.",
        faq_change_plan: "¿Puedo cambiar mi plan en cualquier momento?",
        faq_change_plan_answer: "¡Sí! Puedes mejorar o reducir tu plan en cualquier momento. Al mejorar, obtendrás acceso inmediato a límites más altos. Al reducir, los cambios se aplican en tu próximo ciclo de facturación.",
        faq_refund_policy: "Política de Reembolso",
        faq_refund_answer: "Los créditos y suscripciones no son reembolsables ni transferibles. Todas las ventas son finales. Te animamos a comenzar con un paquete más pequeño o el plan gratuito para asegurarte de que nuestra plataforma satisface tus necesidades.",
        
        // Profile/Settings
        settings_title: "Perfil y Configuración",
        settings_profile: "Información del Perfil",
        settings_theme: "Personalización del Tema",
        settings_language: "Idioma",
        settings_ai_providers: "Proveedores de IA",
        settings_credit_activity: "Actividad de Créditos Reciente",
        settings_change_password: "Cambiar Contraseña",
        settings_current_password: "Contraseña Actual",
        settings_new_password: "Nueva Contraseña",
        settings_confirm_password: "Confirmar Nueva Contraseña",
        settings_update_profile: "Actualizar Perfil",
        settings_upload_avatar: "Subir Avatar",
        settings_display_name: "Nombre para Mostrar (la IA usará este)",
        settings_display_name_hint: "El asistente de IA te llamará por este nombre",
        settings_security: "Seguridad",
        settings_security_desc: "Actualiza tu contraseña para mantener tu cuenta segura",
        settings_account: "Cuenta",
        settings_add_provider: "Añadir Proveedor",
        settings_provider: "Proveedor",
        settings_api_key: "Clave API",
        settings_model: "Modelo Preferido",
        settings_set_default: "Establecer como Predeterminado",
        settings_no_providers: "No hay proveedores de IA configurados",
        settings_add_api_keys: "Añade tus propias claves API para usar diferentes modelos de IA",
        settings_feature_locked: "Función Bloqueada",
        settings_feature_locked_desc: "Los proveedores de IA personalizados están disponibles en los planes Pro, OpenAI y Enterprise. Mejora tu plan para usar tus propias claves API.",
        settings_upgrade_plan: "Mejorar Plan",
        settings_pro_required: "Pro+ Requerido",
        settings_buy_more: "Comprar más créditos",
        settings_no_activity: "Sin actividad de créditos aún",
        
        // Theme
        theme_primary: "Color Primario",
        theme_secondary: "Color Secundario",
        theme_background: "Fondo",
        theme_card: "Color de Tarjeta",
        theme_text: "Color de Texto",
        theme_hover: "Color de Hover",
        theme_credits: "Color de Créditos",
        theme_bg_image: "URL de Imagen de Fondo",
        theme_reset: "Restablecer",
        theme_preview: "Vista Previa",
        theme_save: "Guardar Tema",
        
        // Plans
        plan_free: "Gratis",
        plan_starter: "Inicial",
        plan_pro: "Pro",
        plan_openai: "OpenAI",
        plan_enterprise: "Empresarial",
        plan_upgrade: "Mejorar Plan",
        plan_current: "Plan Actual",
        plan_switch_free: "Cambiar a Gratis",
        plan_workspaces: "espacio de trabajo",
        plan_workspaces_plural: "espacios de trabajo",
        plan_daily_credits: "créditos/día",
        plan_own_api_keys: "Claves API propias",
        plan_daily_credits_title: "Créditos Diarios",
        plan_concurrent_workspaces: "Espacios de Trabajo Simultáneos",
        
        // Admin
        admin_title: "Panel de Admin",
        admin_users: "Usuarios",
        admin_plans: "Planes",
        admin_stats: "Estadísticas",
        admin_settings: "Configuración",
        admin_ai_providers: "Proveedores de IA",
        admin_ip_records: "Registros de IP",
        admin_system_health: "Salud del Sistema",
        admin_running_jobs: "Trabajos en Ejecución",
        admin_distribute_credits: "Distribuir Créditos Diarios",
        admin_new_plan: "Nuevo Plan",
        admin_edit_plan: "Editar Plan",
        admin_delete_plan: "Eliminar Plan",
        admin_total_users: "Total de Usuarios",
        admin_total_projects: "Total de Proyectos",
        admin_total_credits: "Créditos Totales Usados",
        admin_active_jobs: "Trabajos Activos",
        
        // Global Assistant
        assistant_title: "LittleHelper IA",
        assistant_greeting: "¡Hola! Soy LittleHelper. ¿En qué puedo ayudarte hoy?",
        assistant_placeholder: "Pregúntame lo que sea...",
        assistant_conversations: "Conversaciones",
        assistant_new_conversation: "Nueva conversación",
        assistant_history: "Historial de conversaciones",
        assistant_close: "Cerrar asistente",
        
        // TOS
        tos_title: "Términos de Servicio",
        tos_please_read: "Por favor lee y acepta para continuar",
        tos_updated: "Términos de Servicio Actualizados",
        tos_review: "Por favor revisa y acepta para continuar",
        tos_important: "Aviso Importante",
        tos_important_desc: "LittleHelper AI genera código usando inteligencia artificial. Eres responsable de revisar y probar todo el código generado antes de usarlo. No somos responsables de ningún problema derivado del uso de contenido generado por IA.",
        tos_accept: "Acepto los Términos",
        tos_decline: "Rechazar",
        tos_decline_logout: "Rechazar y Cerrar Sesión",
        tos_must_accept: "Debes aceptar los Términos de Servicio para continuar usando LittleHelper AI",
        tos_click_accept: "Al hacer clic en \"Acepto\", aceptas nuestros Términos de Servicio y Política de Privacidad",
        
        // Errors
        error_login_failed: "Error al iniciar sesión. Por favor intenta de nuevo.",
        error_register_failed: "Error al registrarse. Por favor intenta de nuevo.",
        error_invalid_credentials: "Email o contraseña inválidos",
        error_email_exists: "Email ya registrado",
        error_password_mismatch: "Las contraseñas no coinciden",
        error_password_min: "La contraseña debe tener al menos 8 caracteres",
        error_tos_required: "Debes aceptar los Términos de Servicio para registrarte",
        error_generic: "Algo salió mal. Por favor intenta de nuevo.",
        
        // Success messages
        success_password_changed: "Contraseña cambiada exitosamente",
        success_profile_updated: "Perfil actualizado exitosamente",
        success_theme_saved: "Tema guardado exitosamente",
        success_payment: "¡Pago exitoso!",
        success_credits_added: "créditos añadidos a tu cuenta.",
    },
    fr: {
        // Navigation
        nav_dashboard: "Tableau de Bord",
        nav_credits: "Crédits",
        nav_settings: "Paramètres",
        nav_admin: "Admin",
        nav_logout: "Déconnexion",
        nav_profile: "Profil et Paramètres",
        nav_buy_credits: "Acheter des Crédits",
        nav_back_dashboard: "Retour au Tableau de Bord",
        nav_back_home: "Retour à l'accueil",
        
        // Common
        common_save: "Enregistrer",
        common_cancel: "Annuler",
        common_delete: "Supprimer",
        common_edit: "Modifier",
        common_create: "Créer",
        common_loading: "Chargement...",
        common_saving: "Enregistrement...",
        common_error: "Erreur",
        common_success: "Enregistré",
        common_confirm: "Confirmer",
        common_back: "Retour",
        common_next: "Suivant",
        common_search: "Rechercher",
        common_filter: "Filtrer",
        common_export: "Exporter",
        common_import: "Importer",
        common_upload: "Télécharger",
        common_download: "Télécharger",
        common_close: "Fermer",
        common_open: "Ouvrir",
        common_yes: "Oui",
        common_no: "Non",
        common_remove: "Supprimer",
        common_add: "Ajouter",
        common_update: "Mettre à jour",
        common_view: "Voir",
        common_subscribe: "S'abonner",
        common_buy_now: "Acheter Maintenant",
        common_current_plan: "Plan Actuel",
        
        // Auth
        auth_login: "Connexion",
        auth_register: "S'inscrire",
        auth_email: "Email",
        auth_password: "Mot de passe",
        auth_name: "Nom Complet",
        auth_forgot_password: "Mot de passe oublié?",
        auth_no_account: "Pas de compte?",
        auth_have_account: "Déjà un compte?",
        auth_create_account: "Créer un Compte",
        auth_sign_in: "Se Connecter",
        auth_sign_up: "S'inscrire",
        auth_welcome_back: "Bon retour",
        auth_sign_in_continue: "Connectez-vous pour continuer à construire",
        auth_create_your_account: "Créez votre compte",
        auth_start_building: "Commencez à construire avec l'IA aujourd'hui",
        auth_password_min: "Au moins 6 caractères",
        auth_tos_agree: "J'accepte les",
        auth_tos: "Conditions d'Utilisation",
        auth_tos_acknowledge: "et je reconnais que le code généré par l'IA peut contenir des erreurs",
        
        // Dashboard
        dashboard_title: "Vos Projets",
        dashboard_new_project: "Nouveau Projet",
        dashboard_no_projects: "Pas encore de projets",
        dashboard_create_first: "Créez votre premier projet pour commencer",
        dashboard_search_projects: "Rechercher des projets...",
        dashboard_project_name: "Nom du Projet",
        dashboard_project_description: "Description (optionnelle)",
        dashboard_project_language: "Langage",
        dashboard_create_project: "Créer le Projet",
        
        // Workspace
        workspace_chat: "Chat",
        workspace_output: "Sortie",
        workspace_todo: "À Faire",
        workspace_files: "Fichiers",
        workspace_agents: "Agents",
        workspace_multi_agent: "Mode Multi-Agent",
        workspace_send: "Envoyer",
        workspace_type_message: "Tapez votre message...",
        workspace_build: "Construire",
        workspace_run: "Exécuter",
        workspace_save: "Enregistrer",
        workspace_new_file: "Nouveau Fichier",
        workspace_upload_files: "Télécharger des Fichiers",
        workspace_approve_build: "Approuver et Construire",
        
        // Credits
        credits_title: "Plans et Crédits",
        credits_subtitle: "Choisissez un plan ou achetez des crédits supplémentaires pour votre développement IA",
        credits_balance: "Votre Solde",
        credits_monthly_plans: "Plans Mensuels",
        credits_addon_credits: "Crédits Supplémentaires",
        credits_how_plans_work: "Comment Fonctionnent les Plans Mensuels",
        
        // Settings
        settings_title: "Profil et Paramètres",
        settings_profile: "Informations du Profil",
        settings_theme: "Personnalisation du Thème",
        settings_language: "Langue",
        settings_change_password: "Changer le Mot de Passe",
        settings_current_password: "Mot de Passe Actuel",
        settings_new_password: "Nouveau Mot de Passe",
        settings_security: "Sécurité",
        settings_account: "Compte",
        
        // Theme
        theme_primary: "Couleur Primaire",
        theme_secondary: "Couleur Secondaire",
        theme_background: "Fond",
        theme_reset: "Réinitialiser",
        theme_save: "Enregistrer le Thème",
        
        // Plans
        plan_free: "Gratuit",
        plan_starter: "Débutant",
        plan_pro: "Pro",
        plan_enterprise: "Entreprise",
        plan_daily_credits: "crédits/jour",
        
        // Assistant
        assistant_title: "LittleHelper IA",
        assistant_greeting: "Bonjour! Je suis LittleHelper. Comment puis-je vous aider?",
        assistant_placeholder: "Demandez-moi n'importe quoi...",
        
        // TOS
        tos_title: "Conditions d'Utilisation",
        tos_accept: "J'accepte les Conditions",
        tos_decline: "Refuser",
        
        // FAQ
        faq_title: "Questions Fréquentes",
    },
    de: {
        // Navigation
        nav_dashboard: "Dashboard",
        nav_credits: "Guthaben",
        nav_settings: "Einstellungen",
        nav_admin: "Admin",
        nav_logout: "Abmelden",
        nav_profile: "Profil & Einstellungen",
        nav_buy_credits: "Guthaben Kaufen",
        nav_back_dashboard: "Zurück zum Dashboard",
        nav_back_home: "Zurück zur Startseite",
        
        // Common
        common_save: "Speichern",
        common_cancel: "Abbrechen",
        common_delete: "Löschen",
        common_edit: "Bearbeiten",
        common_create: "Erstellen",
        common_loading: "Laden...",
        common_saving: "Speichern...",
        common_error: "Fehler",
        common_success: "Gespeichert",
        common_subscribe: "Abonnieren",
        common_buy_now: "Jetzt Kaufen",
        common_current_plan: "Aktueller Plan",
        
        // Auth
        auth_login: "Anmelden",
        auth_register: "Registrieren",
        auth_email: "E-Mail",
        auth_password: "Passwort",
        auth_name: "Vollständiger Name",
        auth_welcome_back: "Willkommen zurück",
        auth_create_account: "Konto Erstellen",
        auth_sign_in: "Einloggen",
        
        // Dashboard
        dashboard_title: "Ihre Projekte",
        dashboard_new_project: "Neues Projekt",
        dashboard_no_projects: "Noch keine Projekte",
        dashboard_search_projects: "Projekte suchen...",
        
        // Workspace
        workspace_chat: "Chat",
        workspace_output: "Ausgabe",
        workspace_files: "Dateien",
        workspace_multi_agent: "Multi-Agent-Modus",
        workspace_build: "Bauen",
        workspace_run: "Ausführen",
        
        // Credits
        credits_title: "Pläne & Guthaben",
        credits_monthly_plans: "Monatliche Pläne",
        credits_addon_credits: "Zusätzliches Guthaben",
        
        // Settings
        settings_title: "Profil & Einstellungen",
        settings_profile: "Profilinformationen",
        settings_theme: "Design-Anpassung",
        settings_language: "Sprache",
        settings_change_password: "Passwort Ändern",
        settings_security: "Sicherheit",
        
        // Plans
        plan_free: "Kostenlos",
        plan_daily_credits: "Guthaben/Tag",
        
        // Assistant
        assistant_title: "LittleHelper KI",
        assistant_greeting: "Hallo! Ich bin LittleHelper. Wie kann ich Ihnen helfen?",
        
        // TOS
        tos_title: "Nutzungsbedingungen",
        tos_accept: "Ich akzeptiere die Bedingungen",
        
        // FAQ
        faq_title: "Häufig gestellte Fragen",
    },
    zh: {
        // Navigation
        nav_dashboard: "仪表板",
        nav_credits: "积分",
        nav_settings: "设置",
        nav_admin: "管理",
        nav_logout: "退出登录",
        nav_profile: "个人资料和设置",
        nav_buy_credits: "购买积分",
        nav_back_dashboard: "返回仪表板",
        nav_back_home: "返回首页",
        
        // Common
        common_save: "保存",
        common_cancel: "取消",
        common_delete: "删除",
        common_edit: "编辑",
        common_create: "创建",
        common_loading: "加载中...",
        common_saving: "保存中...",
        common_error: "错误",
        common_success: "已保存",
        common_subscribe: "订阅",
        common_buy_now: "立即购买",
        common_current_plan: "当前计划",
        common_export: "导出",
        common_upload: "上传",
        common_remove: "移除",
        common_add: "添加",
        common_close: "关闭",
        common_confirm: "确认",
        
        // Auth
        auth_login: "登录",
        auth_register: "注册",
        auth_email: "邮箱",
        auth_password: "密码",
        auth_name: "全名",
        auth_welcome_back: "欢迎回来",
        auth_create_account: "创建账号",
        auth_sign_in: "登录",
        auth_sign_in_continue: "登录以继续构建",
        auth_create_your_account: "创建您的账号",
        auth_start_building: "今天就开始用AI构建",
        auth_password_min: "至少6个字符",
        auth_tos_agree: "我同意",
        auth_tos: "服务条款",
        auth_tos_acknowledge: "并承认AI生成的代码可能包含错误",
        auth_no_account: "还没有账号？",
        auth_have_account: "已有账号？",
        
        // Dashboard
        dashboard_title: "您的项目",
        dashboard_new_project: "新建项目",
        dashboard_no_projects: "暂无项目",
        dashboard_create_first: "创建您的第一个项目开始使用",
        dashboard_search_projects: "搜索项目...",
        dashboard_project_name: "项目名称",
        dashboard_project_description: "描述（可选）",
        dashboard_project_language: "编程语言",
        dashboard_create_project: "创建项目",
        dashboard_projects_count: "个项目",
        dashboard_project_count: "个项目",
        
        // Workspace
        workspace_chat: "聊天",
        workspace_output: "输出",
        workspace_todo: "待办",
        workspace_files: "文件",
        workspace_agents: "代理",
        workspace_multi_agent: "多代理模式",
        workspace_multi_agent_on: "AI代理协同工作，构建完整项目",
        workspace_multi_agent_off: "AI直接在聊天中生成和分析代码",
        workspace_build: "构建",
        workspace_run: "运行",
        workspace_save: "保存",
        workspace_send: "发送",
        workspace_type_message: "输入您的消息...",
        workspace_new_file: "新建文件",
        workspace_upload_files: "上传文件",
        workspace_upload_desc: "上传单个文件或ZIP压缩包供AI处理。",
        workspace_select_files: "点击选择文件",
        workspace_uploading: "上传中...",
        workspace_approve_build: "批准并构建",
        workspace_agent_working: "正在工作...",
        workspace_code_editor: "代码编辑器",
        workspace_file_tree: "文件树",
        workspace_no_files: "暂无文件",
        workspace_create_file: "创建文件",
        workspace_or_use_ai: "或使用多代理模式生成代码",
        workspace_no_file_selected: "未选择文件",
        workspace_select_file: "选择一个文件来查看和编辑其内容",
        workspace_hover_name: "悬停显示名称",
        workspace_insufficient_credits: "积分不足。请购买更多积分继续使用。",
        
        // Credits
        credits_title: "计划和积分",
        credits_subtitle: "选择计划或购买附加积分来支持您的AI开发",
        credits_balance: "您的余额",
        credits_monthly_plans: "月度计划",
        credits_addon_credits: "附加积分",
        credits_addon_desc: "需要更多积分？随时购买积分包来增加余额。",
        credits_never_expire: "这些是一次性购买，永不过期。",
        credits_how_plans_work: "月度计划如何运作",
        credits_daily_credits_desc: "积分根据您的计划每天刷新。未使用的积分可累积到最大余额。",
        credits_workspaces_desc: "更高级的计划允许AI代理同时构建更多项目。",
        credits_api_keys_desc: "Pro+计划允许您使用自己的OpenAI/Anthropic密钥，完全绕过积分使用。",
        credits_per_credit: "每积分",
        credits_ai_credits: "AI积分",
        credits_never_expires: "永不过期",
        credits_instant_delivery: "即时交付",
        credits_works_any_plan: "适用于任何计划",
        credits_best_value: "最佳价值",
        credits_most_popular: "最受欢迎",
        
        // Settings/Profile
        settings_title: "个人资料和设置",
        settings_profile: "个人信息",
        settings_theme: "主题自定义",
        settings_language: "语言",
        settings_ai_providers: "AI提供商",
        settings_credit_activity: "近期积分活动",
        settings_change_password: "修改密码",
        settings_current_password: "当前密码",
        settings_new_password: "新密码",
        settings_confirm_password: "确认新密码",
        settings_update_profile: "更新资料",
        settings_upload_avatar: "上传头像",
        settings_display_name: "显示名称（AI将使用此名称）",
        settings_display_name_hint: "AI助手将用此名称称呼您",
        settings_security: "安全",
        settings_security_desc: "更新密码以保护账户安全",
        settings_account: "账户",
        settings_add_provider: "添加提供商",
        settings_provider: "提供商",
        settings_api_key: "API密钥",
        settings_model: "首选模型",
        settings_set_default: "设为默认",
        settings_no_providers: "未配置AI提供商",
        settings_add_api_keys: "添加您自己的API密钥以使用不同的AI模型",
        settings_feature_locked: "功能锁定",
        settings_feature_locked_desc: "自定义AI提供商仅在Pro、OpenAI和企业计划中可用。升级您的计划以使用自己的API密钥。",
        settings_upgrade_plan: "升级计划",
        settings_pro_required: "需要Pro+",
        settings_buy_more: "购买更多积分",
        settings_no_activity: "暂无积分活动",
        
        // Theme
        theme_primary: "主色",
        theme_secondary: "次色",
        theme_background: "背景色",
        theme_card: "卡片色",
        theme_text: "文字色",
        theme_hover: "悬停色",
        theme_credits: "积分色",
        theme_bg_image: "背景图片URL",
        theme_reset: "重置",
        theme_preview: "预览",
        theme_save: "保存主题",
        
        // Plans
        plan_free: "免费",
        plan_starter: "入门",
        plan_pro: "专业",
        plan_openai: "OpenAI",
        plan_enterprise: "企业",
        plan_daily_credits: "积分/天",
        plan_workspaces: "工作区",
        plan_own_api_keys: "自有API密钥",
        
        // Assistant
        assistant_title: "LittleHelper AI",
        assistant_greeting: "你好！我是LittleHelper。今天有什么可以帮助您？",
        assistant_placeholder: "问我任何问题...",
        assistant_new_conversation: "新对话",
        assistant_history: "对话历史",
        assistant_close: "关闭助手",
        
        // TOS
        tos_title: "服务条款",
        tos_accept: "我接受条款",
        tos_decline: "拒绝",
        tos_important: "重要通知",
        tos_important_desc: "LittleHelper AI使用人工智能生成代码。您有责任在使用前审查和测试所有生成的代码。我们对使用AI生成内容产生的任何问题不承担责任。",
        
        // FAQ
        faq_title: "常见问题",
        faq_diff_plans_addons: "计划和附加积分有什么区别？",
        faq_what_credits: "积分用来做什么？",
        faq_credits_expire: "附加积分会过期吗？",
        faq_own_api_key: "我可以使用自己的API密钥吗？",
        faq_change_plan: "我可以随时更改计划吗？",
        
        // Errors
        error_login_failed: "登录失败。请重试。",
        error_register_failed: "注册失败。请重试。",
        error_invalid_credentials: "邮箱或密码无效",
        error_insufficient_credits: "积分不足。请购买更多积分继续使用。",
    },
    ja: {
        // Navigation
        nav_dashboard: "ダッシュボード",
        nav_credits: "クレジット",
        nav_settings: "設定",
        nav_admin: "管理",
        nav_logout: "ログアウト",
        nav_profile: "プロフィールと設定",
        nav_buy_credits: "クレジットを購入",
        nav_back_dashboard: "ダッシュボードに戻る",
        nav_back_home: "ホームに戻る",
        
        // Common
        common_save: "保存",
        common_cancel: "キャンセル",
        common_delete: "削除",
        common_edit: "編集",
        common_create: "作成",
        common_loading: "読み込み中...",
        common_saving: "保存中...",
        common_error: "エラー",
        common_success: "保存しました",
        common_subscribe: "購読する",
        common_buy_now: "今すぐ購入",
        common_current_plan: "現在のプラン",
        
        // Auth
        auth_login: "ログイン",
        auth_register: "登録",
        auth_email: "メール",
        auth_password: "パスワード",
        auth_name: "氏名",
        auth_welcome_back: "おかえりなさい",
        auth_create_account: "アカウント作成",
        auth_sign_in: "サインイン",
        
        // Dashboard
        dashboard_title: "プロジェクト",
        dashboard_new_project: "新規プロジェクト",
        dashboard_no_projects: "プロジェクトがありません",
        dashboard_search_projects: "プロジェクトを検索...",
        
        // Workspace
        workspace_chat: "チャット",
        workspace_output: "出力",
        workspace_todo: "ToDo",
        workspace_files: "ファイル",
        workspace_multi_agent: "マルチエージェントモード",
        workspace_build: "ビルド",
        workspace_run: "実行",
        
        // Credits
        credits_title: "プランとクレジット",
        credits_monthly_plans: "月額プラン",
        credits_addon_credits: "追加クレジット",
        
        // Settings
        settings_title: "プロフィールと設定",
        settings_profile: "プロフィール情報",
        settings_theme: "テーマのカスタマイズ",
        settings_language: "言語",
        settings_change_password: "パスワード変更",
        settings_security: "セキュリティ",
        
        // Plans
        plan_free: "無料",
        plan_daily_credits: "クレジット/日",
        
        // Assistant
        assistant_title: "LittleHelper AI",
        assistant_greeting: "こんにちは！LittleHelperです。今日は何をお手伝いしましょうか？",
        
        // TOS
        tos_title: "利用規約",
        tos_accept: "規約に同意します",
        
        // FAQ
        faq_title: "よくある質問",
    }
};

const SUPPORTED_LANGUAGES = [
    { code: 'en', name: 'English', flag: '🇺🇸' },
    { code: 'es', name: 'Español', flag: '🇪🇸' },
    { code: 'fr', name: 'Français', flag: '🇫🇷' },
    { code: 'de', name: 'Deutsch', flag: '🇩🇪' },
    { code: 'zh', name: '中文', flag: '🇨🇳' },
    { code: 'ja', name: '日本語', flag: '🇯🇵' }
];

const I18nContext = createContext(null);

// Get initial language synchronously
const getInitialLanguage = () => {
    if (typeof window !== 'undefined') {
        const savedLang = localStorage.getItem('userLanguage');
        if (savedLang && translations[savedLang]) {
            return savedLang;
        }
    }
    return 'en';
};

export const I18nProvider = ({ children }) => {
    const [language, setLanguageState] = useState(getInitialLanguage);
    const [loading] = useState(false);

    const setLanguage = async (lang) => {
        if (!translations[lang]) return;
        
        setLanguageState(lang);
        localStorage.setItem('userLanguage', lang);
        
        // Save to backend
        try {
            await authAPI.updateLanguage(lang);
        } catch (error) {
            console.error('Failed to save language to server:', error);
        }
    };

    // Translation function with fallback
    const t = (key, fallback = null) => {
        const langStrings = translations[language] || translations['en'];
        return langStrings[key] || translations['en'][key] || fallback || key;
    };

    return (
        <I18nContext.Provider value={{ 
            language, 
            setLanguage, 
            t, 
            loading,
            supportedLanguages: SUPPORTED_LANGUAGES 
        }}>
            {children}
        </I18nContext.Provider>
    );
};

export const useI18n = () => {
    const context = useContext(I18nContext);
    if (!context) {
        throw new Error('useI18n must be used within an I18nProvider');
    }
    return context;
};

export default I18nContext;
