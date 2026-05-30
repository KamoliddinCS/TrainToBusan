open System
open System.Diagnostics
open System.Text
open System.Threading

type Direction =
    | Up
    | Down
    | Left
    | Right

type Position = { X: int; Y: int }

type Bullet =
    { Position: Position
      Direction: Direction }

type MapDefinition =
    { Name: string
      Description: string
      Width: int
      Height: int
      PlayerStart: Position
      InteriorWalls: Set<Position>
      Cover: Set<Position>
      WaveSpawnPlan: Map<int, Position list>
      EnemiesPerWave: int
      MaxWave: int }

type Command =
    | Move of Direction
    | Shoot of Direction
    | Reload
    | TogglePause
    | Restart
    | ReturnToMenu
    | Quit

type GameState =
    { Map: MapDefinition
      Player: Position
      Enemies: Position list
      Bullets: Bullet list
      Walls: Set<Position>
      Cover: Set<Position>
      Hp: int
      Ammo: int
      Score: int
      Wave: int
      Frame: int
      BulletStepEvery: int
      EnemyStepEvery: int
      LastMessage: string
      IsPaused: bool
      ShouldQuit: bool
      ShouldReturnToMenu: bool }

let maxAmmo = 6
let startingHp = 3
let frameDurationMs = 60L

let movePosition direction position =
    match direction with
    | Up -> { position with Y = position.Y - 1 }
    | Down -> { position with Y = position.Y + 1 }
    | Left -> { position with X = position.X - 1 }
    | Right -> { position with X = position.X + 1 }

let inBounds width height position =
    position.X >= 0
    && position.X < width
    && position.Y >= 0
    && position.Y < height

let boundaryWalls width height =
    seq {
        for x in 0 .. width - 1 do
            yield { X = x; Y = 0 }
            yield { X = x; Y = height - 1 }

        for y in 0 .. height - 1 do
            yield { X = 0; Y = y }
            yield { X = width - 1; Y = y }
    }
    |> Set.ofSeq

let makeMap
    name
    description
    width
    height
    playerStart
    wallPositions
    coverPositions
    waveSpawnPlan
    enemiesPerWave
    maxWave
    =
    { Name = name
      Description = description
      Width = width
      Height = height
      PlayerStart = playerStart
      InteriorWalls = wallPositions |> Set.ofList
      Cover = coverPositions |> Set.ofList
      WaveSpawnPlan = waveSpawnPlan |> Map.ofList
      EnemiesPerWave = enemiesPerWave
      MaxWave = maxWave }

