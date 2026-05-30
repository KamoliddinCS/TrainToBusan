namespace ArenaShooterMac

open System
open System.Collections.ObjectModel
open System.ComponentModel
open Avalonia.Media
open Avalonia.Threading

type ObservableObject() =
    let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()

    [<CLIEvent>]
    member _.PropertyChanged = propertyChanged.Publish

    member this.RaisePropertyChanged propertyName =
        propertyChanged.Trigger(this, PropertyChangedEventArgs(propertyName))

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member _.PropertyChanged = propertyChanged.Publish

type CellViewModel(x: int, y: int) as this =
    inherit ObservableObject()

    let baseBrush =
        let color =
            if (x + y) % 2 = 0 then
                Color.Parse("#132533")
            else
                Color.Parse("#10202D")

        SolidColorBrush(color) :> IBrush

    let mutable floorBrush = baseBrush
    let mutable showWall = false
    let mutable showCover = false
    let mutable showCrackedCover = false
    let mutable showPlayer = false
    let mutable showDrone = false
    let mutable showBrute = false
    let mutable showBullet = false
    let mutable showMedKit = false
    let mutable showAmmoPack = false

    member _.X = x
    member _.Y = y

    member _.FloorBrush
        with get () = floorBrush
        and set value =
            if not (Object.ReferenceEquals(floorBrush, value)) then
                floorBrush <- value
                this.RaisePropertyChanged(nameof this.FloorBrush)

    member _.ShowWall
        with get () = showWall
        and set value =
            if showWall <> value then
                showWall <- value
                this.RaisePropertyChanged(nameof this.ShowWall)

    member _.ShowCover
        with get () = showCover
        and set value =
            if showCover <> value then
                showCover <- value
                this.RaisePropertyChanged(nameof this.ShowCover)

    member _.ShowCrackedCover
        with get () = showCrackedCover
        and set value =
            if showCrackedCover <> value then
                showCrackedCover <- value
                this.RaisePropertyChanged(nameof this.ShowCrackedCover)

    member _.ShowPlayer
        with get () = showPlayer
        and set value =
            if showPlayer <> value then
                showPlayer <- value
                this.RaisePropertyChanged(nameof this.ShowPlayer)

    member _.ShowDrone
        with get () = showDrone
        and set value =
            if showDrone <> value then
                showDrone <- value
                this.RaisePropertyChanged(nameof this.ShowDrone)

    member _.ShowBrute
        with get () = showBrute
        and set value =
            if showBrute <> value then
                showBrute <- value
                this.RaisePropertyChanged(nameof this.ShowBrute)

    member _.ShowBullet
        with get () = showBullet
        and set value =
            if showBullet <> value then
                showBullet <- value
                this.RaisePropertyChanged(nameof this.ShowBullet)

    member _.ShowMedKit
        with get () = showMedKit
        and set value =
            if showMedKit <> value then
                showMedKit <- value
                this.RaisePropertyChanged(nameof this.ShowMedKit)

    member _.ShowAmmoPack
        with get () = showAmmoPack
        and set value =
            if showAmmoPack <> value then
                showAmmoPack <- value
                this.RaisePropertyChanged(nameof this.ShowAmmoPack)

    member this.Reset() =
        this.FloorBrush <- baseBrush
        this.ShowWall <- false
        this.ShowCover <- false
        this.ShowCrackedCover <- false
        this.ShowPlayer <- false
        this.ShowDrone <- false
        this.ShowBrute <- false
        this.ShowBullet <- false
        this.ShowMedKit <- false
        this.ShowAmmoPack <- false

