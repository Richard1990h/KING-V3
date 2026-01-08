import { useState, useEffect, useRef, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
    Shield, Heart, Zap, Target, Trophy, RefreshCw, Volume2, VolumeX,
    Crosshair, Users, Wrench, Swords, Crown, Skull, ChevronRight,
    Pause, Play, ShoppingBag, X, AlertTriangle, Star
} from 'lucide-react';
import { Button } from './ui/button';
import { ScrollArea } from './ui/scroll-area';

// ============================================================================
// GAME CONSTANTS
// ============================================================================
const CANVAS_WIDTH = 800;
const CANVAS_HEIGHT = 500;
const BASE_X = 50;
const BASE_WIDTH = 60;
const PLAYER_Y = CANVAS_HEIGHT - 80;
const SPAWN_X = CANVAS_WIDTH + 50;

const ENEMY_TYPES = {
    runner: { hp: 30, speed: 3, damage: 10, credits: 15, color: '#22c55e', size: 12, name: 'Runner' },
    grunt: { hp: 50, speed: 1.5, damage: 15, credits: 20, color: '#eab308', size: 16, name: 'Grunt' },
    tank: { hp: 150, speed: 0.8, damage: 30, credits: 50, color: '#ef4444', size: 24, name: 'Tank' },
    ranged: { hp: 40, speed: 1, damage: 20, credits: 30, color: '#8b5cf6', size: 14, range: 200, name: 'Shooter' },
    exploder: { hp: 60, speed: 2, damage: 50, credits: 40, color: '#f97316', size: 18, explosive: true, name: 'Exploder' },
    healer: { hp: 45, speed: 1.2, damage: 5, credits: 35, color: '#ec4899', size: 14, heals: true, name: 'Healer' },
    boss: { hp: 500, speed: 0.5, damage: 50, credits: 200, color: '#dc2626', size: 40, name: 'BOSS' }
};

const TROOP_TYPES = {
    soldier: { hp: 80, damage: 15, fireRate: 800, range: 250, cost: 100, color: '#3b82f6', name: 'Soldier' },
    sniper: { hp: 50, damage: 40, fireRate: 1500, range: 400, cost: 200, color: '#8b5cf6', name: 'Sniper' },
    heavy: { hp: 200, damage: 25, fireRate: 1200, range: 200, cost: 250, color: '#ef4444', name: 'Heavy' },
    medic: { hp: 60, damage: 5, fireRate: 2000, range: 150, cost: 150, color: '#22c55e', heals: true, name: 'Medic' },
    engineer: { hp: 70, damage: 10, fireRate: 1000, range: 180, cost: 180, color: '#eab308', repairs: true, name: 'Engineer' }
};

const UPGRADES = {
    playerDamage: { name: 'Weapon Damage', baseCost: 50, perLevel: 30, maxLevel: 10, icon: '🔫' },
    playerHealth: { name: 'Max Health', baseCost: 75, perLevel: 40, maxLevel: 10, icon: '❤️' },
    playerArmor: { name: 'Armor', baseCost: 60, perLevel: 35, maxLevel: 10, icon: '🛡️' },
    playerReload: { name: 'Fire Rate', baseCost: 80, perLevel: 50, maxLevel: 8, icon: '⚡' },
    baseHealth: { name: 'Base Walls', baseCost: 100, perLevel: 60, maxLevel: 10, icon: '🏰' },
    baseTurret: { name: 'Auto Turret', baseCost: 300, perLevel: 150, maxLevel: 5, icon: '🔧' },
    baseRegen: { name: 'Repair Station', baseCost: 200, perLevel: 100, maxLevel: 5, icon: '🔩' }
};

// ============================================================================
// GAME CLASSES
// ============================================================================
class Entity {
    constructor(x, y, config) {
        this.x = x;
        this.y = y;
        this.maxHp = config.hp;
        this.hp = config.hp;
        this.damage = config.damage;
        this.speed = config.speed || 0;
        this.color = config.color;
        this.size = config.size || 16;
        this.dead = false;
    }
    
    takeDamage(amount, armor = 0) {
        const reduced = Math.max(1, amount - armor);
        this.hp -= reduced;
        if (this.hp <= 0) {
            this.hp = 0;
            this.dead = true;
        }
        return reduced;
    }
    