let availableMaps =
    [ makeMap
          "Training Yard"
          "20x10 | Tight arena with quick flanks and short sight lines."
          20
          10
          { X = 2; Y = 3 }
          [ { X = 10; Y = 2 }
            { X = 10; Y = 3 }
            { X = 10; Y = 4 }
            { X = 5; Y = 6 }
            { X = 6; Y = 6 }
            { X = 13; Y = 6 } ]
          [ { X = 4; Y = 2 }
            { X = 5; Y = 2 }
            { X = 14; Y = 3 }
            { X = 15; Y = 3 }
            { X = 8; Y = 7 }
            { X = 9; Y = 7 }
            { X = 14; Y = 7 } ]
          [ 1,
            [ { X = 15; Y = 1 }
              { X = 16; Y = 4 }
              { X = 12; Y = 8 }
              { X = 17; Y = 6 }
              { X = 3; Y = 8 } ]
            2,
            [ { X = 17; Y = 1 }
              { X = 15; Y = 5 }
              { X = 2; Y = 8 }
              { X = 12; Y = 1 }
              { X = 17; Y = 7 } ]
            3,
            [ { X = 2; Y = 1 }
              { X = 16; Y = 1 }
              { X = 17; Y = 8 }
              { X = 11; Y = 5 }
              { X = 2; Y = 6 } ] ]
          3
          3
      makeMap
          "Crossfire Depot"
          "28x14 | Mid-sized map with split lanes and safer reload pockets."
          28
          14
          { X = 3; Y = 6 }
          [ { X = 8; Y = 3 }
            { X = 8; Y = 4 }
            { X = 8; Y = 5 }
            { X = 14; Y = 2 }
            { X = 14; Y = 3 }
            { X = 14; Y = 4 }
            { X = 14; Y = 9 }
            { X = 14; Y = 10 }
            { X = 20; Y = 8 }
            { X = 20; Y = 9 }
            { X = 20; Y = 10 }
            { X = 5; Y = 10 }
            { X = 6; Y = 10 }
            { X = 21; Y = 4 }
            { X = 22; Y = 4 } ]
          [ { X = 5; Y = 3 }
            { X = 6; Y = 3 }
            { X = 10; Y = 8 }
            { X = 11; Y = 8 }
            { X = 17; Y = 5 }
            { X = 18; Y = 5 }
            { X = 23; Y = 10 }
            { X = 24; Y = 10 }
            { X = 12; Y = 11 }
            { X = 13; Y = 11 } ]
          [ 1,
            [ { X = 23; Y = 2 }
              { X = 24; Y = 6 }
              { X = 19; Y = 11 }
              { X = 10; Y = 2 }
              { X = 25; Y = 9 } ]
            2,
            [ { X = 24; Y = 2 }
              { X = 3; Y = 2 }
              { X = 18; Y = 2 }
              { X = 22; Y = 11 }
              { X = 6; Y = 11 }
              { X = 25; Y = 6 } ]
            3,
            [ { X = 2; Y = 2 }
              { X = 25; Y = 2 }
              { X = 25; Y = 11 }
              { X = 2; Y = 11 }
              { X = 18; Y = 6 }
              { X = 11; Y = 11 } ] ]
          4
          4
      makeMap
          "Fortress Run"
          "34x16 | Large arena with long corridors and more sustained pressure."
          34
          16
          { X = 4; Y = 8 }
          [ { X = 9; Y = 3 }
            { X = 9; Y = 4 }
            { X = 9; Y = 5 }
            { X = 9; Y = 6 }
            { X = 16; Y = 5 }
            { X = 17; Y = 5 }
            { X = 18; Y = 5 }
            { X = 25; Y = 3 }
            { X = 25; Y = 4 }
            { X = 25; Y = 5 }
            { X = 12; Y = 10 }
            { X = 13; Y = 10 }
            { X = 14; Y = 10 }
            { X = 21; Y = 9 }
            { X = 21; Y = 10 }
            { X = 21; Y = 11 }
            { X = 28; Y = 10 }
            { X = 29; Y = 10 }
            { X = 30; Y = 10 } ]
          [ { X = 6; Y = 3 }
            { X = 7; Y = 3 }
            { X = 11; Y = 7 }
            { X = 12; Y = 7 }
            { X = 19; Y = 3 }
            { X = 20; Y = 3 }
            { X = 24; Y = 8 }
            { X = 25; Y = 8 }
            { X = 15; Y = 12 }
            { X = 16; Y = 12 }
            { X = 26; Y = 12 }
            { X = 27; Y = 12 } ]
          [ 1,
            [ { X = 28; Y = 2 }
              { X = 30; Y = 6 }
              { X = 25; Y = 13 }
              { X = 18; Y = 2 }
              { X = 31; Y = 10 } ]
            2,
            [ { X = 30; Y = 2 }
              { X = 3; Y = 2 }
              { X = 16; Y = 2 }
              { X = 30; Y = 13 }
              { X = 6; Y = 13 }
              { X = 24; Y = 6 } ]
            3,
            [ { X = 2; Y = 2 }
              { X = 31; Y = 2 }
              { X = 31; Y = 13 }
              { X = 2; Y = 13 }
              { X = 17; Y = 8 }
              { X = 27; Y = 4 }
              { X = 12; Y = 12 } ] ]
          5
          5 ]

let defaultMap = availableMaps.Head

let allCandidateSpawns map =
    seq {
        for y in 1 .. map.Height - 2 do
            for x in 1 .. map.Width - 2 do
                yield { X = x; Y = y }
    }
    |> List.ofSeq

let buildWalls map =
    Set.union (boundaryWalls map.Width map.Height) map.InteriorWalls

let isBlockedByTerrain (state: GameState) position =
    state.Walls.Contains position || state.Cover.Contains position

let spawnEnemies (map: MapDefinition) (wave: int) (player: Position) (walls: Set<Position>) (cover: Set<Position>) =
    let requested =
        map.WaveSpawnPlan
        |> Map.tryFind wave
        |> Option.defaultValue []

    let count = min map.EnemiesPerWave (wave + 2)

    (requested @ allCandidateSpawns map)
    |> List.filter (fun pos -> pos <> player && not (walls.Contains pos) && not (cover.Contains pos))
    |> List.distinct
    |> List.truncate count

