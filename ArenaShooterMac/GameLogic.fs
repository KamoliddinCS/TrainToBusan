namespace ArenaShooterMac

open System

module GameLogic =
    let width = 20
    let height = 12
    let maxAmmo = 8
    let maxHitPoints = 5
    let maxWave = 5

    let private move direction position =
        match direction with
        | Up -> { position with Y = position.Y - 1 }
        | Down -> { position with Y = position.Y + 1 }
        | Left -> { position with X = position.X - 1 }
        | Right -> { position with X = position.X + 1 }

    let private inBounds position =
        position.X >= 0
        && position.X < width
        && position.Y >= 0
        && position.Y < height

    let private buildWalls () =
        seq {
            for x in 0 .. width - 1 do
                yield { X = x; Y = 0 }
                yield { X = x; Y = height - 1 }

            for y in 0 .. height - 1 do
                yield { X = 0; Y = y }
                yield { X = width - 1; Y = y }

            yield { X = 6; Y = 3 }
            yield { X = 7; Y = 3 }
            yield { X = 12; Y = 3 }
            yield { X = 13; Y = 3 }
            yield { X = 9; Y = 6 }
            yield { X = 10; Y = 6 }
            yield { X = 5; Y = 8 }
            yield { X = 14; Y = 8 }
        }
        |> Set.ofSeq

    let private initialCover =
        [ ({ X = 4; Y = 4 }, 2)
          ({ X = 5; Y = 4 }, 2)
          ({ X = 14; Y = 4 }, 2)
          ({ X = 15; Y = 4 }, 2)
          ({ X = 8; Y = 8 }, 2)
          ({ X = 11; Y = 8 }, 2)
          ({ X = 9; Y = 9 }, 2)
          ({ X = 10; Y = 9 }, 2) ]
        |> Map.ofList

    let private enemyBlueprints =
        Map.ofList
            [ 1,
              [ ({ X = 16; Y = 2 }, Drone)
                ({ X = 3; Y = 2 }, Drone)
                ({ X = 10; Y = 2 }, Brute)
                ({ X = 16; Y = 6 }, Drone) ]
              2,
              [ ({ X = 2; Y = 2 }, Drone)
                ({ X = 17; Y = 2 }, Drone)
                ({ X = 4; Y = 9 }, Brute)
                ({ X = 15; Y = 9 }, Drone)
                ({ X = 10; Y = 2 }, Brute) ]
              3,
              [ ({ X = 2; Y = 5 }, Drone)
                ({ X = 17; Y = 5 }, Drone)
                ({ X = 10; Y = 2 }, Brute)
                ({ X = 10; Y = 9 }, Brute)
                ({ X = 3; Y = 9 }, Drone) ]
              4,
              [ ({ X = 2; Y = 2 }, Drone)
                ({ X = 17; Y = 2 }, Drone)
                ({ X = 2; Y = 9 }, Drone)
                ({ X = 17; Y = 9 }, Drone)
                ({ X = 10; Y = 2 }, Brute)
                ({ X = 10; Y = 9 }, Brute) ]
              5,
              [ ({ X = 3; Y = 2 }, Drone)
                ({ X = 16; Y = 2 }, Drone)
                ({ X = 2; Y = 6 }, Drone)
                ({ X = 17; Y = 6 }, Drone)
                ({ X = 7; Y = 2 }, Brute)
                ({ X = 12; Y = 2 }, Brute)
                ({ X = 10; Y = 9 }, Brute) ] ]

    let private pickupBlueprints =
        Map.ofList
            [ 2, { Position = { X = 10; Y = 5 }; Kind = AmmoPack }
              3, { Position = { X = 2; Y = 10 }; Kind = MedKit }
              4, { Position = { X = 17; Y = 10 }; Kind = AmmoPack }
              5, { Position = { X = 10; Y = 10 }; Kind = MedKit } ]

    let private allOpenCells =
        seq {
            for y in 1 .. height - 2 do
                for x in 1 .. width - 2 do
                    yield { X = x; Y = y }
        }
        |> List.ofSeq

    let private enemyHealth kind =
        match kind with
        | Drone -> 1
        | Brute -> 2

    let private enemyScore kind =
        match kind with
        | Drone -> 10
        | Brute -> 20

    let private containsPosition (position: Position) (positions: Position list) =
        positions |> List.exists ((=) position)

    let private blockedByTerrain (state: GameState) (position: Position) =
        state.Walls.Contains position || state.Cover.ContainsKey position

    let private openForSpawn
        (player: Position)
        (walls: Set<Position>)
        (cover: Map<Position, int>)
        (pickups: Pickup list)
        (position: Position)
        =
        position <> player
        && not (walls.Contains position)
        && not (cover.ContainsKey position)
        && not (pickups |> List.exists (fun pickup -> pickup.Position = position))

    let private spawnWave
        (wave: int)
        (player: Position)
        (walls: Set<Position>)
        (cover: Map<Position, int>)
        (pickups: Pickup list)
        (nextEnemyId: int)
        =
        let requested =
            enemyBlueprints
            |> Map.tryFind wave
            |> Option.defaultValue []

        let fallback =
            allOpenCells
            |> List.mapi (fun index position ->
                let kind = if index % 4 = 0 then Brute else Drone
                position, kind)

        let enemyCount = min (wave + 2) 7

        let enemies =
            (requested @ fallback)
            |> List.filter (fun (position, _) -> openForSpawn player walls cover pickups position)
            |> List.distinctBy fst
            |> List.truncate enemyCount
            |> List.mapi (fun index (position, kind) ->
                { Id = nextEnemyId + index
                  Position = position
                  Kind = kind
                  HitPoints = enemyHealth kind })

        enemies, nextEnemyId + enemies.Length

    let private applyPickup (player: Player) (pickups: Pickup list) =
        match pickups |> List.tryFind (fun pickup -> pickup.Position = player.Position) with
        | Some pickup ->
            let updatedPlayer =
                match pickup.Kind with
                | AmmoPack -> { player with Ammo = min maxAmmo (player.Ammo + 3) }
                | MedKit -> { player with HitPoints = min maxHitPoints (player.HitPoints + 2) }

            let message =
                match pickup.Kind with
                | AmmoPack -> "Ammo cache secured. +3 rounds."
                | MedKit -> "Field kit recovered. +2 HP."

            updatedPlayer, pickups |> List.filter (fun entry -> entry.Position <> pickup.Position), Some message
        | None -> player, pickups, None

    let private damageCover (position: Position) (cover: Map<Position, int>) =
        match cover |> Map.tryFind position with
        | Some durability when durability > 1 -> cover |> Map.add position (durability - 1)
        | Some _ -> cover |> Map.remove position
        | None -> cover

    let private applyBulletHit (enemy: Enemy) (score: int) =
        let updatedEnemy = { enemy with HitPoints = enemy.HitPoints - 1 }

        if updatedEnemy.HitPoints <= 0 then
            None, score + enemyScore enemy.Kind, Some "Target neutralized."
        else
            Some updatedEnemy, score, Some "Armor cracked."

    let private updateBullets (state: GameState) =
        let folder
            (keptBullets: Bullet list, enemies: Enemy list, cover: Map<Position, int>, score: int, message: string)
            (bullet: Bullet)
            =
            let nextPosition = move bullet.Direction bullet.Position

            if not (inBounds nextPosition) then
                keptBullets, enemies, cover, score, message
            elif state.Walls.Contains nextPosition then
                keptBullets, enemies, cover, score, (if message = "" then "A shot sparks against the arena wall." else message)
            elif cover.ContainsKey nextPosition then
                keptBullets,
                enemies,
                damageCover nextPosition cover,
                score,
                (if message = "" then "Cover splinters under fire." else message)
            else
                match enemies |> List.tryFind (fun (enemy: Enemy) -> enemy.Position = nextPosition) with
                | Some enemy ->
                    let remainingEnemies = enemies |> List.filter (fun (entry: Enemy) -> entry.Id <> enemy.Id)
                    let maybeEnemy, nextScore, hitMessage = applyBulletHit enemy score
                    let updatedEnemies =
                        match maybeEnemy with
                        | Some survivingEnemy -> survivingEnemy :: remainingEnemies
                        | None -> remainingEnemies

                    keptBullets,
                    updatedEnemies,
                    cover,
                    nextScore,
                    (if message = "" then hitMessage |> Option.defaultValue "" else message)
                | None ->
                    let nextRange = bullet.RangeRemaining - 1

                    if nextRange <= 0 then
                        keptBullets, enemies, cover, score, message
                    else
                        { bullet with Position = nextPosition; RangeRemaining = nextRange } :: keptBullets,
                        enemies,
                        cover,
                        score,
                        message

        let bullets, enemies, cover, score, message =
            state.Bullets
            |> List.fold folder ([], state.Enemies, state.Cover, state.Score, "")

        { state with
            Bullets = List.rev bullets
            Enemies = enemies
            Cover = cover
            Score = score
            Message = if message <> "" then message else state.Message }

    let private preferredDirections (fromPosition: Position) (toPosition: Position) =
        let dx = toPosition.X - fromPosition.X
        let dy = toPosition.Y - fromPosition.Y

        let horizontal =
            if dx < 0 then Left else Right

        let vertical =
            if dy < 0 then Up else Down

        if abs dx > abs dy then
            [ horizontal; vertical ]
        elif abs dy > 0 then
            [ vertical; horizontal ]
        else
            [ horizontal; vertical ]

    let private enemyCanMove (tick: int) (enemy: Enemy) =
        match enemy.Kind with
        | Drone -> true
        | Brute -> tick % 2 = 0

    let private updateEnemies (state: GameState) =
        let rec moveEnemies
            (pending: Enemy list)
            (moved: Enemy list)
            (bullets: Bullet list)
            (playerHp: int)
            (score: int)
            (message: string)
            =
            match pending with
            | [] ->
                List.rev moved, bullets, playerHp, score, message
            | enemy :: rest ->
                if not (enemyCanMove state.Tick enemy) then
                    moveEnemies rest (enemy :: moved) bullets playerHp score message
                else
                    let occupied =
                        (moved |> List.map (fun (entry: Enemy) -> entry.Position))
                        @ (rest |> List.map (fun (entry: Enemy) -> entry.Position))

                    let destination =
                        preferredDirections enemy.Position state.Player.Position
                        |> List.tryPick (fun direction ->
                            let target = move direction enemy.Position

                            if
                                inBounds target
                                && not (state.Walls.Contains target)
                                && not (state.Cover.ContainsKey target)
                                && not (containsPosition target occupied)
                            then
                                Some target
                            else
                                None)
                        |> Option.defaultValue enemy.Position

                    if destination = state.Player.Position then
                        moveEnemies
                            rest
                            moved
                            bullets
                            (max 0 (playerHp - 1))
                            score
                            "A raider crashes into your shield line."
                    else
                        match bullets |> List.tryFind (fun (bullet: Bullet) -> bullet.Position = destination) with
                        | Some bullet ->
                            let remainingBullets = bullets |> List.filter (fun (entry: Bullet) -> entry <> bullet)
                            let updatedEnemy = { enemy with HitPoints = enemy.HitPoints - 1 }

                            if updatedEnemy.HitPoints <= 0 then
                                moveEnemies
                                    rest
                                    moved
                                    remainingBullets
                                    playerHp
                                    (score + enemyScore enemy.Kind)
                                    "An enemy walks into your shot."
                            else
                                moveEnemies
                                    rest
                                    ({ updatedEnemy with Position = destination } :: moved)
                                    remainingBullets
                                    playerHp
                                    score
                                    "A brute shrugs off the hit."
                        | None ->
                            moveEnemies
                                rest
                                ({ enemy with Position = destination } :: moved)
                                bullets
                                playerHp
                                score
                                message

        let movedEnemies, bullets, playerHp, score, message =
            moveEnemies state.Enemies [] state.Bullets state.Player.HitPoints state.Score state.Message

        { state with
            Enemies = movedEnemies
            Bullets = bullets
            Player = { state.Player with HitPoints = playerHp }
            Score = score
            Message = message }

    let private finalizeState (state: GameState) =
        if state.Player.HitPoints <= 0 then
            { state with
                IsGameOver = true
                IsPaused = true
                Message = "Your mech goes dark. Press Restart to launch again." }
        elif state.Wave = state.MaxWave && List.isEmpty state.Enemies then
            { state with
                IsVictory = true
                IsPaused = true
                Message = "Arena secured. You cleared every wave." }
        else
            state

    let private advanceWave (state: GameState) =
        if not (List.isEmpty state.Enemies) || state.Wave >= state.MaxWave then
            state
        else
            let nextWave = state.Wave + 1
            let nextPickups =
                pickupBlueprints
                |> Map.tryFind nextWave
                |> Option.toList

            let enemies, nextEnemyId =
                spawnWave nextWave state.Player.Position state.Walls state.Cover nextPickups state.NextEnemyId

            { state with
                Wave = nextWave
                Enemies = enemies
                Pickups = nextPickups
                NextEnemyId = nextEnemyId
                Message = $"Wave {nextWave} entering the arena." }

    let private directHit (state: GameState) (spawnPosition: Position) (direction: Direction) =
        if state.Walls.Contains spawnPosition then
            { state with Message = "The shot splashes harmlessly across a wall." }
        elif state.Cover.ContainsKey spawnPosition then
            { state with
                Cover = damageCover spawnPosition state.Cover
                Message = "You chip away at nearby cover." }
        else
            match state.Enemies |> List.tryFind (fun (enemy: Enemy) -> enemy.Position = spawnPosition) with
            | Some enemy ->
                let remainingEnemies = state.Enemies |> List.filter (fun (entry: Enemy) -> entry.Id <> enemy.Id)
                let updatedEnemy = { enemy with HitPoints = enemy.HitPoints - 1 }

                if updatedEnemy.HitPoints <= 0 then
                    { state with
                        Enemies = remainingEnemies
                        Score = state.Score + enemyScore enemy.Kind
                        Message = "Point-blank elimination." }
                else
                    { state with
                        Enemies = { updatedEnemy with Position = spawnPosition } :: remainingEnemies
                        Message = "Close-range hit confirmed." }
            | None ->
                { state with
                    Bullets =
                        { Position = spawnPosition
                          Direction = direction
                          RangeRemaining = 10 }
                        :: state.Bullets
                    Message = "Round away." }

    let createInitialState () =
        let walls = buildWalls ()
        let player =
            { Position = { X = 10; Y = 9 }
              HitPoints = maxHitPoints
              Ammo = maxAmmo
              Facing = Up }

        let pickups: Pickup list = [ { Position = { X = 10; Y = 10 }; Kind = AmmoPack } ]
        let enemies, nextEnemyId = spawnWave 1 player.Position walls initialCover pickups 1

        { Width = width
          Height = height
          Player = player
          Enemies = enemies
          Bullets = []
          Walls = walls
          Cover = initialCover
          Pickups = pickups
          Score = 0
          Wave = 1
          MaxWave = maxWave
          Tick = 0
          Message = "Move with WASD. Shoot with arrows. R reloads. P pauses."
          NextEnemyId = nextEnemyId
          IsPaused = false
          IsVictory = false
          IsGameOver = false }

    let tryMovePlayer (direction: Direction) (state: GameState) =
        if state.IsGameOver || state.IsVictory || state.IsPaused then
            state
        else
            let target = move direction state.Player.Position

            if
                not (inBounds target)
                || blockedByTerrain state target
                    || (state.Enemies |> List.exists (fun (enemy: Enemy) -> enemy.Position = target))
            then
                { state with Message = "That path is sealed." }
            else
                let updatedPlayer = { state.Player with Position = target; Facing = direction }
                let collectedPlayer, pickups, pickupMessage = applyPickup updatedPlayer state.Pickups

                { state with
                    Player = collectedPlayer
                    Pickups = pickups
                    Message = pickupMessage |> Option.defaultValue "Thrusters engaged." }

    let tryShoot (direction: Direction) (state: GameState) =
        if state.IsGameOver || state.IsVictory || state.IsPaused then
            state
        elif state.Player.Ammo <= 0 then
            { state with Message = "No ammo. Tap R to reload." }
        else
            let spawnPosition = move direction state.Player.Position
            let preparedState =
                { state with
                    Player = { state.Player with Ammo = state.Player.Ammo - 1; Facing = direction } }

            if not (inBounds spawnPosition) then
                { preparedState with Message = "No line of fire in that direction." }
            else
                directHit preparedState spawnPosition direction

    let reload (state: GameState) =
        if state.IsGameOver || state.IsVictory then
            state
        elif state.IsPaused then
            state
        else
            { state with
                Player = { state.Player with Ammo = maxAmmo }
                Message = "Magazine refilled." }

    let togglePause (state: GameState) =
        if state.IsGameOver || state.IsVictory then
            state
        else
            { state with
                IsPaused = not state.IsPaused
                Message = if state.IsPaused then "Systems live." else "Arena paused." }

    let restart () =
        createInitialState ()

    let tick (state: GameState) =
        if state.IsPaused || state.IsGameOver || state.IsVictory then
            state
        else
            { state with Tick = state.Tick + 1 }
            |> updateBullets
            |> updateEnemies
            |> advanceWave
            |> finalizeState
