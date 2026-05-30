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

type BulletState =
    | Ready
    | InFlight of Projectile

and Projectile =
    { Position: Position
      Direction: Direction
      Returning: bool }

type Zombie =
    { Id: int
      Position: Position }

type TrainCarTemplate =
    { Index: int
      Width: int
      Height: int
      Obstacles: Set<Position>
      ZombieStarts: Position list
      DoorPosition: Position
      EntryPosition: Position
      IntroMessage: string }

type TrainCar =
    { Index: int
      Width: int
      Height: int
      Obstacles: Set<Position>
      Zombies: Zombie list
      DoorPosition: Position
      EntryPosition: Position }

type Command =
    | Move of Direction
    | Shoot of Direction
    | Reload
    | TogglePause
    | Restart
    | ReturnToMenu
    | Quit

type GameState =
    { Cars: TrainCarTemplate list
      CurrentCar: TrainCar
      PlayerPosition: Position
      Facing: Direction
      HitPoints: int
      Score: int
      BulletState: BulletState
      Frame: int
      Message: string
      NextZombieId: int
      BulletMoveEvery: int
      ZombieMoveEvery: int
      ShouldQuit: bool
      IsWin: bool
      IsGameOver: bool }

let frameDurationMs = 140L
let startingHitPoints = 3

let movePosition direction position =
    match direction with
    | Up -> { position with Y = position.Y - 1 }
    | Down -> { position with Y = position.Y + 1 }
    | Left -> { position with X = position.X - 1 }
    | Right -> { position with X = position.X + 1 }

let oppositeDirection direction =
    match direction with
    | Up -> Down
    | Down -> Up
    | Left -> Right
    | Right -> Left

let inBounds width height position =
    position.X >= 0
    && position.X < width
    && position.Y >= 0
    && position.Y < height

let makeCarTemplate index introMessage zombieStarts obstaclePositions =
    { Index = index
      Width = 18
      Height = 4
      Obstacles = obstaclePositions |> Set.ofList
      ZombieStarts = zombieStarts
      DoorPosition = { X = 17; Y = 0 }
      EntryPosition = { X = 1; Y = 2 }
      IntroMessage = introMessage }

let carTemplates =
    [ makeCarTemplate
          1
          "You shove through startled passengers into the first car ahead."
          [ { X = 13; Y = 0 } ]
          [ { X = 8; Y = 1 }; { X = 9; Y = 1 } ]
      makeCarTemplate
          2
          "A body blocks half the aisle. Something moves behind it."
          [ { X = 12; Y = 0 }; { X = 14; Y = 3 } ]
          [ { X = 5; Y = 1 }; { X = 6; Y = 1 }; { X = 10; Y = 2 }; { X = 11; Y = 2 } ]
      makeCarTemplate
          3
          "The carriage lights flicker. More of them are standing now."
          [ { X = 10; Y = 0 }; { X = 14; Y = 1 }; { X = 15; Y = 3 } ]
          [ { X = 4; Y = 1 }; { X = 5; Y = 1 }; { X = 8; Y = 2 }; { X = 9; Y = 2 }; { X = 12; Y = 1 } ]
      makeCarTemplate
          4
          "The floor is slick. They are faster in this car."
          [ { X = 9; Y = 0 }; { X = 13; Y = 0 }; { X = 12; Y = 2 }; { X = 15; Y = 3 } ]
          [ { X = 3; Y = 1 }; { X = 4; Y = 1 }; { X = 7; Y = 1 }; { X = 8; Y = 1 }; { X = 11; Y = 2 }; { X = 12; Y = 2 } ]
      makeCarTemplate
          5
          "The final car is packed with the infected. The engine is just beyond."
          [ { X = 8; Y = 0 }; { X = 11; Y = 0 }; { X = 10; Y = 1 }; { X = 14; Y = 2 }; { X = 16; Y = 3 } ]
          [ { X = 5; Y = 1 }; { X = 6; Y = 1 }; { X = 7; Y = 1 }; { X = 10; Y = 2 }; { X = 11; Y = 2 }; { X = 4; Y = 3 }; { X = 5; Y = 3 } ] ]