let makeInitialState map =
    let walls = buildWalls map
    let wave = 1

    { Map = map
      Player = map.PlayerStart
      Enemies = spawnEnemies map wave map.PlayerStart walls map.Cover
      Bullets = []
      Walls = walls
      Cover = map.Cover
      Hp = startingHp
      Ammo = maxAmmo
      Score = 0
      Wave = wave
      Frame = 0
      BulletStepEvery = 2
      EnemyStepEvery = 5
      LastMessage = $"Arena live on {map.Name}."
      IsPaused = false
      ShouldQuit = false
      ShouldReturnToMenu = false }

let hasWon state =
    state.Wave = state.Map.MaxWave && List.isEmpty state.Enemies

let isFinished state =
    state.Hp <= 0 || hasWon state

let tryMovePlayer direction state =
    if state.IsPaused || isFinished state then
        state
    else
        let target = movePosition direction state.Player

        if
            not (inBounds state.Map.Width state.Map.Height target)
            || isBlockedByTerrain state target
            || (state.Enemies |> List.contains target)
        then
            { state with LastMessage = "Blocked." }
        else
            { state with
                Player = target
                LastMessage = "" }

let resolveBulletSpawnCollision direction state spawnPosition =
    if not (inBounds state.Map.Width state.Map.Height spawnPosition) then
        { state with LastMessage = "The shot hits the edge of the arena." }
    elif state.Walls.Contains spawnPosition then
        { state with LastMessage = "The shot slams into a wall." }
    elif state.Cover.Contains spawnPosition then
        { state with
            Cover = state.Cover.Remove spawnPosition
            LastMessage = "Your shot destroys cover." }
    elif state.Enemies |> List.contains spawnPosition then
        { state with
            Enemies = state.Enemies |> List.filter ((<>) spawnPosition)
            Score = state.Score + 10
            LastMessage = "Direct hit." }
    else
        { state with
            Bullets =
                { Position = spawnPosition
                  Direction = direction }
                :: state.Bullets
            LastMessage = "Shot fired." }

let fireBullet direction state =
    if state.IsPaused || isFinished state then
        state
    elif state.Ammo <= 0 then
        { state with LastMessage = "Out of ammo. Press R to reload." }
    else
        let spawnPosition = movePosition direction state.Player

        resolveBulletSpawnCollision
            direction
            { state with
                Ammo = state.Ammo - 1
                LastMessage = "" }
            spawnPosition

let shouldAdvanceWave state =
    List.isEmpty state.Enemies && state.Wave < state.Map.MaxWave

let tryAdvanceWave state =
    if shouldAdvanceWave state then
        let nextWave = state.Wave + 1
        let spawned = spawnEnemies state.Map nextWave state.Player state.Walls state.Cover

        { state with
            Wave = nextWave
            Enemies = spawned
            Bullets = []
            LastMessage = $"Wave {nextWave} begins on {state.Map.Name}." }
    else
        state

let advanceBullets state =
    let folder (bullets, currentState) bullet =
        let nextPosition = movePosition bullet.Direction bullet.Position

        if not (inBounds currentState.Map.Width currentState.Map.Height nextPosition) then
            bullets, currentState
        elif currentState.Walls.Contains nextPosition then
            bullets,
            { currentState with
                LastMessage =
                    if currentState.LastMessage = "" then
                        "A bullet hit a wall."
                    else
                        currentState.LastMessage }
        elif currentState.Cover.Contains nextPosition then
            bullets,
            { currentState with
                Cover = currentState.Cover.Remove nextPosition
                LastMessage =
                    if currentState.LastMessage = "" then
                        "A bullet destroyed cover."
                    else
                        currentState.LastMessage }
        elif currentState.Enemies |> List.contains nextPosition then
            bullets,
            { currentState with
                Enemies = currentState.Enemies |> List.filter ((<>) nextPosition)
                Score = currentState.Score + 10
                LastMessage =
                    if currentState.LastMessage = "" then
                        "Enemy down."
                    else
                        currentState.LastMessage }
        else
            { bullet with Position = nextPosition } :: bullets, currentState

    let bullets, nextState = List.fold folder ([], state) state.Bullets
    { nextState with Bullets = List.rev bullets }

let occupiedByEnemy enemies position =
    enemies |> List.contains position

