namespace ArenaShooterMac

open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Markup.Xaml

type MainWindow() as this =
    inherit Window()

    let viewModel = MainWindowViewModel()

    do
        this.DataContext <- viewModel
        this.InitializeComponent()
        this.Focusable <- true
        this.Opened.Add(fun _ -> this.Focus() |> ignore)
        this.Closing.Add(fun _ -> viewModel.StopTimer())

    member private _.ViewModel = viewModel

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

    member private this.ExecuteCommand(command: string) =
        match command with
        | "MoveUp" -> this.ViewModel.Move Up
        | "MoveDown" -> this.ViewModel.Move Down
        | "MoveLeft" -> this.ViewModel.Move Left
        | "MoveRight" -> this.ViewModel.Move Right
        | "ShootUp" -> this.ViewModel.Shoot Up
        | "ShootDown" -> this.ViewModel.Shoot Down
        | "ShootLeft" -> this.ViewModel.Shoot Left
        | "ShootRight" -> this.ViewModel.Shoot Right
        | "Reload" -> this.ViewModel.Reload()
        | "Pause" -> this.ViewModel.TogglePause()
        | "Restart" -> this.ViewModel.Restart()
        | _ -> ()

    member this.OnCommandButtonClick(sender: obj, _: RoutedEventArgs) =
        match sender with
        | :? Button as button ->
            match button.Tag with
            | :? string as command -> this.ExecuteCommand(command)
            | _ -> ()
        | _ -> ()

    override this.OnKeyDown eventArgs =
        base.OnKeyDown(eventArgs)

        let command =
            match eventArgs.Key with
            | Key.W -> Some "MoveUp"
            | Key.S -> Some "MoveDown"
            | Key.A -> Some "MoveLeft"
            | Key.D -> Some "MoveRight"
            | Key.Up -> Some "ShootUp"
            | Key.Down -> Some "ShootDown"
            | Key.Left -> Some "ShootLeft"
            | Key.Right -> Some "ShootRight"
            | Key.R -> Some "Reload"
            | Key.P -> Some "Pause"
            | Key.Enter when eventArgs.KeyModifiers = KeyModifiers.None -> Some "Restart"
            | _ -> None

        match command with
        | Some value ->
            this.ExecuteCommand(value)
            eventArgs.Handled <- true
        | None -> ()