let buildTrainCar template nextZombieId =
    let zombies =
        template.ZombieStarts
        |> List.mapi (fun index position ->
            { Id = nextZombieId + index
              Position = position })

    { Index = template.Index
      Width = template.Width
      Height = template.Height
      Obstacles = template.Obstacles
      Zombies = zombies
      DoorPosition = template.DoorPosition
      EntryPosition = template.EntryPosition },
    nextZombieId + zombies.Length

let bulletStatusText bulletState =
    match bulletState with
    | Ready -> "READY"
    | InFlight _ -> "IN FLIGHT"

let doorIsOpen state =
    List.isEmpty state.CurrentCar.Zombies

let makeInitialState () =
    let firstCar, nextZombieId = buildTrainCar carTemplates.Head 1

    { Cars = carTemplates
      CurrentCar = firstCar
      PlayerPosition = firstCar.EntryPosition
      Facing = Right
      HitPoints = startingHitPoints
      Score = 0
      BulletState = Ready
      Frame = 0
      Message = "You hear chaos behind you. You hear nothing ahead."
      NextZombieId = nextZombieId
      BulletMoveEvery = 1
      ZombieMoveEvery = 4
      ShouldQuit = false
      IsWin = false
      IsGameOver = false }

let currentCarWidth state = state.CurrentCar.Width
let currentCarHeight state = state.CurrentCar.Height

let isBlockedByObstacle state position =
    state.CurrentCar.Obstacles.Contains position

let isZombieAt state position =
    state.CurrentCar.Zombies |> List.exists (fun zombie -> zombie.Position = position)

let withDoorUnlockMessage previousZombieCount state fallbackMessage =
    if previousZombieCount > 0 && List.isEmpty state.CurrentCar.Zombies then
        { state with Message = "The door unlocks." }
    elif String.IsNullOrWhiteSpace fallbackMessage then
        state
    else
        { state with Message = fallbackMessage }

let chooseReturnStep (projectile: Projectile) (player: Position) =
    let dx = player.X - projectile.Position.X
    let dy = player.Y - projectile.Position.Y

    if abs dx >= abs dy && dx <> 0 then
        if dx < 0 then Left else Right
    elif dy <> 0 then
        if dy < 0 then Up else Down
    else
        projectile.Direction

let recoverProjectileIfCaught state =
    match state.BulletState with
    | InFlight projectile when projectile.Returning && projectile.Position = state.PlayerPosition ->
        { state with
            BulletState = Ready
            Message = "The projectile returns to your hand." }
    | _ -> state

let advanceToNextCar state =
    if state.CurrentCar.Index >= state.Cars.Length then
        { state with
            IsWin = true
            Message = "You reach the engine." }
    else
        let nextTemplate = state.Cars[state.CurrentCar.Index]
        let nextCar, nextZombieId = buildTrainCar nextTemplate state.NextZombieId

        { state with
            CurrentCar = nextCar
            PlayerPosition = nextCar.EntryPosition
            Facing = Right
            BulletState = Ready
            Message = nextTemplate.IntroMessage
            NextZombieId = nextZombieId }

let movePlayer direction state =
    if state.IsWin || state.IsGameOver then
        state
    else
        let facingState = { state with Facing = direction }
        let target = movePosition direction state.PlayerPosition

        if target = state.CurrentCar.DoorPosition then
            if doorIsOpen facingState then
                advanceToNextCar facingState
            else
                { facingState with Message = "The door is still locked." }
        elif not (inBounds (currentCarWidth facingState) (currentCarHeight facingState) target) then
            facingState
        elif isBlockedByObstacle facingState target then
            facingState
        elif isZombieAt facingState target then
            facingState
        else
            { facingState with
                PlayerPosition = target }
            |> recoverProjectileIfCaught

