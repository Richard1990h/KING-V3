import { useState, useEffect, useRef, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
    Shield, Heart, Zap, Target, Trophy, RefreshCw, Volume2, VolumeX,
    Crosshair, Users, Wrench, Swords, Crown, Skull, ChevronRight,
    Pause, Play, ShoppingBag, X, AlertTriangle, Star, Move
} from 'lucide-react';
import { Button } from './ui/button';

// ============================================================================
// ENHANCED GAME CONSTANTS
// ============================================================================
const CANVAS_WIDTH = 900;
const CANVAS_HEIGHT = 550;
const BASE_X = 60;
const BASE_WIDTH = 70;
const PLAYER_SIZE = 22;
const SPAWN_X = CANVAS_WIDTH + 50;
const GROUND_Y = CANVAS_HEIGHT - 40;

// Player movement bounds
const PLAYER_MIN_X = BASE_X + BASE_WIDTH + 40;
const PLAYER_MAX_X = CANVAS_WIDTH - 50;
const PLAYER_MIN_Y = 120;
const PLAYER_MAX_Y = GROUND_Y - PLAYER_SIZE;
const PLAYER_SPEED = 5;

const ENEMY_TYPES = {
    runner: { hp: 30, speed: 3.5, damage: 10, credits: 15, color: '#22c55e', glowColor: '#4ade80', size: 14, name: 'Runner', shape: 'triangle' },
    grunt: { hp: 55, speed: 1.8, damage: 15, credits: 20, color: '#eab308', glowColor: '#facc15', size: 18, name: 'Grunt', shape: 'circle' },
    tank: { hp: 180, speed: 0.9, damage: 35, credits: 55, color: '#ef4444', glowColor: '#f87171', size: 28, name: 'Tank', shape: 'square' },
    ranged: { hp: 45, speed: 1.2, damage: 22, credits: 35, color: '#8b5cf6', glowColor: '#a78bfa', size: 16, range: 220, name: 'Shooter', shape: 'diamond' },
    exploder: { hp: 70, speed: 2.2, damage: 55, credits: 45, color: '#f97316', glowColor: '#fb923c', size: 20, explosive: true, name: 'Exploder', shape: 'circle' },
    healer: { hp: 50, speed: 1.4, damage: 5, credits: 40, color: '#ec4899', glowColor: '#f472b6', size: 16, heals: true, name: 'Healer', shape: 'cross' },
    boss: { hp: 600, speed: 0.6, damage: 60, credits: 250, color: '#dc2626', glowColor: '#ef4444', size: 45, name: 'BOSS', shape: 'hexagon' }
};

const TROOP_TYPES = {
    soldier: { hp: 90, damage: 18, fireRate: 750, range: 280, cost: 100, color: '#3b82f6', name: 'Soldier' },
    sniper: { hp: 55, damage: 50, fireRate: 1400, range: 450, cost: 220, color: '#8b5cf6', name: 'Sniper' },
    heavy: { hp: 220, damage: 30, fireRate: 1100, range: 220, cost: 280, color: '#ef4444', name: 'Heavy' },
    medic: { hp: 70, damage: 5, fireRate: 1800, range: 180, cost: 160, color: '#22c55e', heals: true, name: 'Medic' },
    engineer: { hp: 80, damage: 12, fireRate: 900, range: 200, cost: 200, color: '#eab308', repairs: true, name: 'Engineer' }
};

const UPGRADES = {
    playerDamage: { name: 'Weapon Power', baseCost: 50, perLevel: 30, maxLevel: 10, icon: '🔫' },
    playerHealth: { name: 'Max Health', baseCost: 75, perLevel: 40, maxLevel: 10, icon: '❤️' },
    playerArmor: { name: 'Armor Plating', baseCost: 60, perLevel: 35, maxLevel: 10, icon: '🛡️' },
    playerReload: { name: 'Fire Rate', baseCost: 80, perLevel: 50, maxLevel: 8, icon: '⚡' },
    baseHealth: { name: 'Reinforced Walls', baseCost: 100, perLevel: 60, maxLevel: 10, icon: '🏰' },
    baseTurret: { name: 'Auto Turret', baseCost: 300, perLevel: 150, maxLevel: 5, icon: '🔧' },
    baseRegen: { name: 'Repair Drones', baseCost: 200, perLevel: 100, maxLevel: 5, icon: '🔩' }
};

// ============================================================================
// ENHANCED RENDERING HELPERS
// ============================================================================
function drawGlowCircle(ctx, x, y, radius, color, glowColor, glowRadius = 8) {
    // Glow effect
    const gradient = ctx.createRadialGradient(x, y, radius * 0.5, x, y, radius + glowRadius);
    gradient.addColorStop(0, glowColor + '80');
    gradient.addColorStop(0.5, glowColor + '40');
    gradient.addColorStop(1, 'transparent');
    ctx.fillStyle = gradient;
    ctx.beginPath();
    ctx.arc(x, y, radius + glowRadius, 0, Math.PI * 2);
    ctx.fill();
    
    // Main body
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    ctx.fill();
    
    // Highlight
    const highlight = ctx.createRadialGradient(x - radius * 0.3, y - radius * 0.3, 0, x, y, radius);
    highlight.addColorStop(0, 'rgba(255,255,255,0.4)');
    highlight.addColorStop(1, 'transparent');
    ctx.fillStyle = highlight;
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    ctx.fill();
}

function drawShape(ctx, x, y, size, shape, color, glowColor) {
    ctx.save();
    
    // Glow
    ctx.shadowColor = glowColor;
    ctx.shadowBlur = 12;
    
    ctx.fillStyle = color;
    ctx.beginPath();
    
    switch (shape) {
        case 'triangle':
            ctx.moveTo(x, y - size);
            ctx.lineTo(x + size, y + size * 0.8);
            ctx.lineTo(x - size, y + size * 0.8);
            ctx.closePath();
            break;
        case 'square':
            ctx.fillRect(x - size * 0.8, y - size * 0.8, size * 1.6, size * 1.6);
            break;
        case 'diamond':
            ctx.moveTo(x, y - size);
            ctx.lineTo(x + size, y);
            ctx.lineTo(x, y + size);
            ctx.lineTo(x - size, y);
            ctx.closePath();
            break;
        case 'hexagon':
            for (let i = 0; i < 6; i++) {
                const angle = (i * Math.PI) / 3 - Math.PI / 2;
                const px = x + size * Math.cos(angle);
                const py = y + size * Math.sin(angle);
                if (i === 0) ctx.moveTo(px, py);
                else ctx.lineTo(px, py);
            }
            ctx.closePath();
            break;
        case 'cross':
            const w = size * 0.4;
            ctx.fillRect(x - w, y - size, w * 2, size * 2);
            ctx.fillRect(x - size, y - w, size * 2, w * 2);
            break;
        default:
            ctx.arc(x, y, size, 0, Math.PI * 2);
    }
    
    ctx.fill();
    ctx.restore();
    
    // Inner highlight
    if (shape !== 'cross') {
        const gradient = ctx.createRadialGradient(x - size * 0.2, y - size * 0.2, 0, x, y, size);
        gradient.addColorStop(0, 'rgba(255,255,255,0.3)');
        gradient.addColorStop(1, 'transparent');
        ctx.fillStyle = gradient;
        ctx.beginPath();
        ctx.arc(x, y, size * 0.8, 0, Math.PI * 2);
        ctx.fill();
    }
}