    heal(amount) {
        this.hp = Math.min(this.maxHp, this.hp + amount);
    }
}

class Enemy extends Entity {
    constructor(type, waveNumber) {
        const config = { ...ENEMY_TYPES[type] };
        // Scale with wave
        const scale = 1 + (waveNumber - 1) * 0.1;
        config.hp = Math.floor(config.hp * scale);
        config.damage = Math.floor(config.damage * (1 + (waveNumber - 1) * 0.05));
        
        super(SPAWN_X, CANVAS_HEIGHT - 60 - Math.random() * 100, config);
        this.type = type;
        this.credits = Math.floor(config.credits * (1 + (waveNumber - 1) * 0.1));
        this.range = config.range || 0;
        this.explosive = config.explosive || false;
        this.heals = config.heals || false;
        this.lastShot = 0;
        this.lastHeal = 0;
    }
    
    update(base, player, enemies, now) {
        if (this.dead) return;
        
        // Healer logic
        if (this.heals && now - this.lastHeal > 2000) {
            const wounded = enemies.find(e => !e.dead && e !== this && e.hp < e.maxHp);
            if (wounded && Math.abs(wounded.x - this.x) < 150) {
                wounded.heal(20);
                this.lastHeal = now;
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
            if (this.x > BASE_X + BASE_WIDTH + 20) {
                this.x -= this.speed;
            } else {
                // Attack base
                return { type: 'attackBase', damage: this.damage };
            }
        }
        return null;
    }
    
    draw(ctx) {
        if (this.dead) return;
        
        // Body
        ctx.fillStyle = this.color;
        ctx.beginPath();
        if (this.type === 'boss') {
            // Boss is a scary hexagon
            const s = this.size;
            ctx.moveTo(this.x + s, this.y);
            for (let i = 1; i < 6; i++) {
                ctx.lineTo(
                    this.x + s * Math.cos(i * Math.PI / 3),
                    this.y + s * Math.sin(i * Math.PI / 3)
                );
            }
            ctx.closePath();
        } else {
            ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
        }
        ctx.fill();
        
        // Health bar
        const barWidth = this.size * 2;
        const barHeight = 4;
        const barY = this.y - this.size - 8;
        ctx.fillStyle = '#1f2937';
        ctx.fillRect(this.x - barWidth/2, barY, barWidth, barHeight);
        ctx.fillStyle = this.hp > this.maxHp * 0.3 ? '#22c55e' : '#ef4444';
        ctx.fillRect(this.x - barWidth/2, barY, barWidth * (this.hp / this.maxHp), barHeight);
        
        // Type indicator
        if (this.heals) {
            ctx.fillStyle = '#fff';
            ctx.font = '10px Arial';
            ctx.textAlign = 'center';
            ctx.fillText('+', this.x, this.y + 4);
        }
        if (this.explosive) {
            ctx.fillStyle = '#fff';
            ctx.font = '10px Arial';
            ctx.textAlign = 'center';
            ctx.fillText('💥', this.x, this.y + 4);
        }
    }
}

class Troop extends Entity {
    constructor(type, x, y) {
        const config = TROOP_TYPES[type];
        super(x, y, { ...config, speed: 0, size: 14 });
        this.type = type;
        this.fireRate = config.fireRate;
        this.range = config.range;
        this.heals = config.heals || false;
        this.repairs = config.repairs || false;
        this.lastShot = 0;
        this.level = 1;
    }
    
