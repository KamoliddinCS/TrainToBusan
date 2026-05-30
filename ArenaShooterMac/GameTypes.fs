namespace ArenaShooterMac

type Direction =
    | Up
    | Down
    | Left
    | Right

type Position = { X: int; Y: int }

type EnemyKind =
    | Drone
    | Brute

type Enemy =
    { Id: int
      Position: Position
      Kind: EnemyKind
      HitPoints: int }

type Bullet =
    { Position: Position
      Direction: Direction
      RangeRemaining: int }

type PickupKind =
    | AmmoPack
    | MedKit

type Pickup =
    { Position: Position
      Kind: PickupKind }

type Player =
    { Position: Position
      HitPoints: int
      Ammo: int
      Facing: Direction }

type GameState =
    { Width: int
      Height: int
      Player: Player
      Enemies: Enemy list
      Bullets: Bullet list
      Walls: Set<Position>
      Cover: Map<Position, int>
      Pickups: Pickup list
      Score: int
      Wave: int
      MaxWave: int
      Tick: int
      Message: string
      NextEnemyId: int
      IsPaused: bool
      IsVictory: bool
      IsGameOver: bool }