function drawHealthBar(ctx, x, y, width, height, current, max, showText = false) {
    const percent = current / max;
    
    // Background
    ctx.fillStyle = '#1f2937';
    ctx.fillRect(x - width / 2, y, width, height);
    
    // Border
    ctx.strokeStyle = '#374151';
    ctx.lineWidth = 1;
    ctx.strokeRect(x - width / 2, y, width, height);
    
    // Fill with gradient
    const gradient = ctx.createLinearGradient(x - width / 2, y, x + width / 2, y);
    if (percent > 0.5) {
        gradient.addColorStop(0, '#22c55e');
        gradient.addColorStop(1, '#16a34a');
    } else if (percent > 0.25) {
        gradient.addColorStop(0, '#eab308');
        gradient.addColorStop(1, '#ca8a04');
    } else {
        gradient.addColorStop(0, '#ef4444');
        gradient.addColorStop(1, '#dc2626');
    }
    
    ctx.fillStyle = gradient;
    ctx.fillRect(x - width / 2 + 1, y + 1, (width - 2) * percent, height - 2);
    
    if (showText) {
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 10px monospace';
        ctx.textAlign = 'center';
        ctx.fillText(`${Math.ceil(current)}/${max}`, x, y + height + 12);
    }
}

function drawBackground(ctx, time) {
    // Gradient background
    const gradient = ctx.createLinearGradient(0, 0, 0, CANVAS_HEIGHT);
    gradient.addColorStop(0, '#0c1222');
    gradient.addColorStop(0.5, '#0f172a');
    gradient.addColorStop(1, '#1e293b');
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    
    // Animated stars
    ctx.fillStyle = 'rgba(255,255,255,0.3)';
    for (let i = 0; i < 50; i++) {
        const x = (i * 137 + time * 0.01) % CANVAS_WIDTH;
        const y = (i * 89) % (CANVAS_HEIGHT * 0.6);
        const size = (i % 3) + 1;
        ctx.beginPath();
        ctx.arc(x, y, size * (0.5 + 0.5 * Math.sin(time * 0.003 + i)), 0, Math.PI * 2);
        ctx.fill();
    }
    
    // Grid
    ctx.strokeStyle = 'rgba(100, 150, 255, 0.05)';
    ctx.lineWidth = 1;
    for (let i = 0; i < CANVAS_WIDTH; i += 50) {
        ctx.beginPath();
        ctx.moveTo(i, 0);
        ctx.lineTo(i, CANVAS_HEIGHT);
        ctx.stroke();
    }
    for (let i = 0; i < CANVAS_HEIGHT; i += 50) {
        ctx.beginPath();
        ctx.moveTo(0, i);
        ctx.lineTo(CANVAS_WIDTH, i);
        ctx.stroke();
    }
    
    // Ground
    const groundGradient = ctx.createLinearGradient(0, GROUND_Y, 0, CANVAS_HEIGHT);
    groundGradient.addColorStop(0, '#1e3a5f');
    groundGradient.addColorStop(1, '#0f172a');
    ctx.fillStyle = groundGradient;
    ctx.fillRect(0, GROUND_Y, CANVAS_WIDTH, CANVAS_HEIGHT - GROUND_Y);
    
    // Ground line
    ctx.strokeStyle = '#3b82f6';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(0, GROUND_Y);
    ctx.lineTo(CANVAS_WIDTH, GROUND_Y);
    ctx.stroke();
}

function drawBase(ctx, hp, maxHp, turretLevel, time) {
    const percent = hp / maxHp;
    
    // Base shadow
    ctx.fillStyle = 'rgba(0,0,0,0.3)';
    ctx.fillRect(BASE_X + 5, GROUND_Y - 5, BASE_WIDTH, 8);
    
    // Base body with gradient
    const baseGradient = ctx.createLinearGradient(BASE_X, GROUND_Y - 160, BASE_X + BASE_WIDTH, GROUND_Y);
    if (percent > 0.5) {
        baseGradient.addColorStop(0, '#3b82f6');
        baseGradient.addColorStop(1, '#1d4ed8');
    } else if (percent > 0.25) {
        baseGradient.addColorStop(0, '#eab308');
        baseGradient.addColorStop(1, '#ca8a04');
    } else {
        baseGradient.addColorStop(0, '#ef4444');
        baseGradient.addColorStop(1, '#b91c1c');
    }
    
    ctx.fillStyle = baseGradient;
    ctx.fillRect(BASE_X, GROUND_Y - 160, BASE_WIDTH, 160);
    
    // Base details
    ctx.fillStyle = '#0f172a';
    ctx.fillRect(BASE_X + 10, GROUND_Y - 145, BASE_WIDTH - 20, 80);
    ctx.fillRect(BASE_X + 15, GROUND_Y - 55, BASE_WIDTH - 30, 35);
    
    // Windows with glow
    ctx.fillStyle = percent > 0.25 ? '#22d3ee' : '#f87171';
    ctx.shadowColor = percent > 0.25 ? '#22d3ee' : '#f87171';
    ctx.shadowBlur = 10;
    ctx.fillRect(BASE_X + 15, GROUND_Y - 135, 15, 20);
    ctx.fillRect(BASE_X + BASE_WIDTH - 30, GROUND_Y - 135, 15, 20);
    ctx.fillRect(BASE_X + 15, GROUND_Y - 105, 15, 20);
    ctx.fillRect(BASE_X + BASE_WIDTH - 30, GROUND_Y - 105, 15, 20);
    ctx.shadowBlur = 0;
    
    // Turret
    if (turretLevel > 0) {
        ctx.fillStyle = '#fbbf24';
        ctx.shadowColor = '#fbbf24';
        ctx.shadowBlur = 8;
        
        // Turret base
        ctx.fillRect(BASE_X + BASE_WIDTH - 20, GROUND_Y - 175, 25, 15);
        
        // Turret barrel (animated)
        const barrelAngle = Math.sin(time * 0.002) * 0.2;
        ctx.save();
        ctx.translate(BASE_X + BASE_WIDTH - 8, GROUND_Y - 170);
        ctx.rotate(barrelAngle);
        ctx.fillRect(0, -3, 20 + turretLevel * 3, 6);
        ctx.restore();
        
        ctx.shadowBlur = 0;
    }
    
    // Health bar
    drawHealthBar(ctx, BASE_X + BASE_WIDTH / 2, GROUND_Y - 180, BASE_WIDTH + 10, 8, hp, maxHp);
    
    // Base label
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 10px monospace';
    ctx.textAlign = 'center';
    ctx.fillText('BASE', BASE_X + BASE_WIDTH / 2, GROUND_Y - 190);
}

