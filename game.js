const canvas = document.getElementById('game');
const ctx = canvas.getContext('2d');
const scoreEl = document.getElementById('score');
const restartButton = document.getElementById('restart');

const playerSize = 16;
const baseSpeed = 260;
const maxSpeed = 420;

let state = createInitialState();

function createInitialState() {
  return {
    player: {
      x: canvas.width / 2,
      y: canvas.height - 90,
      vx: 0,
      vy: -baseSpeed,
      dir: 0,
    },
    obstacles: [],
    spawnCooldown: 0.8,
    time: 0,
    gameOver: false,
    message: 'Tap to start drifting',
  };
}

function resetGame() {
  state = createInitialState();
  state.message = 'Tap to change direction';
}

function toggleDirection() {
  if (state.gameOver) {
    resetGame();
    return;
  }

  state.player.dir = state.player.dir === 0 ? -1 : -state.player.dir;
  state.player.vx = state.player.dir * 170;
  state.message = 'Keep going!';
}

canvas.addEventListener('pointerdown', toggleDirection);
window.addEventListener('keydown', (event) => {
  if (event.code === 'Space' || event.code === 'ArrowLeft' || event.code === 'ArrowRight') {
    event.preventDefault();
    toggleDirection();
  }
});
restartButton.addEventListener('click', resetGame);

let lastTimestamp = performance.now();

function loop(timestamp) {
  const delta = Math.min(0.032, (timestamp - lastTimestamp) / 1000);
  lastTimestamp = timestamp;

  update(delta);
  draw();

  requestAnimationFrame(loop);
}

function update(delta) {
  if (!state.gameOver) {
    state.time += delta;
    state.spawnCooldown -= delta;
    if (state.spawnCooldown <= 0) {
      spawnObstacle();
      state.spawnCooldown = Math.max(0.28, 0.8 - state.time * 0.01);
    }

    const player = state.player;
    player.x += player.vx * delta;
    player.y += player.vy * delta;

    if (player.x < playerSize || player.x > canvas.width - playerSize) {
      player.x = Math.max(playerSize, Math.min(canvas.width - playerSize, player.x));
      player.dir = 0;
      player.vx = 0;
      state.message = 'Bounced off the wall';
    }

    state.obstacles.forEach((obstacle) => {
      obstacle.y += obstacle.speed * delta;
      obstacle.x += obstacle.sway * delta;
      if (obstacle.x < -120) obstacle.x = -120;
      if (obstacle.x > canvas.width - obstacle.w + 120) obstacle.x = canvas.width - obstacle.w + 120;
    });

    state.obstacles = state.obstacles.filter((obstacle) => obstacle.y < canvas.height + 60);

    const collision = state.obstacles.some((obstacle) => intersects(player, obstacle));
    if (collision) {
      state.gameOver = true;
      state.message = `Crash! You lasted ${Math.floor(state.time)}s`;
    }
  }

  scoreEl.textContent = Math.floor(state.time);
}

function spawnObstacle() {
  const width = 60 + Math.random() * 110;
  const height = 16 + Math.random() * 24;
  const x = Math.random() * (canvas.width - width);
  const speed = 220 + Math.random() * 80 + Math.min(90, state.time * 6);

  state.obstacles.push({
    x,
    y: -height,
    w: width,
    h: height,
    speed,
    sway: (Math.random() > 0.5 ? 1 : -1) * (40 + Math.random() * 30),
  });
}

function intersects(player, obstacle) {
  return (
    player.x - playerSize < obstacle.x + obstacle.w &&
    player.x + playerSize > obstacle.x &&
    player.y - playerSize < obstacle.y + obstacle.h &&
    player.y + playerSize > obstacle.y
  );
}

function draw() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
  gradient.addColorStop(0, '#071129');
  gradient.addColorStop(0.45, '#0d1832');
  gradient.addColorStop(1, '#060814');
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  drawStars();
  drawGuideLines();

  state.obstacles.forEach((obstacle) => {
    ctx.fillStyle = '#ff5d7b';
    roundRect(ctx, obstacle.x, obstacle.y, obstacle.w, obstacle.h, 10);
    ctx.fill();
  });

  const player = state.player;
  ctx.save();
  ctx.translate(player.x, player.y);
  ctx.fillStyle = '#7bffcf';
  ctx.beginPath();
  ctx.moveTo(0, -playerSize);
  ctx.lineTo(playerSize, playerSize);
  ctx.lineTo(-playerSize, playerSize);
  ctx.closePath();
  ctx.fill();
  ctx.restore();

  ctx.fillStyle = 'rgba(255,255,255,0.72)';
  ctx.font = '700 18px Inter, sans-serif';
  ctx.textAlign = 'center';
  ctx.fillText(state.message, canvas.width / 2, 40);
}

function drawStars() {
  ctx.save();
  ctx.fillStyle = 'rgba(255,255,255,0.75)';
  for (let i = 0; i < 60; i += 1) {
    const x = (i * 97) % canvas.width;
    const y = (i * 31) % canvas.height;
    const radius = (i % 3) + 1;
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}

function drawGuideLines() {
  ctx.save();
  ctx.strokeStyle = 'rgba(255,255,255,0.12)';
  ctx.lineWidth = 1;
  for (let y = 0; y < canvas.height; y += 80) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(canvas.width, y);
    ctx.stroke();
  }
  ctx.restore();
}

function roundRect(context, x, y, width, height, radius) {
  context.beginPath();
  context.moveTo(x + radius, y);
  context.arcTo(x + width, y, x + width, y + height, radius);
  context.arcTo(x + width, y + height, x, y + height, radius);
  context.arcTo(x, y + height, x, y, radius);
  context.arcTo(x, y, x + width, y, radius);
  context.closePath();
}

requestAnimationFrame(loop);