let tryMoveEnemy state remainingEnemies enemy =
    let dx = state.Player.X - enemy.X
    let dy = state.Player.Y - enemy.Y

    let preferredDirection =
        if abs dx > abs dy then
            if dx < 0 then Left else Right
        else if dy < 0 then
            Up
        else
            Down

    let fallbackDirection =
        if preferredDirection = Left || preferredDirection = Right then
            if dy < 0 then Up else Down
        else if dx < 0 then
            Left
        else
            Right

    let tryDirections =
        if preferredDirection = fallbackDirection then
            [ preferredDirection ]
        else
            [ preferredDirection; fallbackDirection ]

    tryDirections
    |> List.tryPick (fun direction ->
        let target = movePosition direction enemy
        let blocked =
            not (inBounds state.Map.Width state.Map.Height target)
            || state.Walls.Contains target
            || state.Cover.Contains target
            || occupiedByEnemy remainingEnemies target

        if blocked then None else Some target)
    |> Option.defaultValue enemy

let advanceEnemies state =
    let folder (movedEnemies, hpLost, playerHit, bullets, score) enemy =
        let nextPosition = tryMoveEnemy state movedEnemies enemy

        if nextPosition = state.Player then
            movedEnemies, hpLost + 1, true, bullets, score
        elif bullets |> List.exists (fun bullet -> bullet.Position = nextPosition) then
            let remainingBullets = bullets |> List.filter (fun bullet -> bullet.Position <> nextPosition)
            movedEnemies, hpLost, playerHit, remainingBullets, score + 10
        else
            nextPosition :: movedEnemies, hpLost, playerHit, bullets, score

    let movedEnemies, hpLost, playerHit, remainingBullets, scoreGained =
        List.fold folder ([], 0, false, state.Bullets, 0) state.Enemies

    { state with
        Enemies = List.rev movedEnemies
        Bullets = remainingBullets
        Hp = max 0 (state.Hp - hpLost)
        Score = state.Score + scoreGained
        LastMessage =
            if playerHit then
                $"An enemy hit you. HP -{hpLost}."
            elif scoreGained > 0 then
                "An enemy ran into your fire."
            else
                state.LastMessage }

let applyCommand command state =
    match command with
    | Move direction -> tryMovePlayer direction state
    | Shoot direction -> fireBullet direction state
    | Reload ->
        if state.IsPaused || isFinished state then
            state
        else
            { state with Ammo = maxAmmo; LastMessage = "Reloaded." }
    | TogglePause ->
        if isFinished state then
            state
        else
            { state with
                IsPaused = not state.IsPaused
                LastMessage = if state.IsPaused then "Resumed." else "Paused." }
    | Restart -> makeInitialState state.Map
    | ReturnToMenu -> { state with ShouldReturnToMenu = true }
    | Quit -> { state with ShouldQuit = true }

let applyCommands commands state =
    commands |> List.fold (fun current command -> applyCommand command current) state

let advanceFrame state =
    if state.IsPaused || isFinished state then
        { state with Frame = state.Frame + 1 }
    else
        let advanced = { state with Frame = state.Frame + 1 }
        let withBullets =
            if advanced.Frame % advanced.BulletStepEvery = 0 then
                advanceBullets advanced
            else
                advanced

        let withEnemies =
            if withBullets.Frame % withBullets.EnemyStepEvery = 0 then
                advanceEnemies withBullets
            else
                withBullets

        withEnemies |> tryAdvanceWave

let commandsFromKey (key: ConsoleKeyInfo) =
    match key.Key with
    | ConsoleKey.W -> [ Move Up ]
    | ConsoleKey.S -> [ Move Down ]
    | ConsoleKey.A -> [ Move Left ]
    | ConsoleKey.D -> [ Move Right ]
    | ConsoleKey.UpArrow -> [ Shoot Up ]
    | ConsoleKey.DownArrow -> [ Shoot Down ]
    | ConsoleKey.LeftArrow -> [ Shoot Left ]
    | ConsoleKey.RightArrow -> [ Shoot Right ]
    | ConsoleKey.R -> [ Reload ]
    | ConsoleKey.P -> [ TogglePause ]
    | ConsoleKey.M -> [ ReturnToMenu ]
    | ConsoleKey.Escape -> [ Quit ]
    | _ -> []

let readPendingCommands state =
    let rec gather commands =
        if Console.KeyAvailable then
            let key = Console.ReadKey(true)

            let nextCommands =
                match key.Key with
                | ConsoleKey.Enter when isFinished state -> Restart :: commands
                | _ -> (commandsFromKey key) @ commands

            gather nextCommands
        else
            List.rev commands

    gather []

