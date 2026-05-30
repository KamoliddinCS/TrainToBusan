# Train To Busan: Requirements

## Overview

This project is a real-time command-line ASCII survival game written in F# using .NET 10. The user plays as a passenger trapped on a moving train during a zombie outbreak. The user must move forward through five train cars, avoid or defeat zombies, and reach the engine before losing all HP.

## Requirements

1. When the program starts, it will display a narrative intro in the terminal and wait for the user to press `Enter` before gameplay begins.
2. During gameplay, the game will run in a continuous real-time loop and redraw the terminal repeatedly without requiring the user to press `Enter` for each action.
3. Each train car will be rendered as an ASCII frame using `+`, `-`, and `|` around an internal playable area.
4. The internal playable area of each train car will be `18` columns wide and `4` rows tall.
5. The game will contain exactly five train cars. The user wins by advancing through all five cars and reaching the engine.
6. The game will use the following symbols during gameplay:
   - `P` for the player
   - `Z` for a zombie
   - `*` for the projectile
   - `#` for a seat or obstacle
   - `D` for the door to the next car
   - space for an empty floor tile
7. The user will control the player with the keyboard during gameplay. Pressing `W`, `A`, `S`, or `D` will move the player up, left, down, or right. The arrow keys will also move the player in the corresponding direction.
8. Moving the player will also change the player’s facing direction to the direction of movement, even if the attempted movement is blocked.
9. The player cannot move outside the playable grid, through obstacles, or through zombies.
10. The player starts with `3` HP. If HP reaches `0`, the game ends in a loss.
11. Pressing `Space` will launch the projectile in the player’s current facing direction only if the projectile is in the `READY` state.
12. The player can have only one projectile. While the projectile is in flight, pressing `Space` again will not create another projectile.
13. If the projectile moves into a zombie, that zombie will be removed, the player’s score will increase by `1`, and the projectile will begin returning toward the player.
14. If the projectile moves into an obstacle or the edge of the train car, it will reverse into a returning state instead of disappearing.
15. When the returning projectile reaches the player, the projectile state becomes `READY` again and the player may shoot again.
16. Pressing `R` while the projectile is in flight and not already returning will force the projectile to start returning toward the player.
17. Each train car will begin with a fixed number of zombies:
   - Car 1: `1` zombie
   - Car 2: `2` zombies
   - Car 3: `3` zombies
   - Car 4: `4` zombies
   - Car 5: `5` zombies
18. Zombies will move toward the player automatically at a slower interval than the screen refresh.
19. Zombies cannot move through obstacles, outside the playable grid, through the exit door tile, or onto another zombie’s position.
20. If a zombie reaches the player’s position, the player loses `1` HP and that zombie is removed from the train car.
21. The door `D` to the next car will remain closed while at least one zombie remains in the current car.
22. When the last zombie in the current car is defeated, the door will open.
23. If the player moves onto the open door tile, the game will load the next train car, reset the player to the next car’s entry position, and reset the projectile to `READY`.
24. After entering the fifth car’s exit, the game will display a win ending.
25. Each gameplay frame will display the train car map, a status line, a message line or block, and a controls reminder.
26. The status display will show the player’s HP, projectile state, score, and current car number.
27. The message area will show short event-driven narrative updates for important events such as defeating a zombie, unlocking a door, advancing to the next car, winning, or losing.
28. Pressing `Q` during gameplay will quit the program.
29. After a win or loss, pressing `Enter` will restart the game from the beginning.
30. The game’s car layouts, zombie placements, and obstacle placements will be fixed rather than random so that the game behavior remains deterministic and testable.

## Example Interaction

The game displays the intro text and waits for the user to press `Enter`. The first train car appears with the player near the left side of the car, one zombie ahead, and a closed door at the far right. The user presses `D` to move forward and `Space` to launch the projectile. The projectile hits the zombie, the score increases to `1`, and the message changes to indicate that the infected was defeated or the door unlocked. The user moves onto the open door to advance into the next car. The game redraws continuously during this process without requiring line-based text input.