    update(enemies, player, base, now) {
        if (this.dead) return null;
        
        // Medic heals player/troops
        if (this.heals && now - this.lastShot > this.fireRate) {
            if (player.hp < player.maxHp) {
                player.heal(10);
                this.lastShot = now;
                return { type: 'heal', target: 'player' };
            }
        }
        
        // Engineer repairs base
        if (this.repairs && now - this.lastShot > this.fireRate) {
            if (base.hp < base.maxHp) {
                base.hp = Math.min(base.maxHp, base.hp + 5);
                this.lastShot = now;
                return { type: 'repair' };
            }
        }
        
        // Combat troops shoot enemies
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
        
        // Body (square for troops)
        ctx.fillStyle = this.color;
        ctx.fillRect(this.x - this.size/2, this.y - this.size/2, this.size, this.size);
        
        // Health bar
        const barWidth = this.size * 1.5;
        const barHeight = 3;
        const barY = this.y - this.size - 5;
        ctx.fillStyle = '#1f2937';
        ctx.fillRect(this.x - barWidth/2, barY, barWidth, barHeight);
        ctx.fillStyle = '#22c55e';
        ctx.fillRect(this.x - barWidth/2, barY, barWidth * (this.hp / this.maxHp), barHeight);
        
        // Level indicator
        if (this.level > 1) {
            ctx.fillStyle = '#fbbf24';
            ctx.font = 'bold 8px Arial';
            ctx.textAlign = 'center';
            ctx.fillText(`★${this.level}`, this.x, this.y + this.size + 10);
        }
    }
}

class Projectile {
    constructor(x, y, targetX, targetY, damage, isEnemy = false) {
        this.x = x;
        this.y = y;
        this.damage = damage;
        this.isEnemy = isEnemy;
        this.speed = 12;
        this.size = 4;
        this.dead = false;
        
        const dx = targetX - x;
        const dy = targetY - y;
        const dist = Math.sqrt(dx*dx + dy*dy);
        this.vx = (dx / dist) * this.speed;
        this.vy = (dy / dist) * this.speed;
    }
    
    update() {
        this.x += this.vx;
        this.y += this.vy;
        if (this.x < 0 || this.x > CANVAS_WIDTH || this.y < 0 || this.y > CANVAS_HEIGHT) {
            this.dead = true;
        }
    }
    
    draw(ctx) {
        ctx.fillStyle = this.isEnemy ? '#ef4444' : '#fbbf24';
        ctx.beginPath();
        ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
        ctx.fill();
    }
}

class Particle {
    constructor(x, y, color, type = 'explosion') {
        this.x = x;
        this.y = y;
        this.color = color;
        this.type = type;
        this.vx = (Math.random() - 0.5) * 8;
        this.vy = (Math.random() - 0.5) * 8;
        this.life = 1;
        this.size = type === 'explosion' ? Math.random() * 6 + 2 : 3;
    }
    
    update() {
        this.x += this.vx;
        this.y += this.vy;
        this.vy += 0.2;
        this.life -= 0.03;
        return this.life > 0;
    }
    
