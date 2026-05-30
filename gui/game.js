const WIDTH = 20;
const HEIGHT = 10;
const MAX_AMMO = 6;
const MAX_HP = 3;
const MAX_WAVE = 3;
const ENEMIES_PER_WAVE = 3;

const directions = {
  up: { x: 0, y: -1 },
  down: { x: 0, y: 1 },
  left: { x: -1, y: 0 },
  right: { x: 1, y: 0 },
};

const assetPaths = {
  player: "./assets/player.svg",
  enemy: "./assets/enemy.svg",
  bullet: "./assets/bullet.svg",
  wall: "./assets/wall.svg",
  cover: "./assets/cover.svg",
};

const waveSpawnPlan = new Map([
  [1, [{ x: 15, y: 1 }, { x: 16, y: 4 }, { x: 12, y: 8 }, { x: 17, y: 6 }, { x: 3, y: 8 }]],
  [2, [{ x: 17, y: 1 }, { x: 15, y: 5 }, { x: 2, y: 8 }, { x: 12, y: 1 }, { x: 17, y: 7 }]],
  [3, [{ x: 2, y: 1 }, { x: 16, y: 1 }, { x: 17, y: 8 }, { x: 11, y: 5 }, { x: 2, y: 6 }]],
]);

const boardElement = document.querySelector("#board");
const messageText = document.querySelector("#messageText");
const scoreValue = document.querySelector("#scoreValue");
const waveValue = document.querySelector("#waveValue");
const turnLabel = document.querySelector("#turnLabel");
const statusBadge = document.querySelector("#statusBadge");
const hpPips = document.querySelector("#hpPips");
const ammoPips = document.querySelector("#ammoPips");
const restartButton = document.querySelector("#restartButton");
const commandButtons = Array.from(document.querySelectorAll("[data-command]"));

const wallPositions = buildWallPositions();
const coverPositions = [
  { x: 4, y: 2 },
  { x: 5, y: 2 },
  { x: 14, y: 3 },
  { x: 15, y: 3 },
  { x: 8, y: 7 },
  { x: 9, y: 7 },
  { x: 14, y: 7 },
];

const allCandidateSpawns = [];
for (let y = 1; y < HEIGHT - 1; y += 1) {
  for (let x = 1; x < WIDTH - 1; x += 1) {
    allCandidateSpawns.push({ x, y });
  }
}

let state = createInitialState();
const cells = [];

function buildWallPositions() {
  const result = [];

  for (let x = 0; x < WIDTH; x += 1) {
    result.push({ x, y: 0 });
    result.push({ x, y: HEIGHT - 1 });
  }

  for (let y = 0; y < HEIGHT; y += 1) {
    result.push({ x: 0, y });
    result.push({ x: WIDTH - 1, y });
  }

  result.push({ x: 10, y: 2 });
  result.push({ x: 10, y: 3 });
  result.push({ x: 10, y: 4 });
  result.push({ x: 5, y: 6 });
  result.push({ x: 6, y: 6 });
  result.push({ x: 13, y: 6 });

  return uniquePositions(result);
}

function createInitialState() {
  const baseState = {
    width: WIDTH,
    height: HEIGHT,
    player: { x: 2, y: 3 },
    enemies: [],
    bullets: [],
    walls: wallPositions.slice(),
    cover: coverPositions.slice(),
    hp: MAX_HP,
    ammo: MAX_AMMO,
    score: 0,
    wave: 1,
    maxWave: MAX_WAVE,
    enemiesPerWave: ENEMIES_PER_WAVE,
    turn: 1,
    lastMessage: "Mission start. Clear every raider in the arena.",
    gameOver: false,
    victory: false,
  };

  return {
    ...baseState,
    enemies: spawnEnemies(1, ENEMIES_PER_WAVE, baseState.player, baseState.walls, baseState.cover),
  };
}