let shootProjectile state =
    if state.IsWin || state.IsGameOver then
        state
    else
        match state.BulletState with
        | InFlight _ -> { state with Message = "Your projectile is still in flight." }
        | Ready ->
            let spawnPosition = movePosition state.Facing state.PlayerPosition

            if not (inBounds (currentCarWidth state) (currentCarHeight state) spawnPosition) then
                { state with
                    BulletState =
                        InFlight
                            { Position = state.PlayerPosition
                              Direction = state.Facing
                              Returning = true }
                    Message = "The throw glances off the carriage frame and snaps back." }
            elif isBlockedByObstacle state spawnPosition then
                { state with
                    BulletState =
                        InFlight
                            { Position = state.PlayerPosition
                              Direction = state.Facing
                              Returning = true }
                    Message = "The projectile ricochets off the seats." }
            else
                let previousZombieCount = state.CurrentCar.Zombies.Length

                match state.CurrentCar.Zombies |> List.tryFind (fun zombie -> zombie.Position = spawnPosition) with
                | Some zombie ->
                    let remainingZombies = state.CurrentCar.Zombies |> List.filter (fun entry -> entry.Id <> zombie.Id)

                    { state with
                        CurrentCar = { state.CurrentCar with Zombies = remainingZombies }
                        Score = state.Score + 1
                        BulletState =
                            InFlight
                                { Position = spawnPosition
                                  Direction = state.Facing
                                  Returning = true } }
                    |> withDoorUnlockMessage previousZombieCount <| "A zombie drops and the projectile arcs back."
                | None ->
                    { state with
                        BulletState =
                            InFlight
                                { Position = spawnPosition
                                  Direction = state.Facing
                                  Returning = false }
                        Message = "You launch the projectile down the aisle." }

let recallOrRecover state =
    if state.IsWin || state.IsGameOver then
        state
    else
        match state.BulletState with
        | Ready -> { state with Message = "You steady your breathing." }
        | InFlight projectile when projectile.Returning ->
            { state with Message = "The projectile is already on its way back." }
        | InFlight projectile ->
            { state with
                BulletState = InFlight { projectile with Returning = true }
                Message = "You yank the projectile back toward you." }

let quitGame state =
    { state with ShouldQuit = true }

let applyCommand command state =
    match command with
    | Move direction -> movePlayer direction state
    | Shoot _ -> shootProjectile state
    | Reload -> recallOrRecover state
    | TogglePause -> state
    | Restart -> makeInitialState ()
    | ReturnToMenu -> state
    | Quit -> quitGame state

let moveBullet state =
    match state.BulletState with
    | Ready -> state
    | InFlight projectile when projectile.Returning ->
        let direction = chooseReturnStep projectile state.PlayerPosition
        let nextPosition = movePosition direction projectile.Position

        if nextPosition = state.PlayerPosition then
            { state with
                BulletState = Ready
                Message = "The projectile returns to your hand." }
        else
            { state with
                BulletState =
                    InFlight
                        { Position = nextPosition
                          Direction = direction
                          Returning = true } }
    | InFlight projectile ->
        let nextPosition = movePosition projectile.Direction projectile.Position

        if not (inBounds (currentCarWidth state) (currentCarHeight state) nextPosition) then
            { state with
                BulletState = InFlight { projectile with Returning = true }
                Message = "The projectile ricochets off the carriage wall." }
        elif state.CurrentCar.Obstacles.Contains nextPosition then
            { state with
                BulletState = InFlight { projectile with Returning = true }
                Message = "The projectile slams into the seats and comes back." }
        else
            let previousZombieCount = state.CurrentCar.Zombies.Length

            match state.CurrentCar.Zombies |> List.tryFind (fun zombie -> zombie.Position = nextPosition) with
            | Some zombie ->
                let remainingZombies = state.CurrentCar.Zombies |> List.filter (fun entry -> entry.Id <> zombie.Id)

                { state with
                    CurrentCar = { state.CurrentCar with Zombies = remainingZombies }
                    Score = state.Score + 1
                    BulletState =
                        InFlight
                            { Position = nextPosition
                              Direction = oppositeDirection projectile.Direction
                              Returning = true } }
                |> withDoorUnlockMessage previousZombieCount <| "The projectile drops an infected."
            | None ->
                { state with
                    BulletState = InFlight { projectile with Position = nextPosition } }