type MainWindowViewModel() as this =
    inherit ObservableObject()

    let timer = DispatcherTimer(Interval = TimeSpan.FromMilliseconds(150.0))
    let cells =
        ObservableCollection<CellViewModel>(
            [ for y in 0 .. GameLogic.height - 1 do
                  for x in 0 .. GameLogic.width - 1 do
                      CellViewModel(x, y) ])
    let mutable state = GameLogic.createInitialState ()
    let mutable statusText = "Mission Active"
    let mutable statusBrush = SolidColorBrush(Color.Parse("#58D2B3")) :> IBrush
    let mutable scoreText = "0"
    let mutable waveText = "1 / 5"
    let mutable healthText = "5 / 5"
    let mutable ammoText = "8 / 8"
    let mutable enemyText = string state.Enemies.Length
    let mutable messageText = state.Message
    let mutable pauseText = "Pause"

    let tileAt position =
        cells.[position.Y * GameLogic.width + position.X]

    let setStatus text brush =
        statusText <- text
        statusBrush <- brush
        this.RaisePropertyChanged(nameof this.StatusText)
        this.RaisePropertyChanged(nameof this.StatusBrush)

    let refreshComputedProperties () =
        scoreText <- string state.Score
        waveText <- $"{state.Wave} / {state.MaxWave}"
        healthText <- $"{state.Player.HitPoints} / {GameLogic.maxHitPoints}"
        ammoText <- $"{state.Player.Ammo} / {GameLogic.maxAmmo}"
        enemyText <- string state.Enemies.Length
        messageText <- state.Message
        pauseText <- if state.IsPaused && not state.IsGameOver && not state.IsVictory then "Resume" else "Pause"

        this.RaisePropertyChanged(nameof this.ScoreText)
        this.RaisePropertyChanged(nameof this.WaveText)
        this.RaisePropertyChanged(nameof this.HealthText)
        this.RaisePropertyChanged(nameof this.AmmoText)
        this.RaisePropertyChanged(nameof this.EnemyText)
        this.RaisePropertyChanged(nameof this.MessageText)
        this.RaisePropertyChanged(nameof this.PauseText)

        if state.IsVictory then
            setStatus "Victory" (SolidColorBrush(Color.Parse("#F6B756")) :> IBrush)
        elif state.IsGameOver then
            setStatus "Defeat" (SolidColorBrush(Color.Parse("#FF6F61")) :> IBrush)
        elif state.IsPaused then
            setStatus "Paused" (SolidColorBrush(Color.Parse("#A7B6C3")) :> IBrush)
        elif state.Player.HitPoints <= 2 then
            setStatus "Critical" (SolidColorBrush(Color.Parse("#FF6F61")) :> IBrush)
        elif state.Player.Ammo <= 2 then
            setStatus "Low Ammo" (SolidColorBrush(Color.Parse("#F6B756")) :> IBrush)
        else
            setStatus "Mission Active" (SolidColorBrush(Color.Parse("#58D2B3")) :> IBrush)

    let renderBoard () =
        cells |> Seq.iter (fun cell -> cell.Reset())

        for wall in state.Walls do
            let cell = tileAt wall
            cell.ShowWall <- true

        for KeyValue(position, durability) in state.Cover do
            let cell = tileAt position
            cell.ShowCover <- true
            cell.ShowCrackedCover <- (durability = 1)

        for pickup in state.Pickups do
            let cell = tileAt pickup.Position

            match pickup.Kind with
            | AmmoPack ->
                cell.ShowAmmoPack <- true
                cell.FloorBrush <- SolidColorBrush(Color.Parse("#1B2838")) :> IBrush
            | MedKit ->
                cell.ShowMedKit <- true
                cell.FloorBrush <- SolidColorBrush(Color.Parse("#1E2733")) :> IBrush

        for bullet in state.Bullets do
            let cell = tileAt bullet.Position
            cell.ShowBullet <- true
            cell.FloorBrush <- SolidColorBrush(Color.Parse("#222E2A")) :> IBrush

        for enemy in state.Enemies do
            let cell = tileAt enemy.Position

            match enemy.Kind with
            | Drone -> cell.ShowDrone <- true
            | Brute -> cell.ShowBrute <- true

            cell.FloorBrush <- SolidColorBrush(Color.Parse("#251B21")) :> IBrush

        let playerCell = tileAt state.Player.Position
        playerCell.ShowPlayer <- true
        playerCell.FloorBrush <- SolidColorBrush(Color.Parse("#17353B")) :> IBrush

    let applyState nextState =
        state <- nextState

        if state.IsPaused then
            timer.Stop()
        elif not timer.IsEnabled then
            timer.Start()

        renderBoard ()
        refreshComputedProperties ()

    do
        timer.Tick.Add(fun _ -> applyState (GameLogic.tick state))
        renderBoard ()
        refreshComputedProperties ()
        timer.Start()

    member _.Cells = cells
    member _.ScoreText = scoreText
    member _.WaveText = waveText
    member _.HealthText = healthText
    member _.AmmoText = ammoText
    member _.EnemyText = enemyText
    member _.MessageText = messageText
    member _.StatusText = statusText
    member _.StatusBrush = statusBrush
    member _.PauseText = pauseText

    member _.Move direction =
        applyState (GameLogic.tryMovePlayer direction state)

    member _.Shoot direction =
        applyState (GameLogic.tryShoot direction state)

    member _.Reload () =
        applyState (GameLogic.reload state)

    member _.TogglePause () =
        let nextState = GameLogic.togglePause state
        state <- nextState

        if nextState.IsPaused then
            timer.Stop()
        elif not nextState.IsGameOver && not nextState.IsVictory then
            timer.Start()

        renderBoard ()
        refreshComputedProperties ()

    member _.Restart () =
        state <- GameLogic.restart ()
        timer.Start()
        renderBoard ()
        refreshComputedProperties ()

    member _.StopTimer () =
        timer.Stop()
