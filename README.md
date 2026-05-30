# Train to Busan

## Description

`Train to Busan` is a real-time ASCII survival game written in F# for .NET 10.

You are a passenger trapped on a moving train after a sudden infection outbreak begins near the rear carriage on your way from Seoul. The only safe direction is forward. Survive five train cars, manage a single returning projectile, and reach the engine before the infected overrun you.

## How To Run

### Prerequisites

- `.NET 10 SDK`

Verify the installation with:

```bash
dotnet --version
```

### Run From The Repository Root

```bash
dotnet run --project TrainToBusan/TrainToBusan.fsproj
```

### Run From The Project Directory

```bash
cd TrainToBusan
dotnet run
```

### Build

```bash
dotnet build TrainToBusan/TrainToBusan.fsproj
```

## Controls

- `W` move up
- `S` move down
- `A` move left
- `D` move right
- Arrow keys also move the player
- `Space` shoot in the current facing direction
- `R` recall or recover the projectile
- `Q` quit
- `Enter` restart after a win or loss

## Symbol Legend

- `P` player
- `Z` zombie
- `*` projectile
- `#` seats and obstacles
- `D` door to the next car
- `+ - |` train-car border
- ` ` empty floor

## Win And Loss Conditions

You win by clearing or escaping through all five train cars and reaching the engine.

You lose when your HP reaches `0`.

## LLM Usage

This project was developed with LLM assistance during design and implementation.

What the LLM was used for:

- helping organize the project into clear game-state, update, rendering, input, and helper logic,
- giving logic guidance for how to structure the implementation in F#,
- suggesting that the project should stay console-based and use built-in .NET libraries such as `System.Console`, `System.Diagnostics`, and `System.Threading` instead of external graphics libraries,
- implementing the non-blocking keyboard movement using `Console.KeyAvailable` and `Console.ReadKey(true)`, and
- implementing the continuous real-time game loop and frame-based updates.

What had to be manually changed or reprompted:

- the rendering logic needed multiple follow-up fixes to reduce flicker and frame lag,
- the intro and gameplay message text needed additional iteration so long narrative lines would not overlap or wrap unpredictably in the terminal, and
- the message system was revised so it updates only for meaningful gameplay events instead of changing constantly during normal movement.

Main point the LLM did not do correctly at first:

- the terminal rendering behavior was not fully correct in the first versions, especially around stable screen refresh, text wrapping, and message-area layout, so those parts required repeated debugging, reprompting, and manual verification.

The game has no runtime AI dependency and runs entirely as a local .NET console application.

## Important Note

Please expand your terminal window before playing. This avoids layout issues that can happen with a narrow console.