let directionPriority fromPosition targetPosition =
    let dx = targetPosition.X - fromPosition.X
    let dy = targetPosition.Y - fromPosition.Y

    let horizontal = if dx < 0 then Left else Right
    let vertical = if dy < 0 then Up else Down

    if abs dx > abs dy then
        [ horizontal; vertical ]
    elif abs dy > 0 then
        [ vertical; horizontal ]
    else
        [ horizontal; vertical ]

let moveZombies state =
    let rec stepZombies pending placed hp remainingScore message =
        match pending with
        | [] -> List.rev placed, hp, remainingScore, message
        | zombie :: rest ->
            let occupied = placed |> List.map (fun entry -> entry.Position)

            let nextPosition =
                directionPriority zombie.Position state.PlayerPosition
                |> List.tryPick (fun direction ->
                    let target = movePosition direction zombie.Position
                    let blocked =
                        not (inBounds (currentCarWidth state) (currentCarHeight state) target)
                        || state.CurrentCar.Obstacles.Contains target
                        || target = state.CurrentCar.DoorPosition
                        || occupied |> List.contains target
                        || (rest |> List.exists (fun other -> other.Position = target))

                    if blocked then None else Some target)
                |> Option.defaultValue zombie.Position

            if nextPosition = state.PlayerPosition then
                stepZombies
                    rest
                    placed
                    (max 0 (hp - 1))
                    remainingScore
                    "They lunge through the aisle and tear into you."
            else
                stepZombies
                    rest
                    ({ zombie with Position = nextPosition } :: placed)
                    hp
                    remainingScore
                    message

    let zombies, hp, score, message =
        stepZombies
            state.CurrentCar.Zombies
            []
            state.HitPoints
            state.Score
            state.Message

    { state with
        CurrentCar = { state.CurrentCar with Zombies = zombies }
        HitPoints = hp
        Score = score
        Message = message }

let finalizeState state =
    if state.HitPoints <= 0 then
        { state with
            IsGameOver = true
            Message = "You hesitated." }
    elif state.IsWin then
        state
    else
        state

let advanceFrame state =
    if state.IsWin || state.IsGameOver || state.ShouldQuit then
        state
    else
        let advanced = { state with Frame = state.Frame + 1 }
        let afterBullet =
            if advanced.Frame % advanced.BulletMoveEvery = 0 then
                moveBullet advanced
            else
                advanced

        let afterZombies =
            if afterBullet.Frame % afterBullet.ZombieMoveEvery = 0 then
                moveZombies afterBullet
            else
                afterBullet

        afterZombies |> finalizeState

let borderLine width =
    "+" + String('-', width) + "+"

let wrapText maxWidth (text: string) =
    let effectiveWidth = max 8 maxWidth
    let words =
        text.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.toList

    let rec build (currentLine: string list) (currentLength: int) (remaining: string list) (acc: string list) =
        match remaining with
        | [] ->
            match currentLine with
            | [] -> List.rev acc
            | _ -> currentLine |> List.rev |> String.concat " " |> fun line -> List.rev (line :: acc)
        | word :: rest ->
            let proposedLength =
                if currentLine.IsEmpty then
                    word.Length
                else
                    currentLength + 1 + word.Length

            if proposedLength <= effectiveWidth then
                build (word :: currentLine) proposedLength rest acc
            else
                let completed =
                    currentLine |> List.rev |> String.concat " "

                if currentLine.IsEmpty then
                    let head = if word.Length <= effectiveWidth then word else word.Substring(0, effectiveWidth)
                    let tail =
                        if word.Length <= effectiveWidth then
                            rest
                        else
                            word.Substring(effectiveWidth) :: rest

                    build [] 0 tail (head :: acc)
                else
                    build [ word ] word.Length rest (completed :: acc)

    build [] 0 words []

let trimWithEllipsis width (text: string) =
    if text.Length <= width then
        text
    elif width <= 3 then
        text.Substring(0, width)
    else
        text.Substring(0, width - 3) + "..."

