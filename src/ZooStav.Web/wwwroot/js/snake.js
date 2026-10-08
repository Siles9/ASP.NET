(function () {
    'use strict';

    var canvas = document.getElementById('snakeCanvas');
    if (!canvas) {
        return;
    }

    var ctx = canvas.getContext('2d');
    var scoreEl = document.getElementById('snakeScore');
    var bestEl = document.getElementById('snakeBest');
    var startBtn = document.getElementById('snakeStart');

    var CELL = 20;
    var COLS = canvas.width / CELL;
    var ROWS = canvas.height / CELL;
    var BEST_KEY = 'zooSnakeBest';
    var FUR = '#9a9ca1';
    var STRIPE = '#2f2f33';
    var FISH = '#e8a33d';

    var MOVES = {
        ArrowUp: [0, -1], ArrowDown: [0, 1], ArrowLeft: [-1, 0], ArrowRight: [1, 0],
        w: [0, -1], s: [0, 1], a: [-1, 0], d: [1, 0],
        W: [0, -1], S: [0, 1], A: [-1, 0], D: [1, 0]
    };

    var snake = [];
    var dir = { x: 1, y: 0 };
    var nextDir = { x: 1, y: 0 };
    var fish = null;
    var score = 0;
    var state = 'idle';
    var timer = null;
    var best = readBest();

    bestEl.textContent = best;

    function readBest() {
        try {
            return parseInt(localStorage.getItem(BEST_KEY), 10) || 0;
        } catch (e) {
            return 0;
        }
    }

    function saveBest(value) {
        try {
            localStorage.setItem(BEST_KEY, String(value));
        } catch (e) {
        }
    }

    function isOccupied(x, y, cells) {
        return cells.some(function (c) { return c.x === x && c.y === y; });
    }

    function placeFish() {
        var free = [];
        for (var x = 0; x < COLS; x++) {
            for (var y = 0; y < ROWS; y++) {
                if (!isOccupied(x, y, snake)) {
                    free.push({ x: x, y: y });
                }
            }
        }
        fish = free.length ? free[Math.floor(Math.random() * free.length)] : null;
    }

    function reset() {
        snake = [{ x: 8, y: 8 }, { x: 7, y: 8 }, { x: 6, y: 8 }];
        dir = { x: 1, y: 0 };
        nextDir = { x: 1, y: 0 };
        score = 0;
        scoreEl.textContent = score;
        placeFish();
    }

    function gameOver() {
        state = 'over';
        clearTimeout(timer);
        draw();
    }

    function step() {
        dir = nextDir;
        var head = { x: snake[0].x + dir.x, y: snake[0].y + dir.y };

        if (head.x < 0 || head.y < 0 || head.x >= COLS || head.y >= ROWS) {
            return gameOver();
        }

        var eats = fish !== null && head.x === fish.x && head.y === fish.y;
        var body = eats ? snake : snake.slice(0, -1);
        if (isOccupied(head.x, head.y, body)) {
            return gameOver();
        }

        snake.unshift(head);

        if (eats) {
            score += 1;
            scoreEl.textContent = score;
            if (score > best) {
                best = score;
                bestEl.textContent = best;
                saveBest(best);
            }
            placeFish();
        } else {
            snake.pop();
        }

        draw();
    }

    function schedule() {
        timer = setTimeout(function () {
            step();
            if (state === 'playing') {
                schedule();
            }
        }, Math.max(70, 150 - score * 4));
    }

    function start() {
        clearTimeout(timer);
        reset();
        state = 'playing';
        startBtn.textContent = 'Заново';
        draw();
        schedule();
    }

    function turn(vx, vy) {
        if (state !== 'playing') {
            return;
        }
        if (vx === -dir.x && vy === -dir.y) {
            return;
        }
        nextDir = { x: vx, y: vy };
    }

    function center(cell) {
        return { x: cell.x * CELL + CELL / 2, y: cell.y * CELL + CELL / 2 };
    }

    function drawBackground() {
        ctx.fillStyle = '#e7f2ec';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.strokeStyle = 'rgba(29, 75, 58, 0.07)';
        ctx.lineWidth = 1;
        for (var i = 0; i <= COLS; i++) {
            ctx.beginPath();
            ctx.moveTo(i * CELL + 0.5, 0);
            ctx.lineTo(i * CELL + 0.5, canvas.height);
            ctx.stroke();
        }
        for (var j = 0; j <= ROWS; j++) {
            ctx.beginPath();
            ctx.moveTo(0, j * CELL + 0.5);
            ctx.lineTo(canvas.width, j * CELL + 0.5);
            ctx.stroke();
        }
    }

    function drawFish(cell) {
        var c = center(cell);
        ctx.fillStyle = FISH;
        ctx.beginPath();
        ctx.ellipse(c.x - 2, c.y, 7, 4.5, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.moveTo(c.x + 4, c.y);
        ctx.lineTo(c.x + 9, c.y - 4);
        ctx.lineTo(c.x + 9, c.y + 4);
        ctx.closePath();
        ctx.fill();
        ctx.fillStyle = STRIPE;
        ctx.beginPath();
        ctx.arc(c.x - 5, c.y - 1, 1.2, 0, Math.PI * 2);
        ctx.fill();
    }

    function drawTailSegment(cell, index) {
        var c = center(cell);
        ctx.beginPath();
        ctx.arc(c.x, c.y, CELL / 2 - 1, 0, Math.PI * 2);
        ctx.fillStyle = index % 3 === 0 ? STRIPE : FUR;
        ctx.fill();
    }

    function drawHead(cell, d) {
        var c = center(cell);
        var r = CELL / 2 + 1;
        var fx = d.x * r * 0.35;
        var fy = d.y * r * 0.35;
        var sx = -d.y * r * 0.5;
        var sy = d.x * r * 0.5;

        ctx.fillStyle = FUR;
        ctx.beginPath();
        ctx.arc(c.x, c.y, r, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = STRIPE;
        ctx.beginPath();
        ctx.arc(c.x + fx * 0.6, c.y + fy * 0.6, r * 0.5, 0, Math.PI * 2);
        ctx.fill();

        [1, -1].forEach(function (side) {
            var ex = c.x + fx + sx * side;
            var ey = c.y + fy + sy * side;
            ctx.fillStyle = '#ffffff';
            ctx.beginPath();
            ctx.arc(ex, ey, 2.4, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = STRIPE;
            ctx.beginPath();
            ctx.arc(ex + fx * 0.25, ey + fy * 0.25, 1.2, 0, Math.PI * 2);
            ctx.fill();
        });

        ctx.fillStyle = STRIPE;
        ctx.beginPath();
        ctx.arc(c.x + fx * 1.4, c.y + fy * 1.4, 1.6, 0, Math.PI * 2);
        ctx.fill();
    }

    function drawOverlay(title, subtitle) {
        ctx.fillStyle = 'rgba(16, 38, 31, 0.6)';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = '#ffffff';
        ctx.textAlign = 'center';
        ctx.font = '600 20px "Segoe UI", system-ui, sans-serif';
        ctx.fillText(title, canvas.width / 2, canvas.height / 2 - 6);
        ctx.font = '14px "Segoe UI", system-ui, sans-serif';
        ctx.fillText(subtitle, canvas.width / 2, canvas.height / 2 + 18);
    }

    function draw() {
        drawBackground();
        if (fish) {
            drawFish(fish);
        }
        for (var i = snake.length - 1; i >= 1; i--) {
            drawTailSegment(snake[i], i);
        }
        if (snake.length) {
            drawHead(snake[0], dir);
        }
        if (state === 'idle') {
            drawOverlay('Енот ждёт рыбку', 'Нажмите «Начать»');
        } else if (state === 'over') {
            drawOverlay('Игра окончена', 'Счёт: ' + score + '. Нажмите «Заново»');
        }
    }

    document.addEventListener('keydown', function (e) {
        var move = MOVES[e.key];
        if (!move || state !== 'playing') {
            return;
        }
        e.preventDefault();
        turn(move[0], move[1]);
    });

    var touchStart = null;

    canvas.addEventListener('touchstart', function (e) {
        var t = e.touches[0];
        touchStart = { x: t.clientX, y: t.clientY };
    }, { passive: true });

    canvas.addEventListener('touchend', function (e) {
        if (!touchStart) {
            return;
        }
        var t = e.changedTouches[0];
        var dx = t.clientX - touchStart.x;
        var dy = t.clientY - touchStart.y;
        touchStart = null;
        if (Math.max(Math.abs(dx), Math.abs(dy)) < 20) {
            return;
        }
        if (Math.abs(dx) > Math.abs(dy)) {
            turn(dx > 0 ? 1 : -1, 0);
        } else {
            turn(0, dy > 0 ? 1 : -1);
        }
    });

    startBtn.addEventListener('click', start);

    reset();
    draw();
})();