let buildFrameText state =
    let cells = Array2D.create state.Map.Height state.Map.Width '.'

    for wall in state.Walls do
        cells[wall.Y, wall.X] <- '#'

    for cover in state.Cover do
        cells[cover.Y, cover.X] <- 'C'

    for enemy in state.Enemies do
        cells[enemy.Y, enemy.X] <- 'M'

    for bullet in state.Bullets do
        cells[bullet.Position.Y, bullet.Position.X] <- '*'

    cells[state.Player.Y, state.Player.X] <- 'P'

    let builder = StringBuilder()
    builder.AppendLine($"Map: {state.Map.Name} ({state.Map.Width}x{state.Map.Height})") |> ignore

    for y in 0 .. state.Map.Height - 1 do
        let line =
            [ for x in 0 .. state.Map.Width - 1 -> cells[y, x] ]
            |> Array.ofList
            |> String

        builder.AppendLine(line) |> ignore

    builder.AppendLine() |> ignore
    builder.Append("HP: ").Append(state.Hp).Append(" | Ammo: ").Append(state.Ammo).Append(" | Score: ").Append(state.Score)
        .Append(" | Wave: ").Append(state.Wave).Append("/").Append(state.Map.MaxWave)
        .Append(" | Frame: ").Append(state.Frame).AppendLine()
        |> ignore

    if state.Hp <= 0 then
        builder.AppendLine("Game over. Enter restart | M menu | Esc quit") |> ignore
    elif hasWon state then
        builder.AppendLine("Arena cleared. Enter restart | M menu | Esc quit") |> ignore
    elif state.IsPaused then
        builder.AppendLine("Paused. P resume | M menu | Esc quit") |> ignore
    else
        builder.AppendLine("WASD move | arrows shoot | R reload | P pause | M menu | Esc quit") |> ignore

    if not (String.IsNullOrWhiteSpace state.LastMessage) then
        builder.AppendLine(state.LastMessage) |> ignore

    builder.ToString()

let renderScreen (text: string) =
    Console.Write("\u001b[H\u001b[2J")
    Console.Write(text)
    Console.Out.Flush()

let renderState state =
    renderScreen (buildFrameText state)

let renderMenu selectedIndex =
    let builder = StringBuilder()
    builder.AppendLine("ASCII Arena Shooter") |> ignore
    builder.AppendLine("===================") |> ignore
    builder.AppendLine() |> ignore
    builder.AppendLine("Select a map with W/S or Up/Down, then press Enter.") |> ignore
    builder.AppendLine("Press Esc to quit.") |> ignore
    builder.AppendLine() |> ignore

    availableMaps
    |> List.iteri (fun index map ->
        let prefix = if index = selectedIndex then "> " else "  "
        builder.Append(prefix).Append(map.Name).Append(" - ").AppendLine(map.Description) |> ignore)

    builder.AppendLine() |> ignore
    builder.AppendLine("Different maps change the arena size, lane structure, and wave pressure.") |> ignore
    builder.ToString()

let runMapMenu () =
    Console.Clear()
    Console.CursorVisible <- false

    let rec loop selectedIndex =
        renderScreen (renderMenu selectedIndex)

        let key = Console.ReadKey(true)

        match key.Key with
        | ConsoleKey.W
        | ConsoleKey.UpArrow ->
            let nextIndex = if selectedIndex = 0 then availableMaps.Length - 1 else selectedIndex - 1
            loop nextIndex
        | ConsoleKey.S
        | ConsoleKey.DownArrow ->
            let nextIndex = (selectedIndex + 1) % availableMaps.Length
            loop nextIndex
        | ConsoleKey.Enter -> Some availableMaps[selectedIndex]
        | ConsoleKey.Escape -> None
        | _ -> loop selectedIndex

    loop 0

let runRealtimeGame map =
    Console.CursorVisible <- false
    Console.Clear()

    let stopwatch = Stopwatch.StartNew()
    let mutable nextFrameAt = stopwatch.ElapsedMilliseconds
    let mutable state = makeInitialState map

    renderState state

    while not state.ShouldQuit && not state.ShouldReturnToMenu do
        let now = stopwatch.ElapsedMilliseconds

        if now >= nextFrameAt then
            let commands = readPendingCommands state
            state <- state |> applyCommands commands |> advanceFrame
            renderState state
            nextFrameAt <- nextFrameAt + frameDurationMs
        else
            Thread.Sleep(1)

    state

let assertEqual name expected actual =
    if expected <> actual then
        failwith $"Test failed: {name}. Expected {expected}, got {actual}."

let assertTrue name condition =
    if not condition then
        failwith $"Test failed: {name}."

let advanceFrames count state =
    [ 1 .. count ] |> List.fold (fun current _ -> advanceFrame current) state