function drawPlayer(ctx, x, y, hp, maxHp, armor, time) {
    // Player shadow
    ctx.fillStyle = 'rgba(0,0,0,0.3)';
    ctx.beginPath();
    ctx.ellipse(x, GROUND_Y - 2, PLAYER_SIZE, 6, 0, 0, Math.PI * 2);
    ctx.fill();
    
    // Shield effect if armor > 0
    if (armor > 0) {
        ctx.strokeStyle = `rgba(34, 211, 238, ${0.3 + Math.sin(time * 0.005) * 0.2})`;
        ctx.lineWidth = 2 + armor * 0.3;
        ctx.beginPath();
        ctx.arc(x, y, PLAYER_SIZE + 8, 0, Math.PI * 2);
        ctx.stroke();
    }
    
    // Player body with enhanced visuals
    drawGlowCircle(ctx, x, y, PLAYER_SIZE, '#22d3ee', '#06b6d4', 10);
    
    // Inner core
    const coreGradient = ctx.createRadialGradient(x, y, 0, x, y, PLAYER_SIZE * 0.5);
    coreGradient.addColorStop(0, '#ffffff');
    coreGradient.addColorStop(0.5, '#0891b2');
    coreGradient.addColorStop(1, '#0e7490');
    ctx.fillStyle = coreGradient;
    ctx.beginPath();
    ctx.arc(x, y, PLAYER_SIZE * 0.5, 0, Math.PI * 2);
    ctx.fill();
    
    // Rotating ring
    ctx.strokeStyle = '#22d3ee';
    ctx.lineWidth = 2;
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(time * 0.003);
    ctx.beginPath();
    ctx.arc(0, 0, PLAYER_SIZE * 0.7, 0, Math.PI * 1.5);
    ctx.stroke();
    ctx.restore();
    
    // Health bar
    drawHealthBar(ctx, x, y - PLAYER_SIZE - 15, 50, 6, hp, maxHp);
    
    // Player label
    ctx.fillStyle = '#22d3ee';
    ctx.font = 'bold 9px monospace';
    ctx.textAlign = 'center';
    ctx.fillText('PLAYER', x, y - PLAYER_SIZE - 22);
}

function drawCrosshair(ctx, x, y, time) {
    const pulse = 1 + Math.sin(time * 0.01) * 0.1;
    const size = 16 * pulse;
    
    ctx.strokeStyle = '#f472b6';
    ctx.lineWidth = 2;
    ctx.shadowColor = '#f472b6';
    ctx.shadowBlur = 8;
    
    // Outer circle
    ctx.beginPath();
    ctx.arc(x, y, size, 0, Math.PI * 2);
    ctx.stroke();
    
    // Inner dot
    ctx.fillStyle = '#f472b6';
    ctx.beginPath();
    ctx.arc(x, y, 3, 0, Math.PI * 2);
    ctx.fill();
    
    // Crosshair lines
    const gap = 8;
    const lineLen = 12;
    ctx.beginPath();
    ctx.moveTo(x - size - lineLen, y);
    ctx.lineTo(x - gap, y);
    ctx.moveTo(x + gap, y);
    ctx.lineTo(x + size + lineLen, y);
    ctx.moveTo(x, y - size - lineLen);
    ctx.lineTo(x, y - gap);
    ctx.moveTo(x, y + gap);
    ctx.lineTo(x, y + size + lineLen);
    ctx.stroke();
    
    ctx.shadowBlur = 0;
}

// ============================================================================
// GAME CLASSES
// ============================================================================
class Enemy {
    constructor(type, waveNumber) {
        const config = { ...ENEMY_TYPES[type] };
        const scale = 1 + (waveNumber - 1) * 0.12;
        config.hp = Math.floor(config.hp * scale);
        config.damage = Math.floor(config.damage * (1 + (waveNumber - 1) * 0.06));
        
        this.x = SPAWN_X;
        this.y = GROUND_Y - 40 - Math.random() * 80;
        this.maxHp = config.hp;
        this.hp = config.hp;
        this.damage = config.damage;
        this.speed = config.speed;
        this.color = config.color;
        this.glowColor = config.glowColor;
        this.size = config.size;
        this.shape = config.shape;
        this.type = type;
        this.credits = Math.floor(config.credits * (1 + (waveNumber - 1) * 0.12));
        this.range = config.range || 0;
        this.explosive = config.explosive || false;
        this.heals = config.heals || false;
        this.lastShot = 0;
        this.lastHeal = 0;
        this.dead = false;
        this.hitFlash = 0;
    }
    
    takeDamage(amount) {
        this.hp -= amount;
        this.hitFlash = 5;
        if (this.hp <= 0) {
            this.hp = 0;
            this.dead = true;
        }
    }
    
    update(base, player, enemies, now) {
        if (this.dead) return null;
        if (this.hitFlash > 0) this.hitFlash--;
        
        // Healer logic
        if (this.heals && now - this.lastHeal > 2000) {
            const wounded = enemies.find(e => !e.dead && e !== this && e.hp < e.maxHp);
            if (wounded && Math.abs(wounded.x - this.x) < 150) {
                wounded.hp = Math.min(wounded.maxHp, wounded.hp + 25);
                this.lastHeal = now;
                return { type: 'heal', target: wounded };
            }
        }
        
        // Ranged enemy logic
        if (this.range > 0) {
            const distToPlayer = Math.abs(this.x - player.x);
            if (distToPlayer < this.range && now - this.lastShot > 1500) {
                this.lastShot = now;
                return { type: 'enemyShoot', target: player };
            }
            if (distToPlayer > this.range * 0.7) {
                this.x -= this.speed;
            }
        } else {
            // Melee - move towards base
            if (this.x > BASE_X + BASE_WIDTH + 25) {
                this.x -= this.speed;
            } else {
                return { type: 'attackBase', damage: this.damage };
            }
        }
        return null;
    }
    
    draw(ctx, time) {
        if (this.dead) return;
        
        // Flash white when hit
        const color = this.hitFlash > 0 ? '#ffffff' : this.color;
        const glow = this.hitFlash > 0 ? '#ffffff' : this.glowColor;
        
        drawShape(ctx, this.x, this.y, this.size, this.shape, color, glow);
        
        // Health bar
        drawHealthBar(ctx, this.x, this.y - this.size - 12, this.size * 2, 4, this.hp, this.maxHp);
        
        // Type indicators
        if (this.heals) {
            ctx.fillStyle = '#fff';
            ctx.font = 'bold 12px Arial';
            ctx.textAlign = 'center';
            ctx.fillText('+', this.x, this.y + 4);
        }
        if (this.explosive) {
            ctx.fillStyle = '#fff';
            ctx.font = '10px Arial';
            ctx.textAlign = 'center';
            ctx.fillText('💥', this.x, this.y + 4);
        }
        if (this.type === 'boss') {
            ctx.fillStyle = '#fff';
            ctx.font = 'bold 10px monospace';
            ctx.textAlign = 'center';
            ctx.fillText('BOSS', this.x, this.y - this.size - 20);
        }
    }
}

class Troop {
    constructor(type, x, y) {
        const config = TROOP_TYPES[type];
        this.x = x;
        this.y = y;
        this.maxHp = config.hp;
        this.hp = config.hp;
        this.damage = config.damage;
        this.fireRate = config.fireRate;
        this.range = config.range;
        this.color = config.color;
        this.type = type;
        this.heals = config.heals || false;
        this.repairs = config.repairs || false;
        this.lastShot = 0;
        this.level = 1;
        this.dead = false;
    }
    