let appendWrappedBlock (builder: StringBuilder) (label: string) (width: int) (lineCount: int) (text: string) =
    let wrappedLines =
        wrapText width text
        |> List.truncate lineCount

    let paddedLines =
        if wrappedLines.Length < lineCount then
            wrappedLines @ List.replicate (lineCount - wrappedLines.Length) ""
        else
            wrappedLines

    let normalizedLines =
        match List.tryLast paddedLines, (wrapText width text).Length > lineCount with
        | Some lastLine, true ->
            let init = paddedLines |> List.take (lineCount - 1)
            init @ [ trimWithEllipsis width lastLine ]
        | _ -> paddedLines

    match normalizedLines with
    | [] -> builder.Append(label).AppendLine() |> ignore
    | first :: rest ->
        builder.Append(label).AppendLine(first) |> ignore

        for line in rest do
            builder.Append(String(' ', label.Length)).AppendLine(line) |> ignore

let buildGrid state =
    let cells = Array2D.create state.CurrentCar.Height state.CurrentCar.Width ' '

    for obstacle in state.CurrentCar.Obstacles do
        cells[obstacle.Y, obstacle.X] <- '#'

    cells[state.CurrentCar.DoorPosition.Y, state.CurrentCar.DoorPosition.X] <- 'D'

    for zombie in state.CurrentCar.Zombies do
        cells[zombie.Position.Y, zombie.Position.X] <- 'Z'

    match state.BulletState with
    | InFlight projectile when projectile.Position <> state.PlayerPosition ->
        if inBounds state.CurrentCar.Width state.CurrentCar.Height projectile.Position then
            cells[projectile.Position.Y, projectile.Position.X] <- '*'
    | _ -> ()

    cells[state.PlayerPosition.Y, state.PlayerPosition.X] <- 'P'
    cells

let buildFrameText state =
    let cells = buildGrid state
    let builder = StringBuilder()
    let border = borderLine state.CurrentCar.Width

    builder.AppendLine(border) |> ignore

    for y in 0 .. state.CurrentCar.Height - 1 do
        let line =
            [ for x in 0 .. state.CurrentCar.Width - 1 -> cells[y, x] ]
            |> Array.ofList
            |> String

        builder.Append('|').Append(line).Append('|').AppendLine() |> ignore

    builder.AppendLine(border) |> ignore
    builder.AppendLine() |> ignore
    builder.Append("HP: ").Append(state.HitPoints)
        .Append(" | Bullet: ").Append(bulletStatusText state.BulletState)
        .Append(" | Score: ").Append(state.Score)
        .Append(" | Car: ").Append(state.CurrentCar.Index).Append('/').Append(state.Cars.Length)
        .AppendLine()
        |> ignore

    if state.IsWin then
        builder.AppendLine("You reach the engine.") |> ignore
        builder.AppendLine() |> ignore
        builder.AppendLine("The train keeps moving.") |> ignore
        builder.AppendLine() |> ignore
        builder.AppendLine("For now.") |> ignore
        builder.AppendLine("Press ENTER to play again or Q to quit.") |> ignore
    elif state.IsGameOver then
        builder.AppendLine("You hesitated.") |> ignore
        builder.AppendLine() |> ignore
        builder.AppendLine("They didn’t.") |> ignore
        builder.AppendLine("Press ENTER to try again or Q to quit.") |> ignore
    else
        appendWrappedBlock builder "Message:  " 54 2 state.Message
        appendWrappedBlock builder "Controls: " 54 1 "WASD or arrows move | Space shoot | R recover | Q quit"

        if doorIsOpen state then
            builder.AppendLine("Door: OPEN") |> ignore
        else
            builder.AppendLine("Door: LOCKED") |> ignore

    builder.ToString()

let normalizeLines (text: string) =
    text.Replace("\r", "").TrimEnd('\n').Split('\n')

let resetRenderer =
    let mutable previousLines: string array = [||]

    fun () ->
        previousLines <- [||]