let runSelfTests () =
    let baseState = makeInitialState defaultMap

    let blockedMoveState =
        { baseState with Player = { X = 1; Y = 1 }; LastMessage = "" }
        |> applyCommand (Move Left)

    assertEqual "player blocked by wall" { X = 1; Y = 1 } blockedMoveState.Player

    let noAmmoState =
        { baseState with Ammo = 0; LastMessage = "" }
        |> applyCommand (Shoot Right)

    assertEqual "shoot with no ammo keeps bullets empty" 0 noAmmoState.Bullets.Length
    assertEqual "shoot with no ammo keeps ammo at zero" 0 noAmmoState.Ammo

    let enemyHitState =
        { baseState with
            Player = { X = 2; Y = 2 }
            Wave = baseState.Map.MaxWave
            Cover = Set.empty
            Enemies = [ { X = 4; Y = 2 } ]
            Bullets =
                [ { Position = { X = 3; Y = 2 }
                    Direction = Right } ]
            LastMessage = "" }
        |> advanceFrames baseState.BulletStepEvery

    assertEqual "bullet removes enemy" 0 enemyHitState.Enemies.Length
    assertEqual "bullet adds score" 10 enemyHitState.Score

    let coverHitState =
        { baseState with
            Player = { X = 2; Y = 2 }
            Wave = baseState.Map.MaxWave
            Cover = Set.ofList [ { X = 4; Y = 2 } ]
            Bullets =
                [ { Position = { X = 3; Y = 2 }
                    Direction = Right } ]
            LastMessage = "" }
        |> advanceFrames baseState.BulletStepEvery

    assertTrue "bullet removes cover" (not (coverHitState.Cover.Contains { X = 4; Y = 2 }))

    let enemyContactState =
        { baseState with
            Player = { X = 3; Y = 3 }
            Wave = baseState.Map.MaxWave
            Enemies = [ { X = 3; Y = 2 } ]
            LastMessage = "" }
        |> advanceFrames baseState.EnemyStepEvery

    assertEqual "enemy collision reduces hp" 2 enemyContactState.Hp
    assertEqual "enemy collision removes enemy" 0 enemyContactState.Enemies.Length

    let waveAdvanceState =
        { baseState with Enemies = []; Wave = 1; LastMessage = "" }
        |> advanceFrame

    assertEqual "wave advances" 2 waveAdvanceState.Wave
    assertEqual "new wave spawns enemies" 3 waveAdvanceState.Enemies.Length

    let enemyIntoBulletState =
        { baseState with
            Player = { X = 8; Y = 4 }
            Wave = baseState.Map.MaxWave
            Cover = Set.empty
            BulletStepEvery = 10
            EnemyStepEvery = 5
            Enemies = [ { X = 8; Y = 2 } ]
            Bullets =
                [ { Position = { X = 8; Y = 3 }
                    Direction = Right } ]
            LastMessage = "" }
        |> advanceFrames 5

    assertEqual "enemy moving into bullet is removed" 0 enemyIntoBulletState.Enemies.Length
    assertEqual "enemy moving into bullet adds score" 10 enemyIntoBulletState.Score

    let pauseState =
        { baseState with IsPaused = true; Frame = 7; Bullets = [ { Position = { X = 4; Y = 4 }; Direction = Right } ] }
        |> advanceFrame

    assertEqual "paused frame still increments counter" 8 pauseState.Frame
    assertEqual "paused frame does not move bullets" { X = 4; Y = 4 } pauseState.Bullets.Head.Position

    let largeMapState = makeInitialState availableMaps[2]
    assertEqual "large map width preserved" 34 largeMapState.Map.Width
    assertEqual "large map height preserved" 16 largeMapState.Map.Height
    assertTrue "large map spawns enemies" (largeMapState.Enemies.Length > 0)

    let menuState = { baseState with ShouldReturnToMenu = false } |> applyCommand ReturnToMenu
    assertTrue "menu command toggles menu return" menuState.ShouldReturnToMenu

    printfn "All self-tests passed."

[<EntryPoint>]
let main argv =
    if argv |> Array.contains "--self-test" then
        runSelfTests ()
        0
    else
        try
            let mutable keepRunning = true

            while keepRunning do
                match runMapMenu () with
                | None ->
                    keepRunning <- false
                | Some map ->
                    let result = runRealtimeGame map

                    if result.ShouldQuit then
                        keepRunning <- false

            0
        finally
            Console.CursorVisible <- true
            Console.Clear()