    update(enemies, player, base, now) {
        if (this.dead) return null;
        
        if (this.heals && now - this.lastShot > this.fireRate) {
            if (player.hp < player.maxHp) {
                player.hp = Math.min(player.maxHp, player.hp + 12);
                this.lastShot = now;
                return { type: 'heal', target: 'player' };
            }
        }
        
        if (this.repairs && now - this.lastShot > this.fireRate) {
            if (base.hp < base.maxHp) {
                this.lastShot = now;
                return { type: 'repair', amount: 8 };
            }
        }
        
        if (!this.heals && !this.repairs && now - this.lastShot > this.fireRate) {
            const target = enemies.find(e => !e.dead && Math.abs(e.x - this.x) < this.range);
            if (target) {
                this.lastShot = now;
                return { type: 'shoot', target, damage: this.damage * this.level };
            }
        }
        
        return null;
    }
    
    draw(ctx) {
        if (this.dead) return;
        
        // Troop body (square with rounded corners effect)
        ctx.fillStyle = this.color;
        ctx.shadowColor = this.color;
        ctx.shadowBlur = 8;
        ctx.fillRect(this.x - 8, this.y - 8, 16, 16);
        ctx.shadowBlur = 0;
        
        // Health bar
        drawHealthBar(ctx, this.x, this.y - 15, 20, 3, this.hp, this.maxHp);
        
        // Level
        if (this.level > 1) {
            ctx.fillStyle = '#fbbf24';
            ctx.font = 'bold 8px Arial';
            ctx.textAlign = 'center';
            ctx.fillText(`★${this.level}`, this.x, this.y + 18);
        }
    }
}

class Projectile {
    constructor(x, y, targetX, targetY, damage, isEnemy = false, color = null) {
        this.x = x;
        this.y = y;
        this.damage = damage;
        this.isEnemy = isEnemy;
        this.color = color || (isEnemy ? '#ef4444' : '#fbbf24');
        this.speed = isEnemy ? 10 : 14;
        this.size = isEnemy ? 5 : 6;
        this.dead = false;
        this.trail = [];
        
        const dx = targetX - x;
        const dy = targetY - y;
        const dist = Math.sqrt(dx * dx + dy * dy);
        this.vx = (dx / dist) * this.speed;
        this.vy = (dy / dist) * this.speed;
    }
    
    update() {
        this.trail.push({ x: this.x, y: this.y });
        if (this.trail.length > 8) this.trail.shift();
        
        this.x += this.vx;
        this.y += this.vy;
        
        if (this.x < 0 || this.x > CANVAS_WIDTH || this.y < 0 || this.y > CANVAS_HEIGHT) {
            this.dead = true;
        }
    }
    
    draw(ctx) {
        // Trail
        this.trail.forEach((pos, i) => {
            const alpha = i / this.trail.length;
            ctx.fillStyle = this.color + Math.floor(alpha * 128).toString(16).padStart(2, '0');
            ctx.beginPath();
            ctx.arc(pos.x, pos.y, this.size * alpha, 0, Math.PI * 2);
            ctx.fill();
        });
        
        // Main projectile
        ctx.fillStyle = this.color;
        ctx.shadowColor = this.color;
        ctx.shadowBlur = 10;
        ctx.beginPath();
        ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
        ctx.fill();
        ctx.shadowBlur = 0;
    }
}

class Particle {
    constructor(x, y, color, type = 'explosion') {
        this.x = x;
        this.y = y;
        this.color = color;
        this.type = type;
        this.vx = (Math.random() - 0.5) * (type === 'explosion' ? 10 : 5);
        this.vy = (Math.random() - 0.5) * (type === 'explosion' ? 10 : 5);
        this.life = 1;
        this.size = type === 'explosion' ? Math.random() * 8 + 3 : Math.random() * 4 + 2;
    }
    
    update() {
        this.x += this.vx;
        this.y += this.vy;
        this.vy += 0.25;
        this.vx *= 0.98;
        this.life -= 0.025;
        return this.life > 0;
    }
    
    draw(ctx) {
        ctx.globalAlpha = this.life;
        ctx.fillStyle = this.color;
        ctx.shadowColor = this.color;
        ctx.shadowBlur = 5;
        ctx.beginPath();
        ctx.arc(this.x, this.y, this.size * this.life, 0, Math.PI * 2);
        ctx.fill();
        ctx.shadowBlur = 0;
        ctx.globalAlpha = 1;
    }
}

// ============================================================================
// WAVE GENERATOR
// ============================================================================
function generateWave(waveNumber) {
    const enemies = [];
    const baseCount = 4 + Math.floor(waveNumber * 1.6);
    
    if (waveNumber % 5 === 0) {
        enemies.push({ type: 'boss', delay: 2000 });
    }
    
    for (let i = 0; i < baseCount; i++) {
        let type = 'grunt';
        const roll = Math.random();
        
        if (waveNumber >= 2 && roll < 0.35) type = 'runner';
        if (waveNumber >= 4 && roll < 0.2) type = 'tank';
        if (waveNumber >= 6 && roll < 0.18) type = 'ranged';
        if (waveNumber >= 8 && roll < 0.12) type = 'exploder';
        if (waveNumber >= 10 && roll < 0.1) type = 'healer';
        
        enemies.push({ type, delay: i * (700 - Math.min(waveNumber * 25, 400)) });
    }
    
    return enemies;
}