function uniquePositions(positions) {
  const seen = new Set();
  return positions.filter((position) => {
    const key = toKey(position);
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function spawnEnemies(wave, count, player, walls, cover) {
  const requested = waveSpawnPlan.get(wave) ?? [];
  return uniquePositions([...requested, ...allCandidateSpawns])
    .filter((position) =>
      !samePosition(position, player)
      && !containsPosition(walls, position)
      && !containsPosition(cover, position))
    .slice(0, count);
}

function samePosition(a, b) {
  return a.x === b.x && a.y === b.y;
}

function containsPosition(list, position) {
  return list.some((entry) => samePosition(entry, position));
}

function removePosition(list, position) {
  return list.filter((entry) => !samePosition(entry, position));
}

function movePosition(position, directionName) {
  const direction = directions[directionName];
  return { x: position.x + direction.x, y: position.y + direction.y };
}

function inBounds(position) {
  return position.x >= 0 && position.x < WIDTH && position.y >= 0 && position.y < HEIGHT;
}

function toKey(position) {
  return `${position.x},${position.y}`;
}

function applyCommand(currentState, commandName) {
  if (currentState.gameOver || currentState.victory) {
    return currentState;
  }

  let nextState = {
    ...currentState,
    lastMessage: "",
  };

  if (commandName.startsWith("move-")) {
    const directionName = commandName.replace("move-", "");
    nextState = movePlayer(nextState, directionName);
  } else if (commandName.startsWith("shoot-")) {
    const directionName = commandName.replace("shoot-", "");
    nextState = fireBullet(nextState, directionName);
  } else if (commandName === "reload") {
    nextState = { ...nextState, ammo: MAX_AMMO, lastMessage: "Magazine refreshed." };
  } else if (commandName === "wait") {
    nextState = { ...nextState, lastMessage: "You hold position." };
  }

  nextState = updateBullets(nextState);
  nextState = updateEnemies(nextState);
  nextState = advanceWaveIfNeeded(nextState);
  nextState = finalizeState(nextState);

  return {
    ...nextState,
    turn: nextState.turn + 1,
  };
}

function movePlayer(currentState, directionName) {
  const target = movePosition(currentState.player, directionName);

  if (
    !inBounds(target)
    || containsPosition(currentState.walls, target)
    || containsPosition(currentState.cover, target)
    || containsPosition(currentState.enemies, target)
  ) {
    return { ...currentState, lastMessage: "That route is blocked." };
  }

  return {
    ...currentState,
    player: target,
    lastMessage: "You reposition.",
  };
}

function fireBullet(currentState, directionName) {
  if (currentState.ammo <= 0) {
    return { ...currentState, lastMessage: "Out of ammo. Reload first." };
  }

  const spawnPosition = movePosition(currentState.player, directionName);
  let nextState = {
    ...currentState,
    ammo: currentState.ammo - 1,
  };

  if (!inBounds(spawnPosition)) {
    return { ...nextState, lastMessage: "The shot dissipates at the arena edge." };
  }

  if (containsPosition(nextState.walls, spawnPosition)) {
    return { ...nextState, lastMessage: "Your shot sparks against a wall." };
  }

  if (containsPosition(nextState.cover, spawnPosition)) {
    return {
      ...nextState,
      cover: removePosition(nextState.cover, spawnPosition),
      lastMessage: "You blast a chunk out of cover.",
    };
  }

  if (containsPosition(nextState.enemies, spawnPosition)) {
    return {
      ...nextState,
      enemies: removePosition(nextState.enemies, spawnPosition),
      score: nextState.score + 10,
      lastMessage: "Direct hit. Raider eliminated.",
    };
  }

  return {
    ...nextState,
    bullets: [
      ...nextState.bullets,
      {
        position: spawnPosition,
        direction: directionName,
        fresh: true,
      },
    ],
    lastMessage: "Shot fired.",
  };
}

function updateBullets(currentState) {
  const keptBullets = [];
  let nextState = { ...currentState };
  let collisionMessage = "";

  currentState.bullets.forEach((bullet) => {
    if (bullet.fresh) {
      keptBullets.push({ ...bullet, fresh: false });
      return;
    }

    const nextPosition = movePosition(bullet.position, bullet.direction);
    if (!inBounds(nextPosition)) {
      return;
    }

    if (containsPosition(nextState.walls, nextPosition)) {
      if (!collisionMessage) {
        collisionMessage = "A round shatters against the wall.";
      }
      return;
    }

    if (containsPosition(nextState.cover, nextPosition)) {
      nextState = {
        ...nextState,
        cover: removePosition(nextState.cover, nextPosition),
      };
      if (!collisionMessage) {
        collisionMessage = "A round destroys cover.";
      }
      return;
    }

    if (containsPosition(nextState.enemies, nextPosition)) {
      nextState = {
        ...nextState,
        enemies: removePosition(nextState.enemies, nextPosition),
        score: nextState.score + 10,
      };
      if (!collisionMessage) {
        collisionMessage = "Enemy down.";
      }
      return;
    }

    keptBullets.push({
      ...bullet,
      position: nextPosition,
    });
  });

  return {
    ...nextState,
    bullets: keptBullets,
    lastMessage: collisionMessage || nextState.lastMessage,
  };
}

function updateEnemies(currentState) {
  let hpLost = 0;
  let scoreGained = 0;
  let message = currentState.lastMessage;
  const movedEnemies = [];
  let remainingBullets = currentState.bullets.slice();

  currentState.enemies.forEach((enemy) => {
    const nextPosition = moveEnemyTowardPlayer(currentState, enemy, movedEnemies);

    if (samePosition(nextPosition, currentState.player)) {
      hpLost += 1;
      message = `A raider hits you. HP -${hpLost}.`;
      return;
    }

    const bulletAtTile = remainingBullets.find((bullet) => samePosition(bullet.position, nextPosition));
    if (bulletAtTile) {
      remainingBullets = remainingBullets.filter((bullet) => !samePosition(bullet.position, nextPosition));
      scoreGained += 10;
      message = "A raider charges straight into your fire.";
      return;
    }

    movedEnemies.push(nextPosition);
  });

  return {
    ...currentState,
    enemies: movedEnemies,
    bullets: remainingBullets,
    hp: Math.max(0, currentState.hp - hpLost),
    score: currentState.score + scoreGained,
    lastMessage: message,
  };
}

function moveEnemyTowardPlayer(currentState, enemy, movedEnemies) {
  const dx = currentState.player.x - enemy.x;
  const dy = currentState.player.y - enemy.y;

  let primaryDirection = "down";
  if (Math.abs(dx) > Math.abs(dy)) {
    primaryDirection = dx < 0 ? "left" : "right";
  } else if (dy < 0) {
    primaryDirection = "up";
  }

  const target = movePosition(enemy, primaryDirection);
  const blocked =
    !inBounds(target)
    || containsPosition(currentState.walls, target)
    || containsPosition(movedEnemies, target);

  return blocked ? enemy : target;
}

function advanceWaveIfNeeded(currentState) {
  if (currentState.enemies.length !== 0) {
    return currentState;
  }

  if (currentState.wave >= currentState.maxWave) {
    return currentState;
  }

  const nextWave = currentState.wave + 1;
  return {
    ...currentState,
    wave: nextWave,
    bullets: [],
    enemies: spawnEnemies(
      nextWave,
      currentState.enemiesPerWave,
      currentState.player,
      currentState.walls,
      currentState.cover,
    ),
    lastMessage: `Wave ${nextWave} incoming.`,
  };
}

function finalizeState(currentState) {
  if (currentState.hp <= 0) {
    return {
      ...currentState,
      gameOver: true,
      lastMessage: "The arena falls silent. Mission failed.",
    };
  }

  if (currentState.wave === currentState.maxWave && currentState.enemies.length === 0) {
    return {
      ...currentState,
      victory: true,
      lastMessage: "All waves cleared. Arena secured.",
    };
  }

  return currentState;
}

function createBoard() {
  for (let y = 0; y < HEIGHT; y += 1) {
    for (let x = 0; x < WIDTH; x += 1) {
      const cell = document.createElement("div");
      cell.className = "cell";
      cell.dataset.x = String(x);
      cell.dataset.y = String(y);
      boardElement.appendChild(cell);
      cells.push(cell);
    }
  }
}

function render() {
  cells.forEach((cell) => {
    cell.innerHTML = "";
  });

  renderPositions(state.walls, "wall", assetPaths.wall);
  renderPositions(state.cover, "cover", assetPaths.cover);
  renderPositions(state.enemies, "enemy", assetPaths.enemy);
  renderPositions(state.bullets.map((bullet) => bullet.position), "bullet", assetPaths.bullet);
  renderPositions([state.player], "player", assetPaths.player);

  scoreValue.textContent = String(state.score);
  waveValue.textContent = String(state.wave);
  turnLabel.textContent = `Turn ${state.turn}`;
  messageText.textContent = state.lastMessage;
  renderPips(hpPips, state.hp, MAX_HP, "health");
  renderPips(ammoPips, state.ammo, MAX_AMMO, "ammo");

  statusBadge.className = "status-badge";
  if (state.victory) {
    statusBadge.textContent = "Victory";
  } else if (state.gameOver) {
    statusBadge.textContent = "Defeat";
    statusBadge.classList.add("danger");
  } else if (state.hp === 1) {
    statusBadge.textContent = "Critical";
    statusBadge.classList.add("danger");
  } else if (state.ammo <= 1) {
    statusBadge.textContent = "Low Ammo";
    statusBadge.classList.add("warning");
  } else {
    statusBadge.textContent = "Mission Active";
  }

  boardElement.classList.remove("flash");
  void boardElement.offsetWidth;
  boardElement.classList.add("flash");
}

function renderPositions(positions, type, assetPath) {
  positions.forEach((position) => {
    const cell = cellAt(position.x, position.y);
    if (!cell) {
      return;
    }

    const entity = document.createElement("div");
    entity.className = `entity ${type}`;

    const image = document.createElement("img");
    image.src = assetPath;
    image.alt = "";
    entity.appendChild(image);
    cell.appendChild(entity);
  });
}

function renderPips(container, currentValue, maxValue, type) {
  container.innerHTML = "";
  for (let index = 0; index < maxValue; index += 1) {
    const pip = document.createElement("span");
    pip.className = `pip ${index < currentValue ? `active ${type}` : ""}`;
    container.appendChild(pip);
  }
}

function cellAt(x, y) {
  return cells[y * WIDTH + x];
}

function handleCommand(commandName) {
  state = applyCommand(state, commandName);
  render();
}

function restartGame() {
  state = createInitialState();
  render();
}

function handleKeydown(event) {
  if (event.repeat) {
    return;
  }

  const tagName = event.target instanceof HTMLElement ? event.target.tagName : "";
  if (tagName === "BUTTON") {
    return;
  }

  const key = event.key.toLowerCase();
  const keyToCommand = {
    w: "move-up",
    a: "move-left",
    s: "move-down",
    d: "move-right",
    arrowup: "shoot-up",
    arrowdown: "shoot-down",
    arrowleft: "shoot-left",
    arrowright: "shoot-right",
    r: "reload",
    " ": "wait",
  };

  const commandName = keyToCommand[key];
  if (!commandName) {
    return;
  }

  event.preventDefault();
  handleCommand(commandName);
}

createBoard();
render();

commandButtons.forEach((button) => {
  button.addEventListener("click", () => {
    handleCommand(button.dataset.command);
  });
});

restartButton.addEventListener("click", restartGame);
window.addEventListener("keydown", handleKeydown);