let renderText =
    let mutable previousLines: string array = [||]

    fun (text: string) ->
        let lines = normalizeLines text
        let maxLineCount = max previousLines.Length lines.Length

        for index in 0 .. maxLineCount - 1 do
            let currentLine = if index < lines.Length then lines[index] else ""
            let previousLine = if index < previousLines.Length then previousLines[index] else ""

            if currentLine <> previousLine then
                let paddedLine =
                    currentLine.PadRight(max currentLine.Length previousLine.Length)

                Console.SetCursorPosition(0, index)
                Console.Write(paddedLine)

        Console.Out.Flush()
        previousLines <- lines

let readIntroConfirmation () =
    let rec waitForEnter () =
        match Console.ReadKey(true).Key with
        | ConsoleKey.Enter -> ()
        | _ -> waitForEnter ()

    waitForEnter ()

let showIntro () =
    Console.Clear()
    Console.CursorVisible <- false
    resetRenderer ()

    let paragraphs =
        [ "Train to Busan"
          ""
          "Boarding complete."
          "Doors closing."
          "Next stop: Busan."
          ""
          "You notice someone stumble aboard at the last second."
          "The doors shut behind them."
          "They collapse near the rear carriage."
          ""
          "Passengers gather."
          "Someone calls for help."
          ""
          "Then the screaming starts."
          ""
          "The train begins to move."
          "Whatever happened back there..."
          "is spreading."
          ""
          "You are near the middle of the train."
          "You hear chaos behind you."
          "You hear nothing ahead."
          ""
          "Move forward."
          ""
          "Press ENTER to begin." ]

    let lines =
        paragraphs
        |> List.collect (fun paragraph ->
            if paragraph = "" then
                [ "" ]
            elif paragraph = "Train to Busan" then
                [ paragraph ]
            else
                wrapText 54 paragraph)

    let builder = StringBuilder()

    for line in lines do
        builder.AppendLine(line) |> ignore
        renderText (builder.ToString())

        if line <> "Press ENTER to begin." then
            Thread.Sleep(220)

    readIntroConfirmation ()

let readCommands state =
    let rec gather (commands: Command list) =
        if Console.KeyAvailable then
            let keyInfo = Console.ReadKey(true)

            let nextCommands =
                if state.IsWin || state.IsGameOver then
                    match keyInfo.Key with
                    | ConsoleKey.Enter -> Restart :: commands
                    | ConsoleKey.Q -> Quit :: commands
                    | _ -> commands
                else
                    match keyInfo.Key with
                    | ConsoleKey.W
                    | ConsoleKey.UpArrow -> Move Up :: commands
                    | ConsoleKey.S
                    | ConsoleKey.DownArrow -> Move Down :: commands
                    | ConsoleKey.A
                    | ConsoleKey.LeftArrow -> Move Left :: commands
                    | ConsoleKey.D
                    | ConsoleKey.RightArrow -> Move Right :: commands
                    | ConsoleKey.Spacebar -> Shoot state.Facing :: commands
                    | ConsoleKey.R -> Reload :: commands
                    | ConsoleKey.Q -> Quit :: commands
                    | _ -> commands

            gather nextCommands
        else
            List.rev commands

    gather []

let applyCommands commands state =
    commands |> List.fold (fun current command -> applyCommand command current) state

let runGame () =
    Console.CursorVisible <- false
    Console.Clear()
    resetRenderer ()
    showIntro ()
    Console.Clear()
    resetRenderer ()

    let stopwatch = Stopwatch.StartNew()
    let mutable nextFrameAt = stopwatch.ElapsedMilliseconds
    let mutable state = makeInitialState ()

    renderText (buildFrameText state)

    while not state.ShouldQuit do
        let now = stopwatch.ElapsedMilliseconds

        if now >= nextFrameAt then
            let commands = readCommands state
            state <- state |> applyCommands commands |> advanceFrame
            renderText (buildFrameText state)
            nextFrameAt <- nextFrameAt + frameDurationMs
        else
            Thread.Sleep(1)

[<EntryPoint>]
let main _ =
    try
        runGame ()
        0
    finally
        Console.CursorVisible <- true
        Console.Clear()
