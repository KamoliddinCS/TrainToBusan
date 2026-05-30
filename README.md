# F# Game Collection

This repo now includes three F# game projects:

- `AsciiArenaShooter`: the original F# console arena shooter
- `ArenaShooterMac`: the native F# desktop GUI version built with Avalonia
- `TrainToBusan`: the real-time ASCII train survival game

## Run the F# console version

```bash
dotnet run --project AsciiArenaShooter/AsciiArenaShooter.fsproj
```

### Console controls

- `w` `a` `s` `d` to move
- `shoot up`
- `shoot down`
- `shoot left`
- `shoot right`
- `reload`

### Console self-test

```bash
dotnet run --project AsciiArenaShooter/AsciiArenaShooter.fsproj -- --self-test
```

## Run the native GUI version

```bash
dotnet run --project ArenaShooterMac/ArenaShooterMac.fsproj
```

#### Native GUI controls

- `W` `A` `S` `D` to move
- Arrow keys to shoot
- `R` to reload
- `P` to pause or resume
- `Enter` to restart after a run
- Or use the on-screen buttons

## Run Train to Busan

```bash
dotnet run --project TrainToBusan/TrainToBusan.fsproj
```

### Train to Busan controls

- `W` `A` `S` `D` to move
- Arrow keys also move
- `Space` to throw the projectile
- `R` to recall the projectile
- `Q` to quit
- `Enter` to restart after a win or loss

### Browser GUI

From the repo root, start a small static server:

```bash
python3 -m http.server 8080
```

Then open:

```text
http://localhost:8080/gui/
```

### GUI controls

- `W` `A` `S` `D` to move
- Arrow keys to shoot
- `R` to reload
- `Space` to wait a turn
- Or use the on-screen control buttons