// ============================================================================
// MAIN GAME COMPONENT
// ============================================================================
export function WaveDefenseGame({ onRetryConnection }) {
    const canvasRef = useRef(null);
    const gameRef = useRef(null);
    const keysRef = useRef({});
    
    const [gameState, setGameState] = useState('menu');
    const [wave, setWave] = useState(1);
    const [credits, setCredits] = useState(100);
    const [totalCredits, setTotalCredits] = useState(0);
    const [kills, setKills] = useState(0);
    const [soundEnabled, setSoundEnabled] = useState(true);
    
    const [playerHp, setPlayerHp] = useState(100);
    const [playerMaxHp, setPlayerMaxHp] = useState(100);
    const [baseHp, setBaseHp] = useState(500);
    const [baseMaxHp, setBaseMaxHp] = useState(500);
    
    const [upgrades, setUpgrades] = useState({
        playerDamage: 0, playerHealth: 0, playerArmor: 0, playerReload: 0,
        baseHealth: 0, baseTurret: 0, baseRegen: 0
    });
    
    const [troops, setTroops] = useState([]);
    const [highScore, setHighScore] = useState(() => {
        return parseInt(localStorage.getItem('waveDefenseHighWave') || '0');
    });

    const playSound = useCallback((type) => {
        if (!soundEnabled) return;
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(ctx.destination);
            
            const sounds = {
                shoot: { freq: 440, type: 'square', dur: 0.04 },
                hit: { freq: 200, type: 'sawtooth', dur: 0.08 },
                kill: { freq: 660, type: 'sine', dur: 0.12 },
                levelUp: { freq: 880, type: 'sine', dur: 0.2 },
                damage: { freq: 130, type: 'sawtooth', dur: 0.12 },
                buy: { freq: 520, type: 'sine', dur: 0.08 }
            };
            
            const s = sounds[type] || sounds.shoot;
            osc.frequency.value = s.freq;
            osc.type = s.type;
            gain.gain.value = 0.08;
            osc.start();
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + s.dur);
            osc.stop(ctx.currentTime + s.dur);
        } catch(e) {}
    }, [soundEnabled]);

    const getPlayerDamage = useCallback(() => 22 + upgrades.playerDamage * 6, [upgrades.playerDamage]);
    const getPlayerFireRate = useCallback(() => Math.max(120, 350 - upgrades.playerReload * 25), [upgrades.playerReload]);
    const getPlayerArmor = useCallback(() => upgrades.playerArmor * 2.5, [upgrades.playerArmor]);
    const getTurretDamage = useCallback(() => upgrades.baseTurret * 18, [upgrades.baseTurret]);
    const getBaseRegen = () => upgrades.baseRegen * 3;

    const startGame = useCallback(() => {
        setGameState('playing');
        setWave(1);
        setCredits(100);
        setTotalCredits(0);
        setKills(0);
        setPlayerHp(100);
        setPlayerMaxHp(100);
        setBaseHp(500);
        setBaseMaxHp(500);
        setUpgrades({
            playerDamage: 0, playerHealth: 0, playerArmor: 0, playerReload: 0,
            baseHealth: 0, baseTurret: 0, baseRegen: 0
        });
        setTroops([]);
        
        gameRef.current = {
            enemies: [],
            projectiles: [],
            particles: [],
            waveEnemies: generateWave(1),
            waveStartTime: Date.now(),
            player: { x: 300, y: GROUND_Y - 50, lastShot: 0 },
            mouseX: 400,
            mouseY: 300,
            waveComplete: false,
            lastTurretShot: 0
        };
    }, []);

    const buyUpgrade = (key) => {
        const upgrade = UPGRADES[key];
        const level = upgrades[key];
        if (level >= upgrade.maxLevel) return;
        
        const cost = upgrade.baseCost + level * upgrade.perLevel;
        if (credits < cost) return;
        
        setCredits(c => c - cost);
        setUpgrades(u => ({ ...u, [key]: u[key] + 1 }));
        playSound('buy');
        
        if (key === 'playerHealth') {
            const newMax = 100 + (upgrades.playerHealth + 1) * 25;
            setPlayerMaxHp(newMax);
            setPlayerHp(h => Math.min(h + 25, newMax));
        }
        if (key === 'baseHealth') {
            const newMax = 500 + (upgrades.baseHealth + 1) * 60;
            setBaseMaxHp(newMax);
            setBaseHp(h => Math.min(h + 60, newMax));
        }
    };

    const buyTroop = (type) => {
        const config = TROOP_TYPES[type];
        if (credits < config.cost) return;
        if (troops.length >= 5) return;
        
        setCredits(c => c - config.cost);
        const newTroop = new Troop(type, 140 + troops.length * 45, GROUND_Y - 50);
        setTroops(t => [...t, newTroop]);
        playSound('buy');
    };

    const startNextWave = useCallback(() => {
        const nextWave = wave + 1;
        if (nextWave > 20) {
            setGameState('victory');
            if (wave > highScore) {
                setHighScore(wave);
                localStorage.setItem('waveDefenseHighWave', wave.toString());
            }
            return;
        }
        
        setWave(nextWave);
        setGameState('playing');
        gameRef.current.waveEnemies = generateWave(nextWave);
        gameRef.current.waveStartTime = Date.now();
        gameRef.current.waveComplete = false;
        gameRef.current.enemies = [];
        
        setBaseHp(h => Math.min(baseMaxHp, h + getBaseRegen() * 12));
        playSound('levelUp');
    }, [wave, highScore, baseMaxHp, getBaseRegen, playSound]);

    // Keyboard handlers
    useEffect(() => {
        const handleKeyDown = (e) => {
            keysRef.current[e.key.toLowerCase()] = true;
            if (['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(e.key.toLowerCase())) {
                e.preventDefault();
            }
        };
        const handleKeyUp = (e) => {
            keysRef.current[e.key.toLowerCase()] = false;
        };
        
        window.addEventListener('keydown', handleKeyDown);
        window.addEventListener('keyup', handleKeyUp);
        
        return () => {
            window.removeEventListener('keydown', handleKeyDown);
            window.removeEventListener('keyup', handleKeyUp);
        };
    }, []);

    // Main game loop
    useEffect(() => {
        if (gameState !== 'playing') return;
        
        const canvas = canvasRef.current;
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const game = gameRef.current;
        
        let animationId;
        
        const gameLoop = () => {
            const now = Date.now();
            
            // Handle player movement
            const keys = keysRef.current;
            if (keys['w'] || keys['arrowup']) {
                game.player.y = Math.max(PLAYER_MIN_Y, game.player.y - PLAYER_SPEED);
            }
            if (keys['s'] || keys['arrowdown']) {
                game.player.y = Math.min(PLAYER_MAX_Y, game.player.y + PLAYER_SPEED);
            }
            if (keys['a'] || keys['arrowleft']) {
                game.player.x = Math.max(PLAYER_MIN_X, game.player.x - PLAYER_SPEED);
            }
            if (keys['d'] || keys['arrowright']) {
                game.player.x = Math.min(PLAYER_MAX_X, game.player.x + PLAYER_SPEED);
            }
            
            // Draw background
            drawBackground(ctx, now);
            
            // Draw base
            drawBase(ctx, baseHp, baseMaxHp, upgrades.baseTurret, now);
            
            // Spawn enemies
            const waveTime = now - game.waveStartTime;
            game.waveEnemies = game.waveEnemies.filter(we => {
                if (waveTime >= we.delay) {
                    game.enemies.push(new Enemy(we.type, wave));
                    return false;
                }
                return true;
            });
            
            // Update and draw enemies
            game.enemies.forEach(enemy => {
                const action = enemy.update(
                    { hp: baseHp },
                    game.player,
                    game.enemies,
                    now
                );
                
                if (action) {
                    if (action.type === 'attackBase') {
                        setBaseHp(h => {
                            const newHp = Math.max(0, h - action.damage);
                            if (newHp <= 0) {
                                setGameState('gameover');
                                if (wave > highScore) {
                                    setHighScore(wave);
                                    localStorage.setItem('waveDefenseHighWave', wave.toString());
                                }
                            }
                            return newHp;
                        });
                        playSound('damage');
                    }
                    if (action.type === 'enemyShoot') {
                        game.projectiles.push(new Projectile(
                            enemy.x, enemy.y,
                            game.player.x, game.player.y,
                            enemy.damage, true
                        ));
                    }
                    if (action.type === 'heal') {
                        for (let i = 0; i < 5; i++) {
                            game.particles.push(new Particle(action.target.x, action.target.y, '#ec4899', 'heal'));
                        }
                    }
                }
                enemy.draw(ctx, now);
            });
            
            // Auto turret
            if (upgrades.baseTurret > 0 && game.enemies.length > 0) {
                const target = game.enemies.find(e => !e.dead && e.x < CANVAS_WIDTH - 100);
                if (target && now - game.lastTurretShot > 800) {
                    game.projectiles.push(new Projectile(
                        BASE_X + BASE_WIDTH, GROUND_Y - 168,
                        target.x, target.y,
                        getTurretDamage(),
                        false, '#fbbf24'
                    ));
                    game.lastTurretShot = now;
                }
            }
            
            // Update troops
            troops.forEach(troop => {
                if (troop.dead) return;
                const action = troop.update(
                    game.enemies,
                    { hp: playerHp, maxHp: playerMaxHp },
                    { hp: baseHp, maxHp: baseMaxHp },
                    now
                );
                
                if (action) {
                    if (action.type === 'shoot') {
                        game.projectiles.push(new Projectile(
                            troop.x, troop.y,
                            action.target.x, action.target.y,
                            action.damage,
                            false, troop.color
                        ));
                    }
                    if (action.type === 'heal') {
                        setPlayerHp(h => Math.min(playerMaxHp, h + 12));
                        for (let i = 0; i < 3; i++) {
                            game.particles.push(new Particle(game.player.x, game.player.y, '#22c55e', 'heal'));
                        }
                    }
                    if (action.type === 'repair') {
                        setBaseHp(h => Math.min(baseMaxHp, h + action.amount));
                        for (let i = 0; i < 3; i++) {
                            game.particles.push(new Particle(BASE_X + BASE_WIDTH / 2, GROUND_Y - 100, '#eab308', 'heal'));
                        }
                    }
                }
                troop.draw(ctx);
            });
            
            // Player shooting
            if (now - game.player.lastShot > getPlayerFireRate()) {
                const target = game.enemies.find(e => !e.dead);
                if (target) {
                    game.projectiles.push(new Projectile(
                        game.player.x, game.player.y,
                        game.mouseX, game.mouseY,
                        getPlayerDamage(),
                        false, '#22d3ee'
                    ));
                    game.player.lastShot = now;
                    playSound('shoot');
                }
            }
            
            // Update projectiles
            game.projectiles = game.projectiles.filter(proj => {
                proj.update();
                if (proj.dead) return false;
                
                if (proj.isEnemy) {
                    const dist = Math.sqrt((proj.x - game.player.x) ** 2 + (proj.y - game.player.y) ** 2);
                    if (dist < PLAYER_SIZE + proj.size) {
                        const dmg = Math.max(1, proj.damage - getPlayerArmor());
                        setPlayerHp(h => {
                            const newHp = Math.max(0, h - dmg);
                            if (newHp <= 0) {
                                setGameState('gameover');
                                if (wave > highScore) {
                                    setHighScore(wave);
                                    localStorage.setItem('waveDefenseHighWave', wave.toString());
                                }
                            }
                            return newHp;
                        });
                        playSound('damage');
                        for (let i = 0; i < 5; i++) {
                            game.particles.push(new Particle(game.player.x, game.player.y, '#ef4444'));
                        }
                        return false;
                    }
                } else {
                    for (const enemy of game.enemies) {
                        if (enemy.dead) continue;
                        const dist = Math.sqrt((proj.x - enemy.x) ** 2 + (proj.y - enemy.y) ** 2);
                        if (dist < enemy.size + proj.size) {
                            enemy.takeDamage(proj.damage);
                            
                            for (let i = 0; i < 6; i++) {
                                game.particles.push(new Particle(enemy.x, enemy.y, enemy.color));
                            }
                            
                            if (enemy.dead) {
                                if (enemy.explosive) {
                                    for (let i = 0; i < 25; i++) {
                                        game.particles.push(new Particle(enemy.x, enemy.y, '#f97316'));
                                    }
                                    game.enemies.forEach(e => {
                                        if (!e.dead && e !== enemy) {
                                            const d = Math.sqrt((e.x - enemy.x) ** 2 + (e.y - enemy.y) ** 2);
                                            if (d < 90) e.takeDamage(35);
                                        }
                                    });
                                }
                                
                                setCredits(c => c + enemy.credits);
                                setTotalCredits(t => t + enemy.credits);
                                setKills(k => k + 1);
                                playSound('kill');
                            } else {
                                playSound('hit');
                            }
                            return false;
                        }
                    }
                }
                
                proj.draw(ctx);
                return true;
            });
            
            // Update particles
            game.particles = game.particles.filter(p => {
                const alive = p.update();
                if (alive) p.draw(ctx);
                return alive;
            });
            
            // Remove dead enemies
            game.enemies = game.enemies.filter(e => !e.dead);
            
            // Draw player
            drawPlayer(ctx, game.player.x, game.player.y, playerHp, playerMaxHp, getPlayerArmor(), now);
            
            // Draw crosshair
            drawCrosshair(ctx, game.mouseX, game.mouseY, now);
            
            // Check wave complete
            if (game.waveEnemies.length === 0 && game.enemies.length === 0 && !game.waveComplete) {
                game.waveComplete = true;
                setTimeout(() => setGameState('shop'), 1000);
            }
            
            // HUD
            ctx.fillStyle = 'rgba(0,0,0,0.6)';
            ctx.fillRect(5, 5, 160, 90);
            ctx.strokeStyle = '#3b82f6';
            ctx.lineWidth = 1;
            ctx.strokeRect(5, 5, 160, 90);
            
            ctx.fillStyle = '#fff';
            ctx.font = 'bold 16px monospace';
            ctx.textAlign = 'left';
            ctx.fillText(`WAVE ${wave}/20`, 15, 28);
            
            ctx.font = '14px monospace';
            ctx.fillStyle = '#fbbf24';
            ctx.fillText(`💰 ${credits}`, 15, 50);
            ctx.fillStyle = '#ef4444';
            ctx.fillText(`☠️ ${kills}`, 90, 50);
            
            ctx.fillStyle = '#22d3ee';
            ctx.fillText(`❤️ ${Math.ceil(playerHp)}`, 15, 72);
            ctx.fillStyle = '#3b82f6';
            ctx.fillText(`🏰 ${Math.ceil(baseHp)}`, 90, 72);
            
            // Controls hint
            ctx.fillStyle = 'rgba(255,255,255,0.5)';
            ctx.font = '10px monospace';
            ctx.textAlign = 'right';
            ctx.fillText('WASD/Arrows to move', CANVAS_WIDTH - 10, 20);
            
            animationId = requestAnimationFrame(gameLoop);
        };
        
        // Mouse handlers
        const handleMouseMove = (e) => {
            const rect = canvas.getBoundingClientRect();
            game.mouseX = (e.clientX - rect.left) * (CANVAS_WIDTH / rect.width);
            game.mouseY = (e.clientY - rect.top) * (CANVAS_HEIGHT / rect.height);
        };
        
        canvas.addEventListener('mousemove', handleMouseMove);
        gameLoop();
        
        return () => {
            cancelAnimationFrame(animationId);
            canvas.removeEventListener('mousemove', handleMouseMove);
        };
    }, [gameState, wave, upgrades, troops, playerHp, playerMaxHp, baseHp, baseMaxHp, playSound, highScore, getPlayerDamage, getPlayerFireRate, getPlayerArmor, getTurretDamage]);

    // ========================================================================
    // RENDER
    // ========================================================================
    return (
        <div className="relative w-full max-w-5xl mx-auto" data-testid="wave-defense-game">
            <div className="relative rounded-2xl overflow-hidden border-2 border-fuchsia-500/50 shadow-2xl shadow-fuchsia-500/20 bg-[#0c1222]">
                <canvas
                    ref={canvasRef}
                    width={CANVAS_WIDTH}
                    height={CANVAS_HEIGHT}
                    className="w-full cursor-crosshair"
                    style={{ aspectRatio: `${CANVAS_WIDTH}/${CANVAS_HEIGHT}` }}
                />
                
                {/* Menu Overlay */}
                {gameState === 'menu' && (
                    <div className="absolute inset-0 bg-gradient-to-b from-black/90 via-black/80 to-black/90 backdrop-blur-sm flex flex-col items-center justify-center">
                        <motion.div
                            initial={{ scale: 0.8, opacity: 0 }}
                            animate={{ scale: 1, opacity: 1 }}
                            className="text-center px-8"
                        >
                            <div className="mb-6">
                                <Shield className="w-20 h-20 mx-auto text-fuchsia-400 mb-4" />
                            </div>
                            <h1 className="text-5xl font-black text-transparent bg-clip-text bg-gradient-to-r from-fuchsia-400 via-cyan-400 to-fuchsia-400 mb-3 tracking-tight">
                                LAST LINE DEFENSE
                            </h1>
                            <p className="text-gray-400 text-lg mb-8">Survive 20 waves • Upgrade your arsenal • Protect your base</p>
                            
                            {highScore > 0 && (
                                <div className="mb-6 p-3 rounded-lg bg-amber-500/10 border border-amber-500/30 inline-block">
                                    <Trophy className="inline w-5 h-5 text-amber-400 mr-2" />
                                    <span className="text-amber-400 font-bold">Best: Wave {highScore}</span>
                                </div>
                            )}
                            
                            <div className="mb-8">
                                <Button onClick={startGame} size="lg" className="bg-gradient-to-r from-fuchsia-600 to-cyan-600 hover:from-fuchsia-500 hover:to-cyan-500 text-lg px-10 py-6 rounded-xl shadow-lg shadow-fuchsia-500/30">
                                    <Swords className="mr-3 w-6 h-6" /> START MISSION
                                </Button>
                            </div>
                            
                            <div className="bg-white/5 rounded-xl p-4 max-w-md mx-auto">
                                <h3 className="font-bold text-cyan-400 mb-3 flex items-center justify-center gap-2">
                                    <Move size={16} /> CONTROLS
                                </h3>
                                <div className="grid grid-cols-2 gap-3 text-sm">
                                    <div className="flex items-center gap-2 text-gray-400">
                                        <kbd className="px-2 py-1 bg-gray-800 rounded text-xs">WASD</kbd>
                                        <span>Move player</span>
                                    </div>
                                    <div className="flex items-center gap-2 text-gray-400">
                                        <kbd className="px-2 py-1 bg-gray-800 rounded text-xs">Mouse</kbd>
                                        <span>Aim & shoot</span>
                                    </div>
                                </div>
                            </div>
                        </motion.div>
                    </div>
                )}
                
                {/* Shop Overlay */}
                {gameState === 'shop' && (
                    <div className="absolute inset-0 bg-black/95 backdrop-blur-sm flex flex-col p-4 overflow-auto">
                        <div className="flex justify-between items-center mb-4 pb-3 border-b border-white/10">
                            <div>
                                <h2 className="text-2xl font-bold text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 to-fuchsia-400">
                                    ⚔️ ARMORY
                                </h2>
                                <p className="text-sm text-gray-400">Wave {wave} Complete! Prepare for Wave {wave + 1}</p>
                            </div>
                            <div className="text-2xl font-bold text-amber-400 bg-amber-500/10 px-4 py-2 rounded-lg">
                                💰 {credits}
                            </div>
                        </div>
                        
                        <div className="grid grid-cols-3 gap-4 flex-1 min-h-0">
                            {/* Player Upgrades */}
                            <div className="space-y-2 overflow-y-auto">
                                <h3 className="font-bold text-fuchsia-400 border-b border-fuchsia-400/30 pb-2 sticky top-0 bg-black">
                                    <Crosshair className="inline mr-2" size={16} /> PLAYER
                                </h3>
                                {['playerDamage', 'playerHealth', 'playerArmor', 'playerReload'].map(key => {
                                    const up = UPGRADES[key];
                                    const level = upgrades[key];
                                    const cost = up.baseCost + level * up.perLevel;
                                    const maxed = level >= up.maxLevel;
                                    return (
                                        <button
                                            key={key}
                                            onClick={() => buyUpgrade(key)}
                                            disabled={maxed || credits < cost}
                                            className={`w-full p-3 rounded-lg text-left transition-all ${
                                                maxed ? 'bg-gray-800/50 opacity-60' :
                                                credits >= cost ? 'bg-fuchsia-500/20 hover:bg-fuchsia-500/30 border border-fuchsia-500/30 hover:border-fuchsia-500/50' :
                                                'bg-gray-800/50 opacity-50'
                                            }`}
                                        >
                                            <div className="flex justify-between items-center">
                                                <span className="font-medium">{up.icon} {up.name}</span>
                                                <span className={`font-bold ${maxed ? 'text-gray-500' : 'text-amber-400'}`}>
                                                    {maxed ? '✓ MAX' : `$${cost}`}
                                                </span>
                                            </div>
                                            <div className="flex items-center gap-2 mt-2">
                                                {Array.from({ length: up.maxLevel }).map((_, i) => (
                                                    <div key={i} className={`h-1.5 flex-1 rounded ${i < level ? 'bg-fuchsia-400' : 'bg-gray-700'}`} />
                                                ))}
                                            </div>
                                        </button>
                                    );
                                })}
                            </div>
                            
                            {/* Base Upgrades */}
                            <div className="space-y-2 overflow-y-auto">
                                <h3 className="font-bold text-blue-400 border-b border-blue-400/30 pb-2 sticky top-0 bg-black">
                                    <Shield className="inline mr-2" size={16} /> BASE
                                </h3>
                                {['baseHealth', 'baseTurret', 'baseRegen'].map(key => {
                                    const up = UPGRADES[key];
                                    const level = upgrades[key];
                                    const cost = up.baseCost + level * up.perLevel;
                                    const maxed = level >= up.maxLevel;
                                    return (
                                        <button
                                            key={key}
                                            onClick={() => buyUpgrade(key)}
                                            disabled={maxed || credits < cost}
                                            className={`w-full p-3 rounded-lg text-left transition-all ${
                                                maxed ? 'bg-gray-800/50 opacity-60' :
                                                credits >= cost ? 'bg-blue-500/20 hover:bg-blue-500/30 border border-blue-500/30 hover:border-blue-500/50' :
                                                'bg-gray-800/50 opacity-50'
                                            }`}
                                        >
                                            <div className="flex justify-between items-center">
                                                <span className="font-medium">{up.icon} {up.name}</span>
                                                <span className={`font-bold ${maxed ? 'text-gray-500' : 'text-amber-400'}`}>
                                                    {maxed ? '✓ MAX' : `$${cost}`}
                                                </span>
                                            </div>
                                            <div className="flex items-center gap-2 mt-2">
                                                {Array.from({ length: up.maxLevel }).map((_, i) => (
                                                    <div key={i} className={`h-1.5 flex-1 rounded ${i < level ? 'bg-blue-400' : 'bg-gray-700'}`} />
                                                ))}
                                            </div>
                                        </button>
                                    );
                                })}
                                
                                <div className="mt-4 p-3 bg-blue-900/30 rounded-lg text-sm">
                                    <div className="grid grid-cols-2 gap-2">
                                        <div>🏰 HP: <span className="text-blue-400">{baseHp}/{baseMaxHp}</span></div>
                                        <div>🔧 Turret: <span className="text-amber-400">{getTurretDamage()} DMG</span></div>
                                        <div>🔩 Regen: <span className="text-green-400">+{getBaseRegen() * 12}/wave</span></div>
                                    </div>
                                </div>
                            </div>
                            
                            {/* Troops */}
                            <div className="space-y-2 overflow-y-auto">
                                <h3 className="font-bold text-green-400 border-b border-green-400/30 pb-2 sticky top-0 bg-black">
                                    <Users className="inline mr-2" size={16} /> TROOPS ({troops.length}/5)
                                </h3>
                                {Object.entries(TROOP_TYPES).map(([key, config]) => (
                                    <button
                                        key={key}
                                        onClick={() => buyTroop(key)}
                                        disabled={credits < config.cost || troops.length >= 5}
                                        className={`w-full p-3 rounded-lg text-left transition-all ${
                                            troops.length >= 5 ? 'bg-gray-800/50 opacity-60' :
                                            credits >= config.cost ? 'bg-green-500/20 hover:bg-green-500/30 border border-green-500/30 hover:border-green-500/50' :
                                            'bg-gray-800/50 opacity-50'
                                        }`}
                                    >
                                        <div className="flex justify-between items-center">
                                            <span className="font-medium" style={{ color: config.color }}>■ {config.name}</span>
                                            <span className="font-bold text-amber-400">${config.cost}</span>
                                        </div>
                                        <div className="text-xs text-gray-400 mt-1 grid grid-cols-3 gap-1">
                                            <span>❤️{config.hp}</span>
                                            <span>⚔️{config.damage}</span>
                                            <span>📏{config.range}</span>
                                        </div>
                                    </button>
                                ))}
                            </div>
                        </div>
                        
                        <div className="mt-4 pt-3 border-t border-white/10 flex justify-between items-center">
                            <div className="text-sm space-x-4">
                                <span className="text-cyan-400">❤️ {Math.ceil(playerHp)}/{playerMaxHp}</span>
                                <span className="text-fuchsia-400">🔫 {getPlayerDamage()} DMG</span>
                                <span className="text-amber-400">🛡️ {getPlayerArmor()} Armor</span>
                            </div>
                            <Button onClick={startNextWave} className="bg-gradient-to-r from-green-600 to-cyan-600 hover:from-green-500 hover:to-cyan-500 px-8">
                                {wave >= 20 ? '🏆 CLAIM VICTORY!' : `⚔️ WAVE ${wave + 1}`}
                                <ChevronRight className="ml-2" />
                            </Button>
                        </div>
                    </div>
                )}
                
                {/* Game Over */}
                {gameState === 'gameover' && (
                    <div className="absolute inset-0 bg-gradient-to-b from-red-900/50 via-black/90 to-black/90 backdrop-blur-sm flex flex-col items-center justify-center">
                        <motion.div
                            initial={{ scale: 0.5, opacity: 0 }}
                            animate={{ scale: 1, opacity: 1 }}
                            className="text-center"
                        >
                            <Skull className="w-24 h-24 text-red-500 mx-auto mb-4 animate-pulse" />
                            <h2 className="text-5xl font-black text-red-500 mb-4">GAME OVER</h2>
                            <p className="text-2xl text-gray-300 mb-2">Reached Wave {wave}</p>
                            <div className="space-y-1 mb-6">
                                <p className="text-amber-400 text-lg">💰 {totalCredits} Credits Earned</p>
                                <p className="text-cyan-400 text-lg">☠️ {kills} Enemies Defeated</p>
                            </div>
                            
                            {wave > highScore && wave > 1 && (
                                <motion.p
                                    initial={{ scale: 0 }}
                                    animate={{ scale: 1 }}
                                    className="text-2xl text-amber-400 mb-6 flex items-center justify-center gap-2"
                                >
                                    <Trophy className="w-6 h-6" /> NEW HIGH SCORE!
                                </motion.p>
                            )}
                            
                            <div className="flex gap-4 justify-center">
                                <Button onClick={startGame} className="bg-fuchsia-600 hover:bg-fuchsia-500 px-8">
                                    <RefreshCw className="mr-2" /> Play Again
                                </Button>
                                <Button onClick={onRetryConnection} variant="outline" className="border-cyan-500 text-cyan-400">
                                    Check Server
                                </Button>
                            </div>
                        </motion.div>
                    </div>
                )}
                
                {/* Victory */}
                {gameState === 'victory' && (
                    <div className="absolute inset-0 bg-gradient-to-b from-amber-900/30 via-black/80 to-black/90 backdrop-blur-sm flex flex-col items-center justify-center">
                        <motion.div
                            initial={{ scale: 0.5, opacity: 0, rotate: -10 }}
                            animate={{ scale: 1, opacity: 1, rotate: 0 }}
                            className="text-center"
                        >
                            <motion.div
                                animate={{ rotate: [0, 10, -10, 0] }}
                                transition={{ repeat: Infinity, duration: 2 }}
                            >
                                <Crown className="w-28 h-28 text-amber-400 mx-auto mb-4" />
                            </motion.div>
                            <h2 className="text-5xl font-black text-transparent bg-clip-text bg-gradient-to-r from-amber-400 via-yellow-300 to-amber-400 mb-4">
                                VICTORY!
                            </h2>
                            <p className="text-2xl text-gray-300 mb-2">All 20 Waves Survived!</p>
                            <div className="space-y-1 mb-6">
                                <p className="text-amber-400 text-lg">💰 {totalCredits} Total Credits</p>
                                <p className="text-cyan-400 text-lg">☠️ {kills} Total Kills</p>
                            </div>
                            
                            <div className="flex gap-4 justify-center">
                                <Button onClick={startGame} className="bg-gradient-to-r from-amber-500 to-yellow-500 hover:from-amber-400 hover:to-yellow-400 px-8">
                                    <RefreshCw className="mr-2" /> Play Again
                                </Button>
                                <Button onClick={onRetryConnection} variant="outline" className="border-cyan-500 text-cyan-400">
                                    Check Server
                                </Button>
                            </div>
                        </motion.div>
                    </div>
                )}
                
                {/* Sound toggle */}
                <button
                    onClick={() => setSoundEnabled(!soundEnabled)}
                    className="absolute top-3 right-3 p-2 bg-black/50 rounded-lg text-gray-400 hover:text-white transition-colors"
                >
                    {soundEnabled ? <Volume2 size={18} /> : <VolumeX size={18} />}
                </button>
            </div>
            
            {/* Server Message */}
            <motion.div 
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className="mt-4 p-4 rounded-xl bg-gradient-to-r from-amber-500/10 to-orange-500/10 border border-amber-500/30"
            >
                <div className="flex items-start gap-3">
                    <AlertTriangle className="w-5 h-5 text-amber-400 mt-0.5 flex-shrink-0" />
                    <div>
                        <p className="font-semibold text-amber-300">Server Temporarily Unavailable</p>
                        <p className="text-sm text-amber-300/80 mt-1">
                            Defend your base while we get things running! Move with WASD, aim with mouse. 🎮
                        </p>
                    </div>
                </div>
            </motion.div>
            
            <div className="mt-3 flex justify-center">
                <Button onClick={onRetryConnection} variant="outline" size="sm" className="border-cyan-500/50 text-cyan-400 hover:bg-cyan-500/10">
                    <RefreshCw size={14} className="mr-2" /> Retry Connection
                </Button>
            </div>
        </div>
    );
}

export default WaveDefenseGame;
