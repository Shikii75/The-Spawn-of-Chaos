# Dojo Entrance Setup

Attach `DojoDoorTransition` to the trigger collider in front of each dojo door.

For an entrance door:
- Set `Destination Point` to an empty GameObject inside the dojo interior.
- Set `Available Prompt Text` to `Press E to enter`.
- Optional: add the exterior objects to `Disable On Arrival` and the interior objects to `Enable On Arrival`.

For an exit door:
- Set `Destination Point` to an empty GameObject outside the dojo door.
- Set `Available Prompt Text` to `Press E to leave`.
- Enable `Requires Dojo Cleared` and assign the room's `DojoWaveManager` if the player should not leave before finishing the fight.
- Optional: add the exterior objects to `Enable On Arrival` and the interior objects to `Disable On Arrival`.

When the turnaround animation is ready, add a trigger parameter named `TurnIn` to the player Animator, or change `Turn In Trigger` on the door to match your trigger name.
