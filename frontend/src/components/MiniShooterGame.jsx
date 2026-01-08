import { useState, useEffect, useRef, useCallback } from 'react';
import { motion } from 'framer-motion';
import { Crosshair, Target, Zap, Trophy, RefreshCw, Volume2, VolumeX } from 'lucide-react';
import { Button } from './ui/button';

/**
 * MiniShooterGame - A fun FPS-style game to play while waiting for the server
 */
export function MiniShooterGame({ onRetryConnection }) {
    const canvasRef = useRef(null);
    const [score, setScore] = useState(0);
    const [highScore, setHighScore] = useState(() => {
        return parseInt(localStorage.getItem('shooterHighScore') || '0');
    });
    const [gameOver, setGameOver] = useState(false);
    const [lives, setLives] = useState(3);
    const [level, setLevel] = useState(1);
    const [combo, setCombo] = useState(0);
    const [soundEnabled, setSoundEnabled] = useState(true);
    const [isPaused, setIsPaused] = useState(false);
    
    const gameStateRef = useRef({
        targets: [],
        particles: [],
        lastSpawn: 0,
        mouseX: 0,
        mouseY: 0,
        shooting: false
    });

    // Sound effects (simple beeps using Web Audio API)
    const playSound = useCallback((type) => {
        if (!soundEnabled) return;
        try {
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();
            
            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);
            
            switch(type) {
                case 'hit':
                    oscillator.frequency.value = 800;
                    gainNode.gain.value = 0.1;
                    oscillator.type = 'sine';
                    break;
                case 'miss':
                    oscillator.frequency.value = 200;
                    gainNode.gain.value = 0.05;
                    oscillator.type = 'sawtooth';
                    break;
                case 'levelUp':
                    oscillator.frequency.value = 1200;
                    gainNode.gain.value = 0.1;
                    oscillator.type = 'sine';
                    break;
                default:
                    oscillator.frequency.value = 400;
                    gainNode.gain.value = 0.05;
            }
            
            oscillator.start();
            gainNode.gain.exponentialRampToValueAtTime(0.001, audioContext.currentTime + 0.1);
            oscillator.stop(audioContext.currentTime + 0.1);
        } catch (e) {
            // Audio not supported
        }
    }, [soundEnabled]);

    // Target class
    class Target {
        constructor(canvas, level) {
            this.canvas = canvas;
            this.size = Math.random() * 20 + 25 - (level * 2);
            this.size = Math.max(this.size, 15);
            this.x = Math.random() * (canvas.width - this.size * 2) + this.size;
            this.y = -this.size;
            this.speed = (Math.random() * 2 + 1) + (level * 0.3);
            this.points = Math.floor(100 / this.size * 10);
            this.color = `hsl(${Math.random() * 60 + 300}, 80%, 60%)`; // Pink/purple hues
            this.hit = false;
            this.wobble = Math.random() * Math.PI * 2;
            this.wobbleSpeed = Math.random() * 0.1 + 0.02;
            this.wobbleAmount = Math.random() * 2 + 1;
        }

        update() {
            this.y += this.speed;
            this.wobble += this.wobbleSpeed;
            this.x += Math.sin(this.wobble) * this.wobbleAmount;
            return this.y < this.canvas.height + this.size;
        }

        draw(ctx) {
            if (this.hit) return;
            
            // Outer glow
            ctx.shadowColor = this.color;
            ctx.shadowBlur = 15;
            
            // Target circle
            ctx.beginPath();
            ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
            ctx.fillStyle = this.color;
            ctx.fill();
            
            // Inner circle
            ctx.beginPath();
            ctx.arc(this.x, this.y, this.size * 0.6, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(0,0,0,0.3)';
            ctx.fill();
            
            // Center dot
            ctx.beginPath();
            ctx.arc(this.x, this.y, this.size * 0.2, 0, Math.PI * 2);
            ctx.fillStyle = '#fff';
            ctx.fill();
            
            ctx.shadowBlur = 0;
            
            // Points label
            ctx.fillStyle = '#fff';
            ctx.font = 'bold 10px monospace';
            ctx.textAlign = 'center';
            ctx.fillText(`+${this.points}`, this.x, this.y + this.size + 15);
        }

        checkHit(x, y) {
            const dist = Math.sqrt((x - this.x) ** 2 + (y - this.y) ** 2);
            return dist < this.size;
        }
    }

    // Particle class for hit effects
    class Particle {
        constructor(x, y, color) {
            this.x = x;
            this.y = y;
            this.vx = (Math.random() - 0.5) * 10;
            this.vy = (Math.random() - 0.5) * 10;
            this.life = 1;
            this.color = color;
            this.size = Math.random() * 4 + 2;
        }

        update() {
            this.x += this.vx;
            this.y += this.vy;
            this.life -= 0.02;
            this.vy += 0.2; // gravity
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

    // Main game loop
    useEffect(() => {
        const canvas = canvasRef.current;
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        const state = gameStateRef.current;
        let animationId;

        const resizeCanvas = () => {
            canvas.width = canvas.offsetWidth;
            canvas.height = canvas.offsetHeight;
        };
        resizeCanvas();
        window.addEventListener('resize', resizeCanvas);

        const spawnTarget = () => {
            if (Date.now() - state.lastSpawn > Math.max(800 - level * 50, 300)) {
                state.targets.push(new Target(canvas, level));
                state.lastSpawn = Date.now();
            }
        };

        const gameLoop = () => {
            if (gameOver || isPaused) {
                animationId = requestAnimationFrame(gameLoop);
                return;
            }

            // Clear canvas with trail effect
            ctx.fillStyle = 'rgba(3, 7, 18, 0.3)';
            ctx.fillRect(0, 0, canvas.width, canvas.height);

            // Draw grid lines for depth effect
            ctx.strokeStyle = 'rgba(255,255,255,0.03)';
            ctx.lineWidth = 1;
            for (let i = 0; i < canvas.width; i += 50) {
                ctx.beginPath();
                ctx.moveTo(i, 0);
                ctx.lineTo(i, canvas.height);
                ctx.stroke();
            }
            for (let i = 0; i < canvas.height; i += 50) {
                ctx.beginPath();
                ctx.moveTo(0, i);
                ctx.lineTo(canvas.width, i);
                ctx.stroke();
            }

            // Spawn targets
            spawnTarget();

            // Update and draw targets
            state.targets = state.targets.filter(target => {
                const alive = target.update();
                if (!alive && !target.hit) {
                    // Target escaped - lose a life
                    setLives(l => {
                        const newLives = l - 1;
                        if (newLives <= 0) {
                            setGameOver(true);
                            if (score > highScore) {
                                setHighScore(score);
                                localStorage.setItem('shooterHighScore', score.toString());
                            }
                        }
                        return newLives;
                    });
                    setCombo(0);
                    playSound('miss');
                }
                target.draw(ctx);
                return alive;
            });

            // Update and draw particles
            state.particles = state.particles.filter(particle => {
                const alive = particle.update();
                particle.draw(ctx);
                return alive;
            });

            // Draw crosshair
            ctx.strokeStyle = '#00ffff';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.arc(state.mouseX, state.mouseY, 15, 0, Math.PI * 2);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(state.mouseX - 25, state.mouseY);
            ctx.lineTo(state.mouseX - 8, state.mouseY);
            ctx.moveTo(state.mouseX + 8, state.mouseY);
            ctx.lineTo(state.mouseX + 25, state.mouseY);
            ctx.moveTo(state.mouseX, state.mouseY - 25);
            ctx.lineTo(state.mouseX, state.mouseY - 8);
            ctx.moveTo(state.mouseX, state.mouseY + 8);
            ctx.lineTo(state.mouseX, state.mouseY + 25);
            ctx.stroke();

            // Center dot
            ctx.fillStyle = '#ff0066';
            ctx.beginPath();
            ctx.arc(state.mouseX, state.mouseY, 3, 0, Math.PI * 2);
            ctx.fill();

            animationId = requestAnimationFrame(gameLoop);
        };

        // Mouse/touch handlers
        const handleMouseMove = (e) => {
            const rect = canvas.getBoundingClientRect();
            state.mouseX = e.clientX - rect.left;
            state.mouseY = e.clientY - rect.top;
        };

        const handleClick = (e) => {
            if (gameOver || isPaused) return;
            
            const rect = canvas.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;

            let hitSomething = false;
            state.targets.forEach(target => {
                if (!target.hit && target.checkHit(x, y)) {
                    target.hit = true;
                    hitSomething = true;
                    
                    // Add particles
                    for (let i = 0; i < 15; i++) {
                        state.particles.push(new Particle(target.x, target.y, target.color));
                    }
                    
                    // Update score with combo
                    setCombo(c => c + 1);
                    setScore(s => {
                        const comboBonus = Math.floor(combo * 10);
                        const newScore = s + target.points + comboBonus;
                        
                        // Level up every 500 points
                        if (Math.floor(newScore / 500) > Math.floor(s / 500)) {
                            setLevel(l => l + 1);
                            playSound('levelUp');
                        }
                        
                        return newScore;
                    });
                    
                    playSound('hit');
                }
            });

            if (!hitSomething) {
                setCombo(0);
            }

            // Remove hit targets
            state.targets = state.targets.filter(t => !t.hit);
        };

        canvas.addEventListener('mousemove', handleMouseMove);
        canvas.addEventListener('click', handleClick);
        canvas.addEventListener('touchmove', (e) => {
            e.preventDefault();
            const touch = e.touches[0];
            handleMouseMove({ clientX: touch.clientX, clientY: touch.clientY });
        });
        canvas.addEventListener('touchstart', (e) => {
            e.preventDefault();
            const touch = e.touches[0];
            handleClick({ clientX: touch.clientX, clientY: touch.clientY });
        });

        gameLoop();

        return () => {
            cancelAnimationFrame(animationId);
            window.removeEventListener('resize', resizeCanvas);
            canvas.removeEventListener('mousemove', handleMouseMove);
            canvas.removeEventListener('click', handleClick);
        };
    }, [gameOver, isPaused, level, combo, score, highScore, playSound]);

    const restartGame = () => {
        setScore(0);
        setLives(3);
        setLevel(1);
        setCombo(0);
        setGameOver(false);
        gameStateRef.current.targets = [];
        gameStateRef.current.particles = [];
    };

    return (
        <motion.div
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            className="relative w-full max-w-2xl mx-auto"
        >
            {/* Header */}
            <div className="flex items-center justify-between mb-4 px-2">
                <div className="flex items-center gap-4">
                    <div className="flex items-center gap-2 text-fuchsia-400">
                        <Target size={20} />
                        <span className="font-bold text-xl">{score}</span>
                    </div>
                    <div className="flex items-center gap-1 text-amber-400">
                        <Trophy size={16} />
                        <span className="text-sm">{highScore}</span>
                    </div>
                </div>
                
                <div className="flex items-center gap-4">
                    <div className="flex items-center gap-1">
                        {[...Array(3)].map((_, i) => (
                            <Zap
                                key={i}
                                size={18}
                                className={i < lives ? 'text-cyan-400 fill-cyan-400' : 'text-gray-600'}
                            />
                        ))}
                    </div>
                    <div className="text-xs text-gray-400">LVL {level}</div>
                    {combo > 1 && (
                        <motion.div
                            initial={{ scale: 1.5 }}
                            animate={{ scale: 1 }}
                            className="text-sm font-bold text-fuchsia-400"
                        >
                            x{combo} COMBO!
                        </motion.div>
                    )}
                </div>
            </div>

            {/* Game Canvas */}
            <div className="relative rounded-xl overflow-hidden border border-white/10 bg-[#030712]">
                <canvas
                    ref={canvasRef}
                    className="w-full h-[350px] cursor-crosshair"
                    style={{ touchAction: 'none' }}
                />
                
                {/* Game Over Overlay */}
                {gameOver && (
                    <motion.div
                        initial={{ opacity: 0 }}
                        animate={{ opacity: 1 }}
                        className="absolute inset-0 bg-black/80 backdrop-blur-sm flex flex-col items-center justify-center"
                    >
                        <h3 className="text-3xl font-bold text-white mb-2">GAME OVER</h3>
                        <p className="text-xl text-fuchsia-400 mb-1">Score: {score}</p>
                        {score >= highScore && score > 0 && (
                            <p className="text-amber-400 text-sm mb-4">🏆 NEW HIGH SCORE!</p>
                        )}
                        <div className="flex gap-3 mt-4">
                            <Button onClick={restartGame} className="bg-fuchsia-500 hover:bg-fuchsia-600">
                                <RefreshCw size={16} className="mr-2" />
                                Play Again
                            </Button>
                            <Button onClick={onRetryConnection} variant="outline" className="border-cyan-500 text-cyan-400">
                                Try Server
                            </Button>
                        </div>
                    </motion.div>
                )}

                {/* Controls hint */}
                <div className="absolute bottom-2 left-2 text-xs text-gray-500">
                    Click/tap targets • Don&apos;t let them escape!
                </div>
                
                {/* Sound toggle */}
                <button
                    onClick={() => setSoundEnabled(!soundEnabled)}
                    className="absolute bottom-2 right-2 text-gray-500 hover:text-white transition-colors"
                >
                    {soundEnabled ? <Volume2 size={16} /> : <VolumeX size={16} />}
                </button>
            </div>

            {/* Server message */}
            <div className="mt-4 p-4 rounded-lg bg-amber-500/10 border border-amber-500/30">
                <div className="flex items-start gap-3">
                    <Crosshair className="w-5 h-5 text-amber-400 mt-0.5 flex-shrink-0" />
                    <div>
                        <p className="font-semibold text-amber-300">Server Temporarily Unavailable</p>
                        <p className="text-sm text-amber-300/80 mt-1">
                            Have fun playing while we get things back up! The game auto-checks for server availability.
                        </p>
                    </div>
                </div>
            </div>
            
            <div className="mt-3 flex justify-center">
                <Button 
                    onClick={onRetryConnection} 
                    variant="outline" 
                    size="sm"
                    className="border-cyan-500/50 text-cyan-400 hover:bg-cyan-500/10"
                >
                    <RefreshCw size={14} className="mr-2" />
                    Check Server Status
                </Button>
            </div>
        </motion.div>
    );
}

export default MiniShooterGame;