    draw(ctx) {
        ctx.globalAlpha = this.life;
        ctx.fillStyle = this.color;
        ctx.beginPath();
        ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 1;
    }
}

// ============================================================================
// WAVE GENERATOR
// ============================================================================
function generateWave(waveNumber) {
    const enemies = [];
    const baseCount = 3 + Math.floor(waveNumber * 1.5);
    
    // Boss every 5 waves
    if (waveNumber % 5 === 0) {
        enemies.push({ type: 'boss', delay: 2000 });
    }
    
    // Regular enemies based on wave
    for (let i = 0; i < baseCount; i++) {
        let type = 'grunt';
        const roll = Math.random();
        
        if (waveNumber >= 3 && roll < 0.3) type = 'runner';
        if (waveNumber >= 5 && roll < 0.2) type = 'tank';
        if (waveNumber >= 7 && roll < 0.15) type = 'ranged';
        if (waveNumber >= 10 && roll < 0.1) type = 'exploder';
        if (waveNumber >= 12 && roll < 0.08) type = 'healer';
        
        enemies.push({ type, delay: i * (800 - Math.min(waveNumber * 20, 400)) });
    }
    
    return enemies;
}

// ============================================================================
// MAIN GAME COMPONENT
// ============================================================================
export function WaveDefenseGame({ onRetryConnection }) {
    const canvasRef = useRef(null);
    const gameRef = useRef(null);
    
    // Game state
    const [gameState, setGameState] = useState('menu'); // menu, playing, paused, shop, gameover, victory
    const [wave, setWave] = useState(1);
    const [credits, setCredits] = useState(100);
    const [totalCredits, setTotalCredits] = useState(0);
    const [kills, setKills] = useState(0);
    const [soundEnabled, setSoundEnabled] = useState(true);
    
    // Player stats
    const [playerHp, setPlayerHp] = useState(100);
    const [playerMaxHp, setPlayerMaxHp] = useState(100);
    const [baseHp, setBaseHp] = useState(500);
    const [baseMaxHp, setBaseMaxHp] = useState(500);
    
    // Upgrades
    const [upgrades, setUpgrades] = useState({
        playerDamage: 0,
        playerHealth: 0,
        playerArmor: 0,
        playerReload: 0,
        baseHealth: 0,
        baseTurret: 0,
        baseRegen: 0
    });
    
    // Troops
    const [troops, setTroops] = useState([]);
    const [selectedTroop, setSelectedTroop] = useState(null);
    
    // High score
    const [highScore, setHighScore] = useState(() => {
        return parseInt(localStorage.getItem('waveDefenseHighWave') || '0');
    });

    // Sound effect
    const playSound = useCallback((type) => {
        if (!soundEnabled) return;
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(ctx.destination);
            
            const sounds = {
                shoot: { freq: 400, type: 'square', dur: 0.05 },
                hit: { freq: 200, type: 'sawtooth', dur: 0.1 },
                kill: { freq: 600, type: 'sine', dur: 0.15 },
                levelUp: { freq: 800, type: 'sine', dur: 0.2 },
                damage: { freq: 150, type: 'sawtooth', dur: 0.1 },
                buy: { freq: 500, type: 'sine', dur: 0.1 }
            };
            
            const s = sounds[type] || sounds.shoot;
            osc.frequency.value = s.freq;
            osc.type = s.type;
            gain.gain.value = 0.1;
            osc.start();
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + s.dur);
            osc.stop(ctx.currentTime + s.dur);
        } catch(e) {
            // Audio not supported, silently ignore
        }
    }, [soundEnabled]);

    // Calculate derived stats
    const getPlayerDamage = useCallback(() => 20 + upgrades.playerDamage * 5, [upgrades.playerDamage]);
    const getPlayerFireRate = useCallback(() => Math.max(150, 400 - upgrades.playerReload * 30), [upgrades.playerReload]);
    const getPlayerArmor = useCallback(() => upgrades.playerArmor * 2, [upgrades.playerArmor]);
    const getTurretDamage = useCallback(() => upgrades.baseTurret * 15, [upgrades.baseTurret]);
    const getBaseRegen = () => upgrades.baseRegen * 2;

    // Start new game
    const startGame = () => {
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
        
        // Initialize game ref
        gameRef.current = {
            enemies: [],
            projectiles: [],
            particles: [],
            waveEnemies: generateWave(1),
            waveStartTime: Date.now(),
            player: { x: 200, y: PLAYER_Y, lastShot: 0 },
            mouseX: 200,
            mouseY: 300,
            waveComplete: false
        };
    };

    // Buy upgrade
    const buyUpgrade = (key) => {
        const upgrade = UPGRADES[key];
        const level = upgrades[key];
        if (level >= upgrade.maxLevel) return;
        
        const cost = upgrade.baseCost + level * upgrade.perLevel;
        if (credits < cost) return;
        
        setCredits(c => c - cost);
        setUpgrades(u => ({ ...u, [key]: u[key] + 1 }));
        playSound('buy');
        
        // Apply immediate effects
        if (key === 'playerHealth') {
            const newMax = 100 + (upgrades.playerHealth + 1) * 20;
            setPlayerMaxHp(newMax);
            setPlayerHp(h => Math.min(h + 20, newMax));
        }
        if (key === 'baseHealth') {
            const newMax = 500 + (upgrades.baseHealth + 1) * 50;
            setBaseMaxHp(newMax);
            setBaseHp(h => Math.min(h + 50, newMax));
        }
    };

    // Buy troop
    const buyTroop = (type) => {
        const config = TROOP_TYPES[type];
        if (credits < config.cost) return;
        if (troops.length >= 5) return; // Max 5 troops
        
        setCredits(c => c - config.cost);
        const newTroop = new Troop(type, 120 + troops.length * 40, PLAYER_Y - 30);
        setTroops(t => [...t, newTroop]);
        playSound('buy');
    };

    // Next wave
    const startNextWave = () => {
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
        
        // Apply base regen
        setBaseHp(h => Math.min(baseMaxHp, h + getBaseRegen() * 10));
        
        playSound('levelUp');
    };

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
            
            // Clear
            ctx.fillStyle = '#0f172a';
            ctx.fillRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
            
            // Draw grid
            ctx.strokeStyle = 'rgba(255,255,255,0.03)';
            for (let i = 0; i < CANVAS_WIDTH; i += 40) {
                ctx.beginPath();
                ctx.moveTo(i, 0);
                ctx.lineTo(i, CANVAS_HEIGHT);
                ctx.stroke();
            }
            
            // Draw base
            const baseHealthPercent = baseHp / baseMaxHp;
            ctx.fillStyle = baseHealthPercent > 0.5 ? '#3b82f6' : baseHealthPercent > 0.25 ? '#eab308' : '#ef4444';
            ctx.fillRect(BASE_X, CANVAS_HEIGHT - 150, BASE_WIDTH, 150);
            ctx.fillStyle = '#1e3a5f';
            ctx.fillRect(BASE_X + 10, CANVAS_HEIGHT - 140, BASE_WIDTH - 20, 100);
            
            // Base health bar
            ctx.fillStyle = '#1f2937';
            ctx.fillRect(BASE_X, CANVAS_HEIGHT - 170, BASE_WIDTH, 10);
            ctx.fillStyle = '#22c55e';
            ctx.fillRect(BASE_X, CANVAS_HEIGHT - 170, BASE_WIDTH * baseHealthPercent, 10);
            
            // Draw turrets
            if (upgrades.baseTurret > 0) {
                ctx.fillStyle = '#fbbf24';
                ctx.fillRect(BASE_X + BASE_WIDTH - 10, CANVAS_HEIGHT - 160, 15, 15);
            }
            
            // Spawn enemies from wave
            const waveTime = now - game.waveStartTime;
            game.waveEnemies = game.waveEnemies.filter(we => {
                if (waveTime >= we.delay) {
                    game.enemies.push(new Enemy(we.type, wave));
                    return false;
                }
                return true;
            });
            
            // Update enemies
            game.enemies.forEach(enemy => {
                const action = enemy.update({ hp: baseHp }, game.player, game.enemies, now);
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
                }
                enemy.draw(ctx);
            });
            
            // Auto turret
            if (upgrades.baseTurret > 0 && game.enemies.length > 0) {
                const target = game.enemies.find(e => !e.dead && e.x < CANVAS_WIDTH - 100);
                if (target && now - (game.lastTurretShot || 0) > 1000) {
                    game.projectiles.push(new Projectile(
                        BASE_X + BASE_WIDTH, CANVAS_HEIGHT - 155,
                        target.x, target.y,
                        getTurretDamage()
                    ));
                    game.lastTurretShot = now;
                }
            }
            
            // Update troops
            troops.forEach((troop, idx) => {
                if (troop.dead) return;
                const action = troop.update(game.enemies, 
                    { hp: playerHp, maxHp: playerMaxHp, heal: (amt) => setPlayerHp(h => Math.min(playerMaxHp, h + amt)) },
                    { hp: baseHp, maxHp: baseMaxHp },
                    now
                );
                if (action && action.type === 'shoot') {
                    game.projectiles.push(new Projectile(
                        troop.x, troop.y,
                        action.target.x, action.target.y,
                        action.damage
                    ));
                }
                if (action && action.type === 'repair') {
                    setBaseHp(h => Math.min(baseMaxHp, h + 5));
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
                        getPlayerDamage()
                    ));
                    game.player.lastShot = now;
                    playSound('shoot');
                }
            }
            
            // Update projectiles
            game.projectiles = game.projectiles.filter(proj => {
                proj.update();
                if (proj.dead) return false;
                
                // Check collisions
                if (proj.isEnemy) {
                    // Hit player
                    const dist = Math.sqrt((proj.x - game.player.x)**2 + (proj.y - game.player.y)**2);
                    if (dist < 20) {
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
                        return false;
                    }
                } else {
                    // Hit enemies
                    for (const enemy of game.enemies) {
                        if (enemy.dead) continue;
                        const dist = Math.sqrt((proj.x - enemy.x)**2 + (proj.y - enemy.y)**2);
                        if (dist < enemy.size + proj.size) {
                            enemy.takeDamage(proj.damage);
                            
                            // Particles
                            for (let i = 0; i < 5; i++) {
                                game.particles.push(new Particle(enemy.x, enemy.y, enemy.color));
                            }
                            
                            if (enemy.dead) {
                                // Explosion for exploders
                                if (enemy.explosive) {
                                    for (let i = 0; i < 20; i++) {
                                        game.particles.push(new Particle(enemy.x, enemy.y, '#f97316'));
                                    }
                                    // Damage nearby
                                    game.enemies.forEach(e => {
                                        if (!e.dead && e !== enemy) {
                                            const d = Math.sqrt((e.x - enemy.x)**2 + (e.y - enemy.y)**2);
                                            if (d < 80) e.takeDamage(30);
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
            ctx.fillStyle = '#22d3ee';
            ctx.beginPath();
            ctx.arc(game.player.x, game.player.y, 18, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#0891b2';
            ctx.beginPath();
            ctx.arc(game.player.x, game.player.y, 10, 0, Math.PI * 2);
            ctx.fill();
            
            // Player health bar
            ctx.fillStyle = '#1f2937';
            ctx.fillRect(game.player.x - 25, game.player.y - 35, 50, 6);
            ctx.fillStyle = playerHp > playerMaxHp * 0.3 ? '#22c55e' : '#ef4444';
            ctx.fillRect(game.player.x - 25, game.player.y - 35, 50 * (playerHp / playerMaxHp), 6);
            
            // Draw crosshair
            ctx.strokeStyle = '#f472b6';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.arc(game.mouseX, game.mouseY, 12, 0, Math.PI * 2);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(game.mouseX - 20, game.mouseY);
            ctx.lineTo(game.mouseX - 6, game.mouseY);
            ctx.moveTo(game.mouseX + 6, game.mouseY);
            ctx.lineTo(game.mouseX + 20, game.mouseY);
            ctx.moveTo(game.mouseX, game.mouseY - 20);
            ctx.lineTo(game.mouseX, game.mouseY - 6);
            ctx.moveTo(game.mouseX, game.mouseY + 6);
            ctx.lineTo(game.mouseX, game.mouseY + 20);
            ctx.stroke();
            
            // Check wave complete
            if (game.waveEnemies.length === 0 && game.enemies.length === 0 && !game.waveComplete) {
                game.waveComplete = true;
                setTimeout(() => setGameState('shop'), 1000);
            }
            
            // HUD
            ctx.fillStyle = '#fff';
            ctx.font = 'bold 16px monospace';
            ctx.textAlign = 'left';
            ctx.fillText(`WAVE ${wave}/20`, 10, 25);
            ctx.fillText(`💰 ${credits}`, 10, 50);
            ctx.fillText(`☠️ ${kills}`, 10, 75);
            
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
    }, [gameState, wave, upgrades, troops, playerHp, playerMaxHp, baseHp, baseMaxHp, playSound, highScore]);

    // ========================================================================
    // RENDER
    // ========================================================================
    return (
        <div className="relative w-full max-w-4xl mx-auto">
            {/* Game Canvas */}
            <div className="relative rounded-xl overflow-hidden border-2 border-fuchsia-500/30 bg-[#0f172a]">
                <canvas
                    ref={canvasRef}
                    width={CANVAS_WIDTH}
                    height={CANVAS_HEIGHT}
                    className="w-full cursor-crosshair"
                    style={{ aspectRatio: `${CANVAS_WIDTH}/${CANVAS_HEIGHT}` }}
                />
                
                {/* Menu Overlay */}
                {gameState === 'menu' && (
                    <div className="absolute inset-0 bg-black/80 backdrop-blur-sm flex flex-col items-center justify-center">
                        <motion.div
                            initial={{ scale: 0.8, opacity: 0 }}
                            animate={{ scale: 1, opacity: 1 }}
                            className="text-center"
                        >
                            <h1 className="text-4xl font-bold text-transparent bg-clip-text bg-gradient-to-r from-fuchsia-400 to-cyan-400 mb-2">
                                LAST LINE DEFENSE
                            </h1>
                            <p className="text-gray-400 mb-6">Survive 20 waves • Upgrade • Protect your base</p>
                            
                            {highScore > 0 && (
                                <p className="text-amber-400 mb-4">🏆 Best: Wave {highScore}</p>
                            )}
                            
                            <Button onClick={startGame} size="lg" className="bg-gradient-to-r from-fuchsia-500 to-cyan-500">
                                <Swords className="mr-2" /> START GAME
                            </Button>
                            
                            <div className="mt-6 text-sm text-gray-500">
                                <p>🖱️ Move mouse to aim • Auto-fire</p>
                                <p>💰 Earn credits • Buy upgrades between waves</p>
                            </div>
                        </motion.div>
                    </div>
                )}
                
                {/* Shop Overlay */}
                {gameState === 'shop' && (
                    <div className="absolute inset-0 bg-black/90 backdrop-blur-sm flex flex-col p-4 overflow-auto">
                        <div className="flex justify-between items-center mb-4">
                            <h2 className="text-2xl font-bold text-cyan-400">
                                <ShoppingBag className="inline mr-2" />
                                ARMORY - Wave {wave} Complete!
                            </h2>
                            <div className="text-xl font-bold text-amber-400">💰 {credits}</div>
                        </div>
                        
                        <div className="grid grid-cols-3 gap-4 flex-1">
                            {/* Player Upgrades */}
                            <div className="space-y-2">
                                <h3 className="font-bold text-fuchsia-400 border-b border-fuchsia-400/30 pb-1">
                                    <Crosshair className="inline mr-1" size={16} /> PLAYER
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
                                            className={`w-full p-2 rounded text-left text-sm ${
                                                maxed ? 'bg-gray-700 opacity-50' :
                                                credits >= cost ? 'bg-fuchsia-500/20 hover:bg-fuchsia-500/30' :
                                                'bg-gray-800 opacity-50'
                                            }`}
                                        >
                                            <div className="flex justify-between">
                                                <span>{up.icon} {up.name}</span>
                                                <span className="text-amber-400">{maxed ? 'MAX' : `$${cost}`}</span>
                                            </div>
                                            <div className="text-xs text-gray-400">
                                                Level {level}/{up.maxLevel}
                                            </div>
                                        </button>
                                    );
                                })}
                            </div>
                            
                            {/* Base Upgrades */}
                            <div className="space-y-2">
                                <h3 className="font-bold text-blue-400 border-b border-blue-400/30 pb-1">
                                    <Shield className="inline mr-1" size={16} /> BASE
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
                                            className={`w-full p-2 rounded text-left text-sm ${
                                                maxed ? 'bg-gray-700 opacity-50' :
                                                credits >= cost ? 'bg-blue-500/20 hover:bg-blue-500/30' :
                                                'bg-gray-800 opacity-50'
                                            }`}
                                        >
                                            <div className="flex justify-between">
                                                <span>{up.icon} {up.name}</span>
                                                <span className="text-amber-400">{maxed ? 'MAX' : `$${cost}`}</span>
                                            </div>
                                            <div className="text-xs text-gray-400">
                                                Level {level}/{up.maxLevel}
                                            </div>
                                        </button>
                                    );
                                })}
                                
                                <div className="mt-4 p-2 bg-blue-900/30 rounded text-xs">
                                    <div>🏰 Base HP: {baseHp}/{baseMaxHp}</div>
                                    <div>🔧 Turret DMG: {getTurretDamage()}</div>
                                    <div>🔩 Regen: +{getBaseRegen() * 10}/wave</div>
                                </div>
                            </div>
                            
                            {/* Troops */}
                            <div className="space-y-2">
                                <h3 className="font-bold text-green-400 border-b border-green-400/30 pb-1">
                                    <Users className="inline mr-1" size={16} /> TROOPS ({troops.length}/5)
                                </h3>
                                {Object.entries(TROOP_TYPES).map(([key, config]) => (
                                    <button
                                        key={key}
                                        onClick={() => buyTroop(key)}
                                        disabled={credits < config.cost || troops.length >= 5}
                                        className={`w-full p-2 rounded text-left text-sm ${
                                            troops.length >= 5 ? 'bg-gray-700 opacity-50' :
                                            credits >= config.cost ? 'bg-green-500/20 hover:bg-green-500/30' :
                                            'bg-gray-800 opacity-50'
                                        }`}
                                    >
                                        <div className="flex justify-between">
                                            <span style={{ color: config.color }}>■ {config.name}</span>
                                            <span className="text-amber-400">${config.cost}</span>
                                        </div>
                                        <div className="text-xs text-gray-400">
                                            HP:{config.hp} DMG:{config.damage} RNG:{config.range}
                                        </div>
                                    </button>
                                ))}
                            </div>
                        </div>
                        
                        <div className="mt-4 flex justify-between items-center">
                            <div className="text-sm text-gray-400">
                                ❤️ Player: {playerHp}/{playerMaxHp} | 
                                🔫 DMG: {getPlayerDamage()} | 
                                🛡️ Armor: {getPlayerArmor()}
                            </div>
                            <Button onClick={startNextWave} className="bg-gradient-to-r from-green-500 to-cyan-500">
                                {wave >= 20 ? '🏆 VICTORY!' : `START WAVE ${wave + 1}`}
                                <ChevronRight className="ml-2" />
                            </Button>
                        </div>
                    </div>
                )}
                
                {/* Game Over */}
                {gameState === 'gameover' && (
                    <div className="absolute inset-0 bg-black/90 backdrop-blur-sm flex flex-col items-center justify-center">
                        <motion.div
                            initial={{ scale: 0.5 }}
                            animate={{ scale: 1 }}
                            className="text-center"
                        >
                            <Skull className="w-20 h-20 text-red-500 mx-auto mb-4" />
                            <h2 className="text-4xl font-bold text-red-500 mb-2">GAME OVER</h2>
                            <p className="text-xl text-gray-400 mb-2">Reached Wave {wave}</p>
                            <p className="text-amber-400 mb-1">💰 Credits Earned: {totalCredits}</p>
                            <p className="text-cyan-400 mb-6">☠️ Enemies Killed: {kills}</p>
                            
                            {wave > highScore && wave > 1 && (
                                <p className="text-amber-400 text-lg mb-4">🏆 NEW HIGH SCORE!</p>
                            )}
                            
                            <div className="flex gap-3 justify-center">
                                <Button onClick={startGame} className="bg-fuchsia-500">
                                    <RefreshCw className="mr-2" /> Play Again
                                </Button>
                                <Button onClick={onRetryConnection} variant="outline">
                                    Check Server
                                </Button>
                            </div>
                        </motion.div>
                    </div>
                )}
                
                {/* Victory */}
                {gameState === 'victory' && (
                    <div className="absolute inset-0 bg-black/90 backdrop-blur-sm flex flex-col items-center justify-center">
                        <motion.div
                            initial={{ scale: 0.5 }}
                            animate={{ scale: 1 }}
                            className="text-center"
                        >
                            <Crown className="w-20 h-20 text-amber-400 mx-auto mb-4" />
                            <h2 className="text-4xl font-bold text-transparent bg-clip-text bg-gradient-to-r from-amber-400 to-yellow-200 mb-2">
                                VICTORY!
                            </h2>
                            <p className="text-xl text-gray-400 mb-2">You survived all 20 waves!</p>
                            <p className="text-amber-400 mb-1">💰 Total Credits: {totalCredits}</p>
                            <p className="text-cyan-400 mb-6">☠️ Total Kills: {kills}</p>
                            
                            <div className="flex gap-3 justify-center">
                                <Button onClick={startGame} className="bg-gradient-to-r from-amber-500 to-yellow-500">
                                    <RefreshCw className="mr-2" /> Play Again
                                </Button>
                                <Button onClick={onRetryConnection} variant="outline">
                                    Check Server
                                </Button>
                            </div>
                        </motion.div>
                    </div>
                )}
                
                {/* Controls */}
                <div className="absolute top-2 right-2 flex gap-2">
                    <button
                        onClick={() => setSoundEnabled(!soundEnabled)}
                        className="p-2 bg-black/50 rounded text-gray-400 hover:text-white"
                    >
                        {soundEnabled ? <Volume2 size={16} /> : <VolumeX size={16} />}
                    </button>
                </div>
            </div>
            
            {/* Server Message */}
            <div className="mt-4 p-4 rounded-lg bg-amber-500/10 border border-amber-500/30">
                <div className="flex items-start gap-3">
                    <AlertTriangle className="w-5 h-5 text-amber-400 mt-0.5 flex-shrink-0" />
                    <div>
                        <p className="font-semibold text-amber-300">Server Temporarily Unavailable</p>
                        <p className="text-sm text-amber-300/80 mt-1">
                            Defend your base while we get things running! 🎮
                        </p>
                    </div>
                </div>
            </div>
            
            <div className="mt-3 flex justify-center gap-3">
                <Button onClick={onRetryConnection} variant="outline" size="sm" className="border-cyan-500/50 text-cyan-400">
                    <RefreshCw size={14} className="mr-2" /> Check Server
                </Button>
            </div>
        </div>
    );
}

export default WaveDefenseGame;
